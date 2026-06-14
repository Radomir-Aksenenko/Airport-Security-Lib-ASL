using ASL.Api;
using Metater;

namespace HelloMod
{
    /// <summary>
    /// Sample / template ASL mod. Reference ASL.API, subclass <see cref="AslMod"/>, subscribe to
    /// events and register menu controls in <see cref="OnLoad"/>. Build it, then drop
    /// <c>HelloMod.dll</c> + <c>manifest.json</c> into <c>mods/HelloMod/</c>.
    /// </summary>
    public sealed class HelloMod : AslMod
    {
        // Channel for the ping/pong round-trip test — namespaced with the mod id so it can't clash.
        private const string PingChannel = "com.example.hellomod/ping";

        private IModContext _ctx;
        private bool _firstFrameLogged;
        private bool _logScenes = true;

        // Net round-trip test state. A real client↔host round trip needs an actual second peer, so this
        // only runs on a PURE client (one that joined a remote host): it pings the host until it gets a
        // pong, proving both directions of the transport. In solo it stays idle (no false failure).
        private bool _netTestDone;
        private int _netTestFrames, _pings, _pingSeq;
        private string _lastNetState;
        private bool _identityLogged;
        private IAslSync _sync;
        private bool _syncTested, _syncChangedSeen;

        public override void OnLoad(IModContext ctx)
        {
            _ctx = ctx;
            ctx.Log.Info($"Hello from {ctx.ModName}! Wiring up ASL events and menu.");

            ctx.Events.Update += OnUpdate;
            ctx.Events.SceneChanged += OnSceneChanged;
            ctx.Events.LocalPlayerChanged += OnLocalPlayerChanged;

            // Demo controls in the shared F8 menu, grouped under this mod's name.
            ctx.Menu.AddLabel("A sample mod wired to ASL events.");
            ctx.Menu.AddToggle("Log scene changes", _logScenes, on => _logScenes = on);
            ctx.Menu.AddButton("Say hello in the log", () => ctx.Log.Info("Hello from the menu button!"));
            ctx.Menu.AddButton("Log network status", () =>
                ctx.Log.Info($"Net: online={ctx.Net.IsOnline} host={ctx.Net.IsHost} client={ctx.Net.IsConnectedClient} conns={ctx.Net.ConnectionCount}"));

            // Networking awareness: log when the connection count changes (server side).
            ctx.Net.ConnectionsChanged += n => ctx.Log.Info($"Connections changed -> {n}");

            // Player identity: react to joins/leaves and offer a roster dump in the menu.
            ctx.Net.PlayerJoined += p => ctx.Log.Info($"Player joined: '{p.Name}' (netId={p.NetId}, conn={p.ConnectionId})");
            ctx.Net.PlayerLeft  += p => ctx.Log.Info($"Player left: '{p.Name}' (netId={p.NetId})");
            ctx.Menu.AddButton("List players", () =>
            {
                var ps = ctx.Net.Players;
                ctx.Log.Info($"Players ({ps.Count}):");
                foreach (var p in ps) ctx.Log.Info($"  netId={p.NetId} conn={p.ConnectionId} local={p.IsLocal} name='{p.Name}'");
            });

            // Synced state: a host-authoritative key/value store, shared with all clients.
            _sync = ctx.Net.GetSync("com.example.hellomod/state");
            _sync.Changed += (key, value) =>
            {
                ctx.Log.Info($"SYNC changed: {key} = '{value}'");
                if (key == "greeting") _syncChangedSeen = true;
            };
            ctx.Menu.AddButton("Set sync greeting (host)", () =>
            {
                bool ok = _sync.Set("greeting", "hi #" + (++_pingSeq));
                ctx.Log.Info($"Sync Set greeting -> {ok}");
            });

            // Networking: typed ping/pong round-trip. Both peers run this same handler. ASL serializes
            // the HelloPing message for us — no manual byte packing.
            ctx.Net.Subscribe<HelloPing>(PingChannel, OnPing);
            ctx.Menu.AddButton("Ping over the network", SendPing);

            // In-process check that the typed (de)serialization round-trips. Independent of the network,
            // so it confirms the typed-message layer itself even in a solo session.
            RunTypedSerializationSelfTest();
        }

        // Serialize a HelloPing and read it back; confirm the fields survived. Proves AslWriter/AslReader
        // + IAslMessage round-trip without needing a second peer.
        private void RunTypedSerializationSelfTest()
        {
            try
            {
                var w = new AslWriter();
                var src = new HelloPing { Seq = 1337, Text = "typed round-trip ✓" };
                src.Write(w);
                var dst = new HelloPing();
                dst.Read(new AslReader(w.ToArray()));
                bool ok = dst.Seq == src.Seq && dst.Text == src.Text;
                _ctx.Log.Info(ok
                    ? "TYPED MSG SELF-TEST: PASS — IAslMessage serialize↔deserialize round-trips."
                    : $"TYPED MSG SELF-TEST: FAIL — got Seq={dst.Seq} Text='{dst.Text}'.");
            }
            catch (System.Exception ex) { _ctx.Log.Error($"TYPED MSG SELF-TEST: FAIL — {ex.Message}"); }
        }

