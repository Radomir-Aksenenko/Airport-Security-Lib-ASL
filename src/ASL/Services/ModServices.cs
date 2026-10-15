using System;
using System.Collections.Generic;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// A small bag of "undo" actions / disposables a mod's services accumulate, run in reverse order when the mod
    /// unloads or hot-reloads (see <see cref="ModServices.Release"/>). Anything that registers something global on
    /// the mod's behalf (a window, a console command, a hook, a menu row...) should <see cref="Add(Action)"/> its undo here.
    /// </summary>
    internal sealed class ModResources
    {
        private readonly List<IDisposable> _items = new();
        private bool _disposed;

        /// <summary>Number of tracked resources.</summary>
        public int Count => _items.Count;

        /// <summary>Track a disposable. If the owner was already released it is disposed immediately.</summary>
        public void Add(IDisposable d)
        {
            if (d == null) return;
            if (_disposed) { SafeDispose(d); return; }
            _items.Add(d);
        }

        /// <summary>Track an undo action (same semantics as <see cref="Add(IDisposable)"/>).</summary>
        public void Add(Action undo)
        {
            if (undo == null) return;
            Add(new ActionDisposable(undo));
        }

        /// <summary>Stop tracking <paramref name="d"/> without disposing it.</summary>
        public bool Remove(IDisposable d) => _items.Remove(d);

        /// <summary>Dispose everything, newest first. Each item is isolated; returns the number that threw.</summary>
        public int DisposeAll(ManualLogSource log, string modId)
        {
            _disposed = true;
            int failed = 0;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                try { _items[i].Dispose(); }
                catch (Exception ex) { failed++; try { log?.LogError($"'{modId}': releasing a resource threw: {ex.Message}"); } catch { } }
            }
            _items.Clear();
            return failed;
        }

        private static void SafeDispose(IDisposable d) { try { d.Dispose(); } catch { } }

        private sealed class ActionDisposable : IDisposable
        {
            private Action _a;
            public ActionDisposable(Action a) => _a = a;
            public void Dispose() { var a = _a; _a = null; a?.Invoke(); }
        }
    }

    /// <summary>
    /// The bag of per-mod service objects behind <see cref="IModContext"/>, plus the factory that builds
    /// them. <see cref="ModLoader"/> calls <see cref="Create"/> once per mod and hands the result to
    /// <see cref="ModContext"/>, so neither of those has to change when a service implementation is
    /// swapped or extended: subsystem agents edit their own class file and, at most, the one line here.
    /// All constructors must be cheap and side-effect free (they run for every mod at boot).
    ///
    /// <b>Ownership &amp; release.</b> Everything a mod creates through its context is owned by the mod.
    /// <see cref="Release"/> (unload, hot reload, or a failed OnLoad) undoes it, in this order: the
    /// <see cref="Releasing"/> event (the UI layer destroys the mod's windows there), the
    /// <see cref="Resources"/> list, timers, event handlers, cross-mod services/subscriptions, console commands,
    /// and finally <see cref="IDisposable.Dispose"/> on any service that implements it.
    /// </summary>
    internal sealed class ModServices
    {
        private ManualLogSource _log;
        private bool _released;

        public IAslEvents Events { get; private set; }
        public IAslUi Ui { get; private set; }
        public IAslConfig Config { get; private set; }
        public IAslStorage Storage { get; private set; }
        public IAslTimers Timers { get; private set; }
        public IAslMods Mods { get; private set; }
        public IAslCommands Commands { get; private set; }
        public IAslAssets Assets { get; private set; }
        public IAslLocalization Loc { get; private set; }
        public IModInfo Info { get; private set; }

        /// <summary>The mod id these services belong to.</summary>
        public string ModId { get; private set; }

        /// <summary>This mod's profiler record.</summary>
        public ModPerf Perf { get; private set; }

        /// <summary>Undo actions / disposables released with the mod (see <see cref="ModResources"/>).</summary>
        public ModResources Resources { get; } = new();

        /// <summary>True once <see cref="Release"/> has run.</summary>
        public bool IsReleased => _released;

        /// <summary>
        /// Raised first thing in <see cref="Release"/>, while the mod's other services are still intact. Subscribe
        /// to tear down anything the mod created elsewhere (e.g. destroy its UI windows). Handlers are isolated.
        /// </summary>
        public event Action Releasing;

        /// <summary>Create every per-mod service for one mod.</summary>
        /// <param name="info">The mod's registry record (also exposed as <c>ctx.Info</c>).</param>
        /// <param name="displayName">The mod's display name (used in logs/menu).</param>
        /// <param name="modLog">The per-mod BepInEx log source.</param>
        /// <param name="sharedBus">The shared event bus the per-mod <see cref="ModEvents"/> proxies.</param>
        /// <param name="sharedUi">The shared announcement-banner helper.</param>
        public static ModServices Create(ModInfo info, string displayName, ManualLogSource modLog, IAslEvents sharedBus, UiManager sharedUi)
        {
            var id = info.Id;
            var dir = info.Directory;
            var perf = new ModPerf(id);
            var events = new ModEvents(id, sharedBus, modLog, perf);
            var ui = new ModUi(id, displayName, dir, sharedUi, modLog);
            var s = new ModServices
            {
                _log = modLog,
                ModId = id,
                Perf = perf,
                Events = events,
                Ui = ui,
                Config = new ModConfig(id, modLog),
                Storage = new ModStorage(id, modLog),
                Timers = new ModTimers(id, modLog, sharedBus as EventBus, perf),
                Mods = new ModMods(AslPlugin.Registry, id, modLog),
                Commands = new TrackingCommands(new ModCommands(AslPlugin.CommandHub, id)),
                Assets = new ModAssets(id, dir, modLog),
                Loc = new ModLocalization(id, dir, modLog, sharedBus),
                Info = info,
            };
            ui.Attach(s);      // config binds / assets for JSON layouts; ReleaseAll on the Releasing event (windows, HUDs, bindings)
            events.Attach();   // join the per-frame list now so Update order == load order
            return s;
        }

        /// <summary>Run this mod's own <c>AllModsLoaded</c> handlers (both <c>ctx.Events</c> and <c>ctx.Mods</c>); used after a hot reload.</summary>
        internal void RaiseAllModsLoaded()
        {
            if (_released) return;
            try { (Mods as ModMods)?.RaiseLocal(); } catch (Exception ex) { Warn($"Mods.AllModsLoaded raise failed: {ex.Message}"); }
            try { (Events as ModEvents)?.OnAllModsLoaded(); } catch (Exception ex) { Warn($"AllModsLoaded raise failed: {ex.Message}"); }
        }

        /// <summary>
        /// Undo everything this mod created. Idempotent and exception-safe: one failing step never skips the rest.
        /// After this the services are inert (new subscriptions / timers are ignored).
        /// </summary>
        internal void Release()
        {
            if (_released) return;
            _released = true;

            var ev = Releasing;
            if (ev != null)
            {
                foreach (var h in ev.GetInvocationList())
                {
                    try { ((Action)h)(); }
                    catch (Exception ex) { Warn($"release handler threw: {ex.Message}"); }
                }
            }
            Releasing = null;

            Step(() => Resources.DisposeAll(_log, ModId), "resources");
            Step(() => (Timers as ModTimers)?.Release(), "timers");
            Step(() => (Events as ModEvents)?.Release(), "events");
            Step(() => (Mods as ModMods)?.Release(), "mods registry");
            Step(() => (Commands as TrackingCommands)?.Release(), "commands");

            // Services owned by other subsystems: dispose them if they know how.
            DisposeIfAble(Ui, "ui");
            DisposeIfAble(Config, "config");
            DisposeIfAble(Storage, "storage");
            DisposeIfAble(Assets, "assets");
            DisposeIfAble(Loc, "localization");
        }

        private void DisposeIfAble(object service, string what)
        {
            if (service is IDisposable d) Step(d.Dispose, what);
        }

        private void Step(Action a, string what)
        {
            try { a(); }
            catch (Exception ex) { Warn($"releasing {what} threw: {ex.Message}"); }
        }

        private void Warn(string message)
        {
            try { _log?.LogWarning($"'{ModId}': {message}"); } catch { }
        }
    }

    /// <summary>
    /// Wraps a mod's <see cref="IAslCommands"/> so the names it registered can be unregistered on release
    /// (the underlying facade only knows how to unregister by name).
    /// </summary>
    internal sealed class TrackingCommands : IAslCommands
    {
        private readonly IAslCommands _inner;
        private readonly List<string> _names = new();
        private bool _released;

        public TrackingCommands(IAslCommands inner) => _inner = inner;

        public void Register(string name, string description, Action<string[]> handler)
        {
            if (_released) return;
            _inner.Register(name, description, handler);
            Track(name);
        }

        public void Register(string name, string description, Func<string[], string> handler)
        {
            if (_released) return;
            _inner.Register(name, description, handler);
            Track(name);
        }

        public void Unregister(string name)
        {
            _inner.Unregister(name);
            _names.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        }

        public bool Execute(string commandLine) => _inner.Execute(commandLine);
        public void Print(string text) => _inner.Print(text);

        /// <summary>Unregister every command this mod registered. Idempotent.</summary>
        public void Release()
        {
            if (_released) return;
            _released = true;
            foreach (var n in _names.ToArray())
            {
                try { _inner.Unregister(n); } catch { }
            }
            _names.Clear();
        }

        private void Track(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            foreach (var n in _names) if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return;
            _names.Add(name);
        }
    }
}
