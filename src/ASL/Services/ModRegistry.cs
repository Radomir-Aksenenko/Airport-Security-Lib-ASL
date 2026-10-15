using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Mutable <see cref="IModInfo"/> record the loader keeps for every mod folder it saw (loaded, failed,
    /// content, ...). Mods only ever see it through the read-only interface.
    /// </summary>
    internal sealed class ModInfo : IModInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string Kind { get; set; }
        public string Directory { get; set; }
        public ModState State { get; set; } = ModState.Failed;
        public string Error { get; set; }
        public IReadOnlyList<string> Dependencies { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

        /// <summary>The parsed manifest (null for folders whose manifest could not be read).</summary>
        public ModManifest Manifest { get; set; }

        /// <summary>Build a record from a manifest. State starts as <see cref="ModState.Failed"/> until the loader marks it loaded.</summary>
        public static ModInfo From(ModManifest m, string kind, string directory, string displayName)
        {
            return new ModInfo
            {
                Id = m.Id,
                Name = displayName,
                Version = m.Version,
                Author = m.Author,
                Description = m.Description,
                Kind = kind,
                Directory = directory,
                Dependencies = m.Dependencies ?? Array.Empty<string>(),
                Tags = m.Tags ?? Array.Empty<string>(),
                Manifest = m,
                Error = "Did not finish loading.",
            };
        }
    }

    /// <summary>
    /// Shared registry of every mod ASL found plus the cross-mod service/message registry.
    /// The Core agent owns this file (ordering, dependency states, per-mod resource release).
    /// </summary>
    internal sealed class ModRegistry
    {
        private readonly ManualLogSource _log;
        private readonly List<ModInfo> _mods = new();
        private readonly List<IModInfo> _view = new();
        private readonly List<(string owner, Type type, object service)> _services = new();
        private readonly Dictionary<string, List<Subscription>> _topics = new(StringComparer.Ordinal);

        public ModRegistry(ManualLogSource log) => _log = log;

        public IReadOnlyList<IModInfo> All => _view;
        public IReadOnlyList<ModInfo> Mods => _mods;

        public event Action AllModsLoaded;

        public ModInfo Add(ModInfo info)
        {
            _mods.Add(info);
            _view.Add(info);
            return info;
        }

        public ModInfo Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var m in _mods) if (string.Equals(m.Id, id, StringComparison.Ordinal)) return m;
            return null;
        }

        public bool IsLoaded(string id) => Find(id)?.State == ModState.Loaded;

        public void RaiseAllModsLoaded()
        {
            var ev = AllModsLoaded;
            if (ev == null) return;
            foreach (var h in ev.GetInvocationList())
            {
                try { ((Action)h)(); }
                catch (Exception ex) { _log.LogError($"Mods.AllModsLoaded handler threw: {ex.Message}"); }
            }
        }

        // ---- services / messages ----------------------------------------------------------

        /// <summary>Forget everything <paramref name="owner"/> published: its provided services and its topic subscriptions (unload / hot reload).</summary>
        public void RemoveOwner(string owner)
        {
            if (owner == null) return;
            _services.RemoveAll(s => s.owner == owner);
            foreach (var list in _topics.Values) list.RemoveAll(s => s.Owner == owner);
        }

        public void Provide(string owner, Type type, object service)
        {
            if (service == null) return;
            _services.Add((owner, type, service));
        }

        public object GetService(Type type, string fromModId)
        {
            foreach (var s in _services)
            {
                if (fromModId != null && !string.Equals(s.owner, fromModId, StringComparison.Ordinal)) continue;
                if (s.type == type) return s.service;
            }
            return null;
        }

        public void Publish(string topic, object payload)
        {
            if (topic == null || !_topics.TryGetValue(topic, out var list)) return;
            foreach (var sub in list.ToArray())
            {
                if (!list.Contains(sub)) continue;   // unsubscribed (or its mod released) by an earlier handler
                try { sub.Handler(payload); }
                catch (Exception ex) { _log.LogError($"Mods.Publish('{topic}') handler of '{sub.Owner}' threw: {ex.Message}"); }
            }
        }

        public IDisposable Subscribe(string owner, string topic, Action<object> handler)
        {
            if (topic == null || handler == null) return new Subscription(null, null, null);
            if (!_topics.TryGetValue(topic, out var list)) _topics[topic] = list = new List<Subscription>();
            var sub = new Subscription(owner, handler, list);
            list.Add(sub);
            return sub;
        }

        private sealed class Subscription : IDisposable
        {
            public readonly string Owner;
            public readonly Action<object> Handler;
            private readonly List<Subscription> _list;

            public Subscription(string owner, Action<object> handler, List<Subscription> list)
            {
                Owner = owner; Handler = handler; _list = list;
            }

            public void Dispose() { _list?.Remove(this); }
        }
    }

    /// <summary>
    /// Per-mod <see cref="IAslMods"/> facade over the shared <see cref="ModRegistry"/> (remembers the owning mod id).
    /// Services this mod provides and topics it subscribes to are owned by it and dropped by <see cref="Release"/>;
    /// its <c>AllModsLoaded</c> handlers are kept per mod so they can be released and re-raised after a hot reload.
    /// </summary>
    internal sealed class ModMods : IAslMods
    {
        private readonly ModRegistry _reg;
        private readonly string _modId;
        private readonly ManualLogSource _log;
        private Action _allLoaded;
        private bool _forwarding;
        private bool _released;

        public ModMods(ModRegistry reg, string modId, ManualLogSource log = null)
        {
            _reg = reg;
            _modId = modId;
            _log = log ?? AslPlugin.Logger;
        }

        public IReadOnlyList<IModInfo> All => _reg.All;
        public bool IsLoaded(string modId) => _reg.IsLoaded(modId);
        public IModInfo Get(string modId) => _reg.Find(modId);
        public void Provide<T>(T service) where T : class { if (!_released) _reg.Provide(_modId, typeof(T), service); }
        public T GetService<T>(string fromModId = null) where T : class => _reg.GetService(typeof(T), fromModId) as T;
        public void Publish(string topic, object payload = null) => _reg.Publish(topic, payload);
        public IDisposable Subscribe(string topic, Action<object> handler) => _released ? new NoSub() : _reg.Subscribe(_modId, topic, handler);

        public event Action AllModsLoaded
        {
            add
            {
                if (_released) return;
                _allLoaded += value;
                if (!_forwarding) { _forwarding = true; _reg.AllModsLoaded += RaiseLocal; }
            }
            remove { _allLoaded -= value; }
        }

        /// <summary>Invoke this mod's own AllModsLoaded handlers (isolated). Also used after a hot reload.</summary>
        public void RaiseLocal()
        {
            var ev = _allLoaded;
            if (_released || ev == null) return;
            foreach (var h in ev.GetInvocationList())
            {
                try { ((Action)h)(); }
                catch (Exception ex) { _log?.LogError($"'{_modId}': Mods.AllModsLoaded handler threw: {ex.Message}"); }
            }
        }

        /// <summary>Drop this mod's services, subscriptions and handlers. Idempotent.</summary>
        public void Release()
        {
            if (_released) return;
            _released = true;
            _allLoaded = null;
            if (_forwarding) { _forwarding = false; _reg.AllModsLoaded -= RaiseLocal; }
            _reg.RemoveOwner(_modId);
        }

        private sealed class NoSub : IDisposable { public void Dispose() { } }
    }
}
