using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;
using HarmonyLib;
using Metater;
using Mirror;

namespace ASL
{
    /// <summary>
    /// Implements <see cref="IAslNet"/>. The awareness half is plain Mirror state reads and a count
    /// poll (no patches, always safe). The message-transport half is delegated to
    /// <see cref="NetTransport"/>, which is installed lazily on first use, so the netcode hot path is
    /// untouched until a mod actually sends or subscribes. <see cref="Poll"/> is driven from the
    /// framework's throttled tick to detect connection-count changes.
    /// </summary>
    internal sealed class NetState : IAslNet
    {
        private readonly ManualLogSource _log;
        private readonly NetTransport _transport;
        private int _lastCount = -1;

        public NetState(ManualLogSource log, Harmony harmony)
        {
            _log = log;
            _transport = new NetTransport(log, harmony);
        }

        public bool IsServer => Safe(() => NetworkServer.active);
        public bool IsClient => Safe(() => NetworkClient.active);
        public bool IsHost => IsServer && IsClient;
        public bool IsConnectedClient => Safe(() => NetworkClient.isConnected);
        public bool IsOnline => IsServer || IsClient;

        public int ConnectionCount
        {
            get
            {
                try
                {
                    if (!NetworkServer.active) return 0;
                    var conns = NetworkServer.connections;
                    return conns != null ? conns.Count : 0;
                }
                catch { return 0; }
            }
        }

        public event Action<int> ConnectionsChanged;

        // ---- Message transport (delegated to NetTransport) ----

        public bool MessagingAvailable => _transport.MessagingAvailable;
        public bool Send(string channel, byte[] data) => _transport.Send(channel, data);
        public bool SendToServer(string channel, byte[] data) => _transport.SendToServer(channel, data);
        public bool SendToAll(string channel, byte[] data) => _transport.SendToAll(channel, data);
        public bool SendToClient(int connectionId, string channel, byte[] data) => _transport.SendToClient(connectionId, channel, data);
        public void Subscribe(string channel, Action<AslNetMessage> handler) => _transport.Subscribe(channel, handler);
        public void Unsubscribe(string channel, Action<AslNetMessage> handler) => _transport.Unsubscribe(channel, handler);

        // ---- Typed messages (serialize over the byte transport) ----

        public bool Send<T>(string channel, T message) where T : IAslMessage => Send(channel, Serialize(message));
        public bool SendToServer<T>(string channel, T message) where T : IAslMessage => SendToServer(channel, Serialize(message));
        public bool SendToAll<T>(string channel, T message) where T : IAslMessage => SendToAll(channel, Serialize(message));
        public bool SendToClient<T>(int connectionId, string channel, T message) where T : IAslMessage => SendToClient(connectionId, channel, Serialize(message));

        private static byte[] Serialize<T>(T message) where T : IAslMessage
        {
            if (message == null) return Array.Empty<byte>();
            var w = new AslWriter();
            message.Write(w);
            return w.ToArray();
        }

        // Typed Subscribe wraps the byte handler; we remember each wrapper so Unsubscribe<T> can find it.
        private sealed class TypedSub { public string Channel; public object Handler; public Action<AslNetMessage> Wrapper; }
        private readonly object _typedGate = new object();
        private readonly List<TypedSub> _typedSubs = new List<TypedSub>();

        public void Subscribe<T>(string channel, Action<T, AslNetMessage> handler) where T : IAslMessage, new()
        {
            if (string.IsNullOrEmpty(channel) || handler == null) { _log.LogError("Net.Subscribe<T>: null/empty argument."); return; }
            Action<AslNetMessage> wrapper = raw =>
            {
                T msg = new T();
                try { msg.Read(new AslReader(raw.Data)); }
                catch (Exception ex) { _log.LogError($"Net typed decode for '{channel}' ({typeof(T).Name}) failed: {ex.Message}"); return; }
                handler(msg, raw);
            };
            lock (_typedGate) { _typedSubs.Add(new TypedSub { Channel = channel, Handler = handler, Wrapper = wrapper }); }
            Subscribe(channel, wrapper);
        }

        public void Unsubscribe<T>(string channel, Action<T, AslNetMessage> handler) where T : IAslMessage, new()
        {
            if (string.IsNullOrEmpty(channel) || handler == null) return;
            Action<AslNetMessage> wrapper = null;
            lock (_typedGate)
            {
                for (int i = 0; i < _typedSubs.Count; i++)
                {
                    var s = _typedSubs[i];
                    if (s.Channel == channel && Equals(s.Handler, handler)) { wrapper = s.Wrapper; _typedSubs.RemoveAt(i); break; }
                }
            }
            if (wrapper != null) Unsubscribe(channel, wrapper);
        }

        // ---- Player identity ----

        public event Action<IAslPlayer> PlayerJoined;
        public event Action<IAslPlayer> PlayerLeft;

        private Dictionary<uint, IAslPlayer> _lastPlayers = new Dictionary<uint, IAslPlayer>();
        private string _localNameCache;

        public IReadOnlyList<IAslPlayer> Players => BuildPlayers();

