# Networking (Mirror)

The game uses **Mirror** for multiplayer (Steam P2P transport, KCP fallback; stock
`Mirror.NetworkManager`, no game subclass). ASL gives mods two things through `ctx.Net`
(`IAslNet`): a safe **awareness** layer, and a **message transport** for sending arbitrary bytes
between the host and clients.

## 1. Awareness — `IAslNet` state

Read-only Mirror state plus a connection-count poll. No patches, no risk:

```csharp
public interface IAslNet
{
    bool IsOnline { get; }            // server and/or client active
    bool IsServer { get; }            // we run the server (host or dedicated)
    bool IsClient { get; }            // we run a client (incl. the host's local client)
    bool IsHost  { get; }             // server + client
    bool IsConnectedClient { get; }   // our client is connected
    int  ConnectionCount { get; }     // server-side connected clients
    event Action<int> ConnectionsChanged;   // server-side, fires with the new count
    // ... message transport, below ...
}
```

```csharp
public override void OnLoad(IModContext ctx)
{
    if (ctx.Net.IsHost) ctx.Log.Info("I'm the host.");
    ctx.Net.ConnectionsChanged += n => ctx.Log.Info($"{n} client(s) connected.");
}
```

## 2. Message transport — send bytes between peers

```csharp
bool MessagingAvailable { get; }
bool Send(string channel, byte[] data);                            // client→server, or host→all
bool SendToServer(string channel, byte[] data);                    // client → server
bool SendToAll(string channel, byte[] data);                       // server → all clients
bool SendToClient(int connectionId, string channel, byte[] data);  // server → one client
void Subscribe(string channel, Action<AslNetMessage> handler);
void Unsubscribe(string channel, Action<AslNetMessage> handler);
```

A mod sends bytes on a **named channel**; a mod on the other peer that `Subscribe`d to the same
channel receives an `AslNetMessage` (`Channel`, `Data`, `SenderConnectionId`, `FromServer`,
`FromClient`). Both ends must run a mod using the same channel string — namespace it with your mod
id (`"com.author.mymod/state"`) so mods don't collide.

```csharp
const string Ch = "com.author.mymod/ping";
ctx.Net.Subscribe(Ch, msg =>
{
    if (msg.FromClient)   // we're the server: a client pinged us
        ctx.Net.SendToClient(msg.SenderConnectionId, Ch, new byte[] { 1 });   // pong back
    else                  // we're a client: got the host's pong
        ctx.Log.Info("pong");
});
// elsewhere, from a client:
ctx.Net.SendToServer(Ch, new byte[] { 0 });
```

