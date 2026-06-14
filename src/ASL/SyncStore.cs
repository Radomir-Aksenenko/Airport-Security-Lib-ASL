using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Implements <see cref="IAslSync"/> over the byte transport. Host-authoritative: the host applies
    /// and broadcasts changes; clients apply what they receive. A joining client is sent the full state
    /// as a snapshot. Wire format on the store's private channel: <c>[op][...]</c> where op 1 = Set
    /// (key, value) and op 2 = Snapshot (count, then count×(key, value)).
    /// </summary>
    internal sealed class SyncStore : IAslSync
    {
        private const byte OpSet = 1;
        private const byte OpSnapshot = 2;

        private readonly IAslNet _net;
        private readonly ManualLogSource _log;
        private readonly string _channel;
        private readonly object _gate = new object();
        private readonly Dictionary<string, string> _data = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _clientSetWarned;

        public string Id { get; }
        public event Action<string, string> Changed;

        public SyncStore(string id, IAslNet net, ManualLogSource log)
        {
            Id = id;
            _net = net;
            _log = log;
            _channel = "asl/sync/" + id;
            _net.Subscribe(_channel, OnMessage);          // installs the transport if needed
            _net.PlayerJoined += OnPlayerJoined;          // host: snapshot late joiners
        }

        public bool Set(string key, string value)
        {
            if (key == null) { _log.LogError($"AslSync '{Id}': Set key is null."); return false; }
            value = value ?? string.Empty;

            if (!_net.IsServer)
            {
                if (!_clientSetWarned) { _clientSetWarned = true; _log.LogWarning($"AslSync '{Id}': Set is host-only; ignored on a client."); }
                return false;
            }

            ApplyLocal(key, value);                       // host is authoritative — apply immediately

            var w = new AslWriter();
            w.WriteByte(OpSet);
            w.WriteString(key);
            w.WriteString(value);
            return _net.SendToAll(_channel, w.ToArray()); // replicate to clients
        }

        public string Get(string key)
        {
            lock (_gate) { return key != null && _data.TryGetValue(key, out var v) ? v : null; }
        }

        public bool TryGet(string key, out string value)
        {
            lock (_gate)
            {
                if (key != null && _data.TryGetValue(key, out value)) return true;
                value = null;
                return false;
            }
        }

        public bool Contains(string key)
        {
            lock (_gate) { return key != null && _data.ContainsKey(key); }
        }

        public IReadOnlyDictionary<string, string> All
        {
            get { lock (_gate) { return new Dictionary<string, string>(_data); } }
        }

        private void ApplyLocal(string key, string value)
        {
            bool changed;
            lock (_gate)
            {
                changed = !_data.TryGetValue(key, out var old) || !string.Equals(old, value, StringComparison.Ordinal);
                _data[key] = value;
            }
            if (changed) FireChanged(key, value);
        }

        private void OnMessage(AslNetMessage msg)
        {
            if (_net.IsServer) return;   // host already authoritative; ignore any echo of its own traffic
            try
            {
                var r = new AslReader(msg.Data);
                byte op = r.ReadByte();
                if (op == OpSet)
                {
                    ApplyLocal(r.ReadString(), r.ReadString());
                }
                else if (op == OpSnapshot)
                {
                    int n = r.ReadInt();
                    for (int i = 0; i < n; i++) ApplyLocal(r.ReadString(), r.ReadString());
                }
            }
            catch (Exception ex) { _log.LogError($"AslSync '{Id}': bad message: {ex.Message}"); }
        }

        private void OnPlayerJoined(IAslPlayer p)
        {
            if (p == null || p.IsLocal || !_net.IsServer || p.ConnectionId < 0) return;

            Dictionary<string, string> snap;
            lock (_gate) { snap = new Dictionary<string, string>(_data); }
            if (snap.Count == 0) return;

            var w = new AslWriter();
            w.WriteByte(OpSnapshot);
            w.WriteInt(snap.Count);
            foreach (var kv in snap) { w.WriteString(kv.Key); w.WriteString(kv.Value); }
            _net.SendToClient(p.ConnectionId, _channel, w.ToArray());
        }

        private void FireChanged(string key, string value)
        {
            var d = Changed;
            if (d == null) return;
            foreach (var h in d.GetInvocationList())
            {
                try { ((Action<string, string>)h)(key, value); }
                catch (Exception ex) { _log.LogError($"AslSync '{Id}' Changed handler threw: {ex.Message}"); }
            }
        }
    }
}
