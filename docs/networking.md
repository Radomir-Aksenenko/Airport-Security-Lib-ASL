# Networking (Mirror)

The game uses **Mirror** for multiplayer (Steam P2P transport, KCP fallback; stock
`Mirror.NetworkManager`, no game subclass). ASL exposes a safe **awareness** layer today, and a
custom-message **transport** is the planned next step — held back by a real IL2CPP constraint
described below.

## What ASL gives mods today: `IAslNet`

`ctx.Net` is read-only awareness — pure Mirror state reads and a connection-count poll, so it has no
generics, no patches, and no risk to the netcode:

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
}
```

With this a mod can already: behave differently on host vs client vs single-player, gate logic on
being connected, and react when players join or leave.

```csharp
public override void OnLoad(IModContext ctx)
{
    if (ctx.Net.IsHost) ctx.Log.Info("I'm the host.");
    ctx.Net.ConnectionsChanged += n => ctx.Log.Info($"{n} client(s) connected.");
}
```

## Why there's no custom-message transport yet

Sending arbitrary data between clients would normally mean defining a Mirror message struct and
calling `NetworkClient.Send<T>` / `NetworkServer.RegisterHandler<T>`. On this **Unity 6 / IL2CPP**
build that does not work for a *new* mod-defined type, for two compounding reasons (confirmed from
the IL2CPP dump):

1. **Generic methods are AOT-only.** `Send<T>`, `RegisterHandler<T>`, `Pack<T>`,
   `NetworkMessageId<T>` are open generics with no standalone compiled body — only the ~28 message
   types the game itself ships have compiled instantiations. A brand-new `T` has no native code.
2. **Serializers are weaver-baked.** Mirror's `Writer<T>.write` / `Reader<T>.read` are populated at
   the game's build time by its IL post-processor. There is no public API to add them at runtime,
   and IL2CPP can't emit them on the fly, so a new message type has no (de)serializer.

So "just define a message struct" is blocked.

## The planned approach (next step)

Two viable paths exist; both reuse machinery that already has compiled code:

- **Envelope through an existing message (preferred).** Reuse an already-instantiated message that
  carries an `ArraySegment<byte>` payload — `EntityStateMessage { uint netId; ArraySegment<byte>
  payload }` is the cleanest. Send it with a **sentinel `netId`** that the game never assigns, pack
  the mod's bytes into `payload`, and intercept it with a Harmony prefix on the existing handler
  (consume-and-cancel ASL packets, pass everything else through). This stays on Mirror's reliable
  channel and framing.
- **Raw transport (most flexible).** Call `Transport.activeTransport.ClientSend` / `ServerSend`
  with a magic-prefixed buffer and intercept `OnClientDataReceived` / `OnServerDataReceived`. Fully
  arbitrary bytes, but you give up Mirror's framing and must not collide with its 2-byte ids.

ASL would wrap whichever path in one owned envelope and expose something like
`Net.Send(modId, byte[])` + `Net.OnMessage(modId, handler)`, so individual mods get a namespaced
channel without touching Mirror.

**Why it isn't shipped yet:** both paths run code in the netcode hot path (a Harmony prefix on a
message/transport handler). Getting it wrong can desync or disconnect players, and it can only be
validated with a real **two-client session** — which the current single-machine workflow can't
exercise. Shipping unverified netcode that patches multiplayer traffic is the wrong trade, so the
transport waits until it can be tested host↔client.
