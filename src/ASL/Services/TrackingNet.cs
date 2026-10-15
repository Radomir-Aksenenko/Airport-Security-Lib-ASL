using System;
using System.Collections.Generic;
using ASL.Api;

namespace ASL
{
    /// <summary>
    /// Per-mod wrapper around the shared <see cref="IAslNet"/> that forwards everything unchanged and remembers every
    /// subscription the mod made (channel handlers, byte and typed; <c>ConnectionsChanged</c> / <c>PlayerJoined</c> /
    /// <c>PlayerLeft</c>; and <c>Changed</c> on the <see cref="IAslSync"/> stores it got from <c>GetSync</c>) so a
    /// mod unload / hot reload can undo them (otherwise the old instance's handlers would keep firing next to the new
    /// instance's). After <see cref="Release"/> new subscriptions are ignored, like the other per-mod services.
    /// </summary>
    internal sealed class TrackingNet : IAslNet
    {
        private sealed class Entry
        {
            public object Key;      // channel for subscriptions / the event name
            public object Handler;  // the delegate the mod passed
            public Action Undo;
        }

        private readonly IAslNet _inner;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<string, TrackingSync> _syncs = new Dictionary<string, TrackingSync>(StringComparer.Ordinal);
        private bool _released;

        public TrackingNet(IAslNet inner) { _inner = inner; }

        internal bool IsReleased => _released;
        internal int TrackedCount => _entries.Count;

        // ---- tracking helpers ----------------------------------------------------------------

        internal void Track(object key, object handler, Action undo)
        {
            _entries.Add(new Entry { Key = key, Handler = handler, Undo = undo });
        }

        internal void Untrack(object key, object handler)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (ReferenceEquals(e.Handler, handler) && Equals(e.Key, key)) { _entries.RemoveAt(i); return; }
            }
        }

        /// <summary>Undo every subscription this mod made. Idempotent; one failing undo never skips the rest.</summary>
        public void Release()
        {
            if (_released) return;
            _released = true;
            var list = _entries.ToArray();
            _entries.Clear();
            for (int i = 0; i < list.Length; i++)
            {
                try { list[i].Undo?.Invoke(); } catch { }
            }
            _syncs.Clear();
        }

        // ---- state ---------------------------------------------------------------------------

        public bool IsOnline => _inner.IsOnline;
        public bool IsServer => _inner.IsServer;
        public bool IsClient => _inner.IsClient;
        public bool IsHost => _inner.IsHost;
        public bool IsConnectedClient => _inner.IsConnectedClient;
        public int ConnectionCount => _inner.ConnectionCount;

        public event Action<int> ConnectionsChanged
        {
            add
            {
                if (_released || value == null) return;
                _inner.ConnectionsChanged += value;
                Track("ConnectionsChanged", value, () => _inner.ConnectionsChanged -= value);
            }
            remove { _inner.ConnectionsChanged -= value; Untrack("ConnectionsChanged", value); }
        }

        // ---- transport -----------------------------------------------------------------------

        public bool MessagingAvailable => _inner.MessagingAvailable;
        public bool Send(string channel, byte[] data) => _inner.Send(channel, data);
        public bool SendToServer(string channel, byte[] data) => _inner.SendToServer(channel, data);
        public bool SendToAll(string channel, byte[] data) => _inner.SendToAll(channel, data);
        public bool SendToClient(int connectionId, string channel, byte[] data) => _inner.SendToClient(connectionId, channel, data);

        public void Subscribe(string channel, Action<AslNetMessage> handler)
        {
            if (_released) return;
            _inner.Subscribe(channel, handler);
            if (channel != null && handler != null) Track(channel, handler, () => _inner.Unsubscribe(channel, handler));
        }

        public void Unsubscribe(string channel, Action<AslNetMessage> handler)
        {
            _inner.Unsubscribe(channel, handler);
            if (channel != null) Untrack(channel, handler);
        }

        public bool Send<T>(string channel, T message) where T : IAslMessage => _inner.Send(channel, message);
        public bool SendToServer<T>(string channel, T message) where T : IAslMessage => _inner.SendToServer(channel, message);
        public bool SendToAll<T>(string channel, T message) where T : IAslMessage => _inner.SendToAll(channel, message);
        public bool SendToClient<T>(int connectionId, string channel, T message) where T : IAslMessage => _inner.SendToClient(connectionId, channel, message);

        public void Subscribe<T>(string channel, Action<T, AslNetMessage> handler) where T : IAslMessage, new()
        {
            if (_released) return;
            _inner.Subscribe<T>(channel, handler);
            if (!string.IsNullOrEmpty(channel) && handler != null) Track(channel, handler, () => _inner.Unsubscribe<T>(channel, handler));
        }

        public void Unsubscribe<T>(string channel, Action<T, AslNetMessage> handler) where T : IAslMessage, new()
        {
            _inner.Unsubscribe<T>(channel, handler);
            if (channel != null) Untrack(channel, handler);
        }

        // ---- players -------------------------------------------------------------------------

        public IReadOnlyList<IAslPlayer> Players => _inner.Players;
        public IAslPlayer LocalPlayer => _inner.LocalPlayer;
        public IAslPlayer GetPlayer(int connectionId) => _inner.GetPlayer(connectionId);

        public event Action<IAslPlayer> PlayerJoined
        {
            add
            {
                if (_released || value == null) return;
                _inner.PlayerJoined += value;
                Track("PlayerJoined", value, () => _inner.PlayerJoined -= value);
            }
            remove { _inner.PlayerJoined -= value; Untrack("PlayerJoined", value); }
        }

        public event Action<IAslPlayer> PlayerLeft
        {
            add
            {
                if (_released || value == null) return;
                _inner.PlayerLeft += value;
                Track("PlayerLeft", value, () => _inner.PlayerLeft -= value);
            }
            remove { _inner.PlayerLeft -= value; Untrack("PlayerLeft", value); }
        }

        // ---- synced state / objects ----------------------------------------------------------

        public IAslSync GetSync(string id)
        {
            var inner = _inner.GetSync(id);
            if (inner == null) return null;
            if (id != null && _syncs.TryGetValue(id, out var cached) && ReferenceEquals(cached.Inner, inner)) return cached;
            var w = new TrackingSync(this, inner);
            if (id != null) _syncs[id] = w;   // same wrapper for the same store, like the shared net returns the same store
            return w;
        }

        public UnityEngine.GameObject FindObject(uint netId) => _inner.FindObject(netId);
    }

    /// <summary>Forwarding <see cref="IAslSync"/> whose <c>Changed</c> handlers are undone with the owning mod.</summary>
    internal sealed class TrackingSync : IAslSync
    {
        private readonly TrackingNet _owner;
        internal readonly IAslSync Inner;

        public TrackingSync(TrackingNet owner, IAslSync inner) { _owner = owner; Inner = inner; }

        public string Id => Inner.Id;
        public bool Set(string key, string value) => Inner.Set(key, value);
        public string Get(string key) => Inner.Get(key);
        public bool TryGet(string key, out string value) => Inner.TryGet(key, out value);
        public bool Contains(string key) => Inner.Contains(key);
        public IReadOnlyDictionary<string, string> All => Inner.All;

        public event Action<string, string> Changed
        {
            add
            {
                if (_owner.IsReleased || value == null) return;
                Inner.Changed += value;
                var inner = Inner;
                _owner.Track(inner, value, () => inner.Changed -= value);
            }
            remove { Inner.Changed -= value; _owner.Untrack(Inner, value); }
        }
    }
}