Rules of thumb: gate on `MessagingAvailable` (or the `Send*` return value); keep messages small and
infrequent (they ride Mirror's **reliable** channel); handlers run on the **main thread** so they
may touch game state but must stay fast.

## 3. Typed messages — send objects, not bytes

Packing bytes by hand gets old fast. Define a message type instead and let ASL serialize it. Implement
`IAslMessage` (write your fields, read them back in the same order) and use the typed `Send<T>` /
`Subscribe<T>` overloads — same channels, same routing, no `byte[]` in sight.

```csharp
public sealed class ScoreUpdate : IAslMessage
{
    public int PlayerId;
    public int Score;
    public string Name;

    public void Write(AslWriter w) { w.WriteInt(PlayerId); w.WriteInt(Score); w.WriteString(Name); }
    public void Read(AslReader r)  { PlayerId = r.ReadInt(); Score = r.ReadInt(); Name = r.ReadString(); }
}

const string Ch = "com.author.mymod/score";

// receive (handler gets the typed message + the raw AslNetMessage for sender info):
ctx.Net.Subscribe<ScoreUpdate>(Ch, (msg, raw) =>
    ctx.Log.Info($"{msg.Name} now has {msg.Score} (from conn {raw.SenderConnectionId})"));

// send:
ctx.Net.SendToAll(Ch, new ScoreUpdate { PlayerId = 1, Score = 42, Name = "Radomir" });
```

`AslWriter` / `AslReader` cover the primitives (`bool`, `byte`, `short`, `int`, `uint`, `long`,
`ulong`, `float`, `double`), length-prefixed `string` (UTF-8), and raw `byte[]` blobs — read in the
**same order** you wrote them. The typed layer is plain managed serialization on top of the byte
transport above, so all the same rules apply (gate on `MessagingAvailable`, keep it small, main
thread). A short/garbled payload is logged and skipped per handler, never thrown at your mod.

## 4. Player identity — who's in the session

`ctx.Net` surfaces the players in the current session, each as an `IAslPlayer` that pairs the game's
own player object with its Mirror identity:

```csharp
public interface IAslPlayer
{
    MetaPlayer Player { get; }   // the real game player object (read/manipulate its state)
    uint   NetId        { get; } // Mirror net id — same on every peer, stable for the session
    int    ConnectionId { get; } // server-side id (matches AslNetMessage.SenderConnectionId), else -1
    bool   IsLocal      { get; } // this is you
    string Name         { get; } // display name (best-effort, see note)
}

IReadOnlyList<IAslPlayer> Players { get; }   // snapshot of everyone in the session
IAslPlayer LocalPlayer { get; }              // you (or null)
IAslPlayer GetPlayer(int connectionId);      // map a sender back to a player (server-side)
event Action<IAslPlayer> PlayerJoined;
event Action<IAslPlayer> PlayerLeft;
```

The killer combo is `GetPlayer` + the message transport: a server receives a message, reads
`SenderConnectionId`, and resolves the actual player to act on.

```csharp
ctx.Net.PlayerJoined += p => ctx.Log.Info($"{p.Name} joined");
ctx.Net.Subscribe<ScoreUpdate>(Ch, (msg, raw) =>
{
    var who = ctx.Net.GetPlayer(raw.SenderConnectionId);   // server: who sent this?
    if (who != null) ctx.Log.Info($"{who.Name} scored {msg.Score}");
});
```

Notes: `Players`/events come from a lightweight poll, so they run on the **main thread**; the list is
rebuilt on read, so capture it if you need a stable view. `PlayerLeft` hands you a last-known snapshot
— that player's `Player` may already be destroyed. **Name** is best-effort: the **local** player's
name is read from Steam; **remote** players' names are empty for now (a later version sources them via
each player's Steam id). `NetId` / `ConnectionId` / `IsLocal` / `Player` are always reliable.

## 5. Synced state — a shared key/value store

For "everyone should see the same value" (scores, a round state, a flag) reach for `ctx.Net.GetSync`
instead of wiring messages by hand. It's a **host-authoritative** key/value store: the host sets
values, they replicate to every client, and a client that joins late is sent the full current state.

```csharp
var state = ctx.Net.GetSync("com.author.mymod/state");   // same id everywhere -> same store

state.Changed += (key, value) => ctx.Log.Info($"{key} = {value}");   // host on Set, client on receive

if (ctx.Net.IsServer)                       // only the host writes
    state.Set("round", "warmup");

string round = state.Get("round");          // anyone reads
foreach (var kv in state.All) { /* ... */ }
```

`Set` is **host-only** (ignored on a client). Values are strings — pack numbers/JSON yourself if you
need more. `Changed` runs on the **main thread**. It rides the message transport, so host→client
replication is verified in-game (PropHunt replicates every player's disguise through this store).

## How it works

Defining a new Mirror message struct and calling `NetworkClient.Send<T>` is the textbook approach —
and it is **blocked** on this Unity 6 / IL2CPP build for a brand-new `T`, for two compounding
reasons (confirmed from the IL2CPP dump):

1. **Generic methods are AOT-only.** `Send<T>`, `RegisterHandler<T>`, `Pack<T>`,
   `NetworkMessageId<T>` are open generics with no standalone compiled body — only the message types
   the game itself ships have compiled instantiations. A brand-new `T` has no native code.
2. **Serializers are weaver-baked.** Mirror's `Writer<T>.write` / `Reader<T>.read` are populated at
   the game's build time by its IL post-processor; there is no public API to add them at runtime and
   IL2CPP can't emit them on the fly, so a new message type has no (de)serializer.

ASL sidesteps both by **never introducing a new type**. It reuses
`EntityStateMessage { uint netId; ArraySegment<byte> payload }` — a message the game already ships,
so its serializer and registered handler are compiled and present. The transport:

- **Receives** by a Harmony **prefix** on Mirror's two handlers,
  `NetworkClient.OnEntityStateMessage` and `NetworkServer.OnEntityStateMessage` (both exist on this
  build — the server registers `EntityStateMessage` too, so client→server is safe). The prefix does
  one `uint` compare against a **sentinel `netId`** (`0xA51A51A5`, a value Mirror never assigns) on
  the hot path; for a match it decodes the payload, dispatches to your subscribers, and **consumes**
  the message (skips the game handler). Every real entity message passes straight through untouched.
  *Confirmed in-game against the live interop assemblies — the prefix reliably intercepts real
  `EntityStateMessage` traffic.*
- **Sends** by going **straight to the active transport** (`Transport.active.ClientSend` /
  `ServerSend`) rather than the typed `Send<EntityStateMessage>`. On this IL2CPP build the generic
  `Send<T>` is not dispatched correctly through the interop layer, and the low-level
  `NetworkConnection.Send(segment, …)` is inlined away (unpatchable / unreachable). So ASL builds the
  complete Mirror transport batch itself — `[double localTime][CompressVarUInt(len)][message]`, where
  the message is `[ushort EntityStateMessage-id][uint sentinel netId][payload]` — using Mirror's own
  `NetworkWriter` / `Compression` for exact framing, with the message id read at runtime from
  `NetworkMessages.Lookup`. The receiver's normal un-batch → dispatch path then routes it to the
  `OnEntityStateMessage` prefix above. The payload is wrapped `[magic "ASL1"][version][channel][data]`.

Two gates (sentinel netId **and** the payload magic) make a false match between ASL packets and real
entity traffic effectively impossible. The intercept is installed **lazily** on the first
`Send`/`Subscribe`, so players running no networking mods pay zero netcode overhead.

## Verification status

> **Confirmed in-game: full two-peer round trip works (`NET TEST: PASS`).**
> Receive was verified on real traffic (server-side in a solo host, client-side against a real remote
> host); send delivery was verified by a host→client packet physically arriving at a second peer. An
> early test exposed a **wire-format bug** — we wrote `EntityStateMessage.netId` as a fixed 4-byte
> uint, but the game reads it as a `CompressVarUInt`, which threw `EndOfStreamException` on
> deserialize. After fixing it (netId now var-compressed), a real two-peer session logs the end-to-end
> **`NET TEST: PASS — client↔host round trip confirmed`**. PropHunt — a full multiplayer game mode —
> runs entirely on this transport and its synced store.
>
> **Why a single machine isn't enough:** a solo host's transport does not echo injected packets back
> to itself (Steam P2P to the same account has no loopback; the game delivers its own host↔client
> messages over an in-memory path that can't be injected into). So a solo session can't exercise the
> round trip — you need two peers.
>
> **Reproducing the check:** start a session on one machine/account and have a **second** peer
> (another machine, another Steam account, or a friend) join via the in-game join code, with the
> sample `HelloMod` present on both. The joining client auto-pings the host; watch
> `BepInEx\LogOutput.log` on the client for `NET TEST: PASS — client↔host round trip confirmed`. To
> capture low-level wire diagnostics, set `NetTransport.Diagnostics = true` and rebuild.

## Limits & notes

- **Reliable channel only** for now (ordered, guaranteed). No unreliable/unordered option yet.
- **Message size**: keep payloads well under Mirror's per-message cap; this is for small mod
  signals/state, not bulk transfer.
- **Same mod on both ends**: the transport carries opaque bytes — it does not match versions for
  you. If your wire format changes, bump your own version byte inside `Data`.
- **No solo loopback**: a host alone in a session can't message its own local client through this
  transport (the game delivers host↔local-client traffic over an in-memory path, not the wire). The
  transport carries traffic to **remote** peers; design mods to act on a real connection, and don't
  rely on a host receiving its own `SendToAll` when no one else is connected.