        public IAslPlayer LocalPlayer
        {
            get
            {
                var mp = SafeLocalPlayerInstance();
                return mp != null ? MakePlayer(mp) : null;
            }
        }

        public IAslPlayer GetPlayer(int connectionId)
        {
            if (connectionId < 0) return null;
            var players = BuildPlayers();
            for (int i = 0; i < players.Count; i++)
                if (players[i].ConnectionId == connectionId) return players[i];
            return null;
        }

        // Enumerate the game's own player registry (MetaPlayer.Instances) and wrap each as an IAslPlayer.
        private List<IAslPlayer> BuildPlayers()
        {
            var result = new List<IAslPlayer>();
            try
            {
                var instances = MetaPlayer.Instances;
                if (instances != null)
                {
                    int count = instances.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var mp = instances[i];
                        if (mp != null) result.Add(MakePlayer(mp));
                    }
                }
            }
            catch (Exception ex) { _log.LogError($"Net.Players enumeration failed: {ex.Message}"); }
            return result;
        }

        private IAslPlayer MakePlayer(MetaPlayer mp)
        {
            uint netId = 0; int connId = -1; bool isLocal = false;
            try { netId = mp.netId; } catch { }
            try { isLocal = mp.isLocalPlayer; } catch { }
            try { var c = mp.connectionToClient; if (c != null) connId = c.connectionId; } catch { }
            return new AslPlayer(mp, netId, connId, isLocal, isLocal ? LocalName() : string.Empty);
        }

        private static MetaPlayer SafeLocalPlayerInstance()
        {
            try { return MetaPlayer.LocalPlayerInstance; } catch { return null; }
        }

        // Local player's display name, from Steam (the game's own "GetLocalPlayerName" returns a
        // display-mode enum, not the name). Cached once resolved. Remote players' names need their
        // SteamId, which isn't wired yet — those stay empty for now.
        private string LocalName()
        {
            if (_localNameCache != null) return _localNameCache;
            string name = "";
            try
            {
                var n = Steamworks.SteamFriends.GetPersonaName();
                if (!string.IsNullOrEmpty(n)) name = n;
            }
            catch { /* Steam not ready -> leave empty */ }
            _localNameCache = name;
            return _localNameCache;
        }

        // ---- Synced state ----

        private readonly Dictionary<string, IAslSync> _syncStores = new Dictionary<string, IAslSync>(StringComparer.Ordinal);

        public IAslSync GetSync(string id)
        {
            if (string.IsNullOrEmpty(id)) { _log.LogError("Net.GetSync: id is null/empty."); return null; }
            lock (_syncStores)
            {
                if (!_syncStores.TryGetValue(id, out var store))
                {
                    store = new SyncStore(id, this, _log);
                    _syncStores[id] = store;
                }
                return store;
            }
        }

        // ---- Spawned objects ----

        public UnityEngine.GameObject FindObject(uint netId)
        {
            try
            {
                NetworkIdentity ni = null;
                try { if (NetworkServer.active && NetworkServer.spawned != null && NetworkServer.spawned.ContainsKey(netId)) ni = NetworkServer.spawned[netId]; } catch { }
                if (ni == null) { try { if (NetworkClient.spawned != null && NetworkClient.spawned.ContainsKey(netId)) ni = NetworkClient.spawned[netId]; } catch { } }
                return ni != null ? ni.gameObject : null;
            }
            catch (Exception ex) { _log.LogError($"Net.FindObject failed: {ex.Message}"); return null; }
        }

        public void Poll()
        {
            // Connection-count change -> ConnectionsChanged.
            int n = ConnectionCount;
            if (n != _lastCount)
            {
                _lastCount = n;
                var d = ConnectionsChanged;
                if (d != null)
                    foreach (var h in d.GetInvocationList())
                    {
                        try { ((Action<int>)h)(n); }
                        catch (Exception ex) { _log.LogError($"Net 'ConnectionsChanged' handler threw: {ex.Message}"); }
                    }
            }

            PollPlayers();
        }

        // Detect joins/leaves by diffing the player set (keyed by netId) between polls.
        private void PollPlayers()
        {
            var joined = PlayerJoined;
            var left = PlayerLeft;
            if (joined == null && left == null && _lastPlayers.Count == 0) return;   // nobody cares yet

            var current = new Dictionary<uint, IAslPlayer>();
            foreach (var p in BuildPlayers()) current[p.NetId] = p;

            if (joined != null)
                foreach (var kv in current)
                    if (!_lastPlayers.ContainsKey(kv.Key)) Fire(joined, kv.Value, "PlayerJoined");

            if (left != null)
                foreach (var kv in _lastPlayers)
                    if (!current.ContainsKey(kv.Key)) Fire(left, kv.Value, "PlayerLeft");

            _lastPlayers = current;
        }

        private void Fire(Action<IAslPlayer> evt, IAslPlayer player, string name)
        {
            foreach (var h in evt.GetInvocationList())
            {
                try { ((Action<IAslPlayer>)h)(player); }
                catch (Exception ex) { _log.LogError($"Net '{name}' handler threw: {ex.Message}"); }
            }
        }

        private static bool Safe(Func<bool> f)
        {
            try { return f(); }
            catch { return false; }
        }
    }
}