        // Drives the round-trip test from a PURE client (joined a remote host): pings the host until a
        // pong comes back (PASS), proving client→server and server→client. Idle otherwise (solo host has
        // no remote peer to echo, so there is nothing meaningful to test and we don't cry failure).
        private void MaybeRunNetSelfTest()
        {
            if (_netTestDone) return;
            var net = _ctx.Net;

            string st = $"online={net.IsOnline} host={net.IsHost} server={net.IsServer} client={net.IsClient} connected={net.IsConnectedClient} conns={net.ConnectionCount} msg={net.MessagingAvailable}";
            if (st != _lastNetState) { _lastNetState = st; _ctx.Log.Info($"NET TEST: state -> {st}"); }

            bool pureClient = net.IsOnline && net.IsConnectedClient && !net.IsServer && net.MessagingAvailable;
            if (!pureClient) return;   // only a real remote client can run the round trip

            _netTestFrames++;
            if (_netTestFrames % 60 == 0 && _pings < 15)
            {
                _pings++;
                bool sent = net.SendToServer(PingChannel, new HelloPing { Seq = _pings, Text = "ping" });
                _ctx.Log.Info($"NET TEST (client): ping #{_pings} sent={sent}; awaiting pong...");
            }
            if (_pings >= 15 && !_netTestDone)
            {
                _netTestDone = true;
                _ctx.Log.Error("NET TEST: FAIL — sent 15 pings to the host, no pong returned.");
            }
        }

        // Send a typed ping. From a client it goes to the host; from the host it broadcasts to all.
        private void SendPing()
        {
            if (!_ctx.Net.IsOnline) { _ctx.Log.Info("Ping: not in a multiplayer session."); return; }
            bool sent = _ctx.Net.Send(PingChannel, new HelloPing { Seq = ++_pingSeq, Text = "ping" });
            _ctx.Log.Info(sent ? "Ping sent." : "Ping failed (transport unavailable?).");
        }

        private void OnPing(HelloPing msg, AslNetMessage raw)
        {
            if (raw.FromClient)   // we're the host: a client pinged us — reply just to them
            {
                _ctx.Log.Info($"NET TEST (host): got ping #{msg.Seq} '{msg.Text}' from client {raw.SenderConnectionId}; replying 'pong'.");
                _ctx.Net.SendToClient(raw.SenderConnectionId, PingChannel, new HelloPing { Seq = msg.Seq, Text = "pong" });
            }
            else                  // we're a client: this came from the host
            {
                if (msg.Text == "pong" && !_netTestDone)
                {
                    _netTestDone = true;
                    _ctx.Log.Info($"NET TEST: PASS — client↔host round trip confirmed (pong #{msg.Seq}).");
                }
                else
                {
                    _ctx.Log.Info($"NET TEST (client): got '{msg.Text}' #{msg.Seq} from the host.");
                }
            }
        }

        private void OnUpdate()
        {
            if (!_firstFrameLogged)              // log only the first tick to keep the log clean
            {
                _firstFrameLogged = true;
                _ctx.Log.Info("First Update tick received - the event bus is live. Press F8 for the menu.");
            }
            MaybeRunNetSelfTest();
            MaybeLogIdentity();
            MaybeRunSyncTest();
        }

        // One-shot: on the host, set a synced value and confirm it applied locally and fired Changed.
        // Verifiable in solo (the cross-peer replication rides on the transport, like the ping test).
        private void MaybeRunSyncTest()
        {
            if (_syncTested) return;
            var net = _ctx.Net;
            if (!net.IsOnline || !net.IsServer) return;   // host is authoritative

            _syncTested = true;
            const string val = "hello from host";
            bool sent = _sync.Set("greeting", val);
            string got = _sync.Get("greeting");
            bool ok = got == val && _syncChangedSeen;
            _ctx.Log.Info(ok
                ? $"SYNC SELF-TEST: PASS (set/get/Changed work locally; broadcast sent={sent})."
                : $"SYNC SELF-TEST: FAIL — got='{got}' changedSeen={_syncChangedSeen}.");
        }

        // One-shot: once a session is up and the local player exists, log the roster. Works in solo
        // (the host's own player is there), so it confirms player identity without a second peer.
        private void MaybeLogIdentity()
        {
            if (_identityLogged) return;
            var net = _ctx.Net;
            if (!net.IsOnline) return;
            var local = net.LocalPlayer;
            if (local == null) return;

            _identityLogged = true;
            var players = net.Players;
            _ctx.Log.Info($"IDENTITY: local player '{local.Name}' netId={local.NetId} conn={local.ConnectionId}; {players.Count} player(s) in session.");
            foreach (var p in players)
                _ctx.Log.Info($"IDENTITY:   netId={p.NetId} conn={p.ConnectionId} local={p.IsLocal} name='{p.Name}'");
        }

        private void OnSceneChanged(string sceneName)
        {
            if (_logScenes) _ctx.Log.Info($"Scene changed -> {sceneName}");
        }

        private void OnLocalPlayerChanged(MetaPlayer player)
        {
            _ctx.Log.Info(player != null ? "Local player is ready!" : "Local player cleared.");
        }
    }

    /// <summary>
    /// A typed network message (see <see cref="IAslMessage"/>). Sent/received with the typed
    /// <c>ctx.Net.Send&lt;HelloPing&gt;</c> / <c>Subscribe&lt;HelloPing&gt;</c> overloads — ASL handles
    /// serialization, so the mod never packs bytes by hand.
    /// </summary>
    public sealed class HelloPing : IAslMessage
    {
        public int Seq;
        public string Text;

        public void Write(AslWriter writer)
        {
            writer.WriteInt(Seq);
            writer.WriteString(Text);
        }

        public void Read(AslReader reader)
        {
            Seq = reader.ReadInt();
            Text = reader.ReadString();
        }
    }
}
