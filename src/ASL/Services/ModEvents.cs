using System;
using System.Diagnostics;
using ASL.Api;
using ASL.Core;
using BepInEx.Logging;
using Metater;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslEvents"/> proxy handed out as <c>ctx.Events</c>. It keeps the mod's own handlers
    /// (so they can be dropped on unload / hot reload) and registers ONE frame participant + event sink with the
    /// shared <see cref="EventBus"/>. Consequences:
    /// <list type="bullet">
    /// <item>A mod's Update / LateUpdate handlers run inside that mod's own try/catch and are timed (Stopwatch
    /// ticks, no allocation) for the <see cref="Profiler"/>; one throwing mod never stops the others.</item>
    /// <item>A mod whose Update (or LateUpdate) throws is never switched off for good: after
    /// <see cref="MaxConsecutiveFailures"/> throwing frames in a row it is retried on a backoff schedule
    /// (0.5 s, 1 s, 2 s ... capped at 5 s) with at most one error log per retry, and any success (or a scene /
    /// local-player change) puts it back on every frame - see <see cref="FrameBackoff"/>.</item>
    /// <item>The infrequent events are dispatched per handler with errors attributed to the mod.</item>
    /// <item>After <see cref="Release"/> the proxy is inert: subscriptions are ignored and nothing is invoked.</item>
    /// </list>
    /// </summary>
    internal sealed class ModEvents : IAslEvents, IFrameParticipant, IEventSink
    {
        /// <summary>Consecutive throwing frames after which a mod's Update (or LateUpdate) is only retried on a backoff schedule.</summary>
        public const int MaxConsecutiveFailures = FrameBackoff.FreeFailures;

        private readonly string _modId;
        private readonly EventBus _bus;
        private readonly ManualLogSource _log;
        private readonly ModPerf _perf;
        private bool _attached;
        private bool _released;

        private Action _update, _lateUpdate, _allModsLoaded, _gameQuitting;
        private Action<string> _sceneChanged, _languageChanged;
        private Action<MetaPlayer> _localPlayerChanged;
        private Action<bool> _menuVisibilityChanged;

        private FrameBackoff _updateBackoff, _lateBackoff;

        public ModEvents(string modId, IAslEvents sharedBus, ManualLogSource log = null, ModPerf perf = null)
        {
            _modId = modId;
            _bus = sharedBus as EventBus;
            _log = log ?? AslPlugin.Logger;
            _perf = perf ?? new ModPerf(modId);
        }

        public string ModId => _modId;

        /// <summary>True while Update (or LateUpdate) is being retried on a backoff schedule after repeated exceptions.</summary>
        public bool UpdateDisabled => _updateBackoff.InBackoff;
        public bool LateUpdateDisabled => _lateBackoff.InBackoff;

        /// <summary>Register with the bus now (keeps per-frame order = load order). Otherwise this happens on the first subscription.</summary>
        public void Attach()
        {
            if (_attached || _released || _bus == null) return;
            _attached = true;
            _bus.Register((IFrameParticipant)this);
            _bus.Register((IEventSink)this);
            Profiler.Register(_perf);
        }

        /// <summary>Drop every handler and unhook from the bus. Idempotent.</summary>
        public void Release()
        {
            if (_released) return;
            _released = true;
            _update = null; _lateUpdate = null; _allModsLoaded = null; _gameQuitting = null;
            _sceneChanged = null; _languageChanged = null; _localPlayerChanged = null; _menuVisibilityChanged = null;
            if (_attached)
            {
                _attached = false;
                _bus?.Unregister((IFrameParticipant)this);
                _bus?.Unregister((IEventSink)this);
                Profiler.Unregister(_perf);
            }
        }

        // ---- IAslEvents (what the mod sees) ------------------------------------------------------

        public event Action Update { add { if (_released) return; _update += value; Attach(); } remove { _update -= value; } }
        public event Action<string> SceneChanged { add { if (_released) return; _sceneChanged += value; Attach(); } remove { _sceneChanged -= value; } }
        public event Action<MetaPlayer> LocalPlayerChanged { add { if (_released) return; _localPlayerChanged += value; Attach(); } remove { _localPlayerChanged -= value; } }
        public event Action LateUpdate { add { if (_released) return; _lateUpdate += value; Attach(); } remove { _lateUpdate -= value; } }
        public event Action<bool> MenuVisibilityChanged { add { if (_released) return; _menuVisibilityChanged += value; Attach(); } remove { _menuVisibilityChanged -= value; } }
        public event Action AllModsLoaded { add { if (_released) return; _allModsLoaded += value; Attach(); } remove { _allModsLoaded -= value; } }
        public event Action<string> LanguageChanged { add { if (_released) return; _languageChanged += value; Attach(); } remove { _languageChanged -= value; } }
        public event Action GameQuitting { add { if (_released) return; _gameQuitting += value; Attach(); } remove { _gameQuitting -= value; } }

        // ---- per-frame (hot path: no allocation) ---------------------------------------------------

        public void FrameUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (_released) return;
            var h = _update;
            if (h == null) return;

            long t0 = Stopwatch.GetTimestamp();
            if (!_updateBackoff.ShouldRun(t0)) return;
            try { h(); _updateBackoff.Success(); _perf.UpdateDisabled = false; }
            catch (Exception ex) { OnFrameFailure(ex, "Update", ref _updateBackoff); _perf.UpdateDisabled = _updateBackoff.InBackoff; }
            _perf.UpdateTicks += Stopwatch.GetTimestamp() - t0;
        }

        public void FrameLateUpdate()
        {
            if (_released) return;
            var h = _lateUpdate;
            if (h == null) return;

            long t0 = Stopwatch.GetTimestamp();
            if (!_lateBackoff.ShouldRun(t0)) return;
            try { h(); _lateBackoff.Success(); _perf.LateUpdateDisabled = false; }
            catch (Exception ex) { OnFrameFailure(ex, "LateUpdate", ref _lateBackoff); _perf.LateUpdateDisabled = _lateBackoff.InBackoff; }
            _perf.LateUpdateTicks += Stopwatch.GetTimestamp() - t0;
        }

        // Logs the first failure of a streak, then (once backing off) at most one line per retry window with the
        // number of failures that were not logged. Only the failure path allocates, never the happy path.
        private void OnFrameFailure(Exception ex, string name, ref FrameBackoff backoff)
        {
            if (!backoff.Failure(Stopwatch.GetTimestamp(), Stopwatch.Frequency, out int suppressed)) return;
            string more = suppressed > 0 ? $" (+{suppressed} similar error(s) not logged)" : "";
            if (backoff.InBackoff)
                SafeLog($"'{_modId}': {name} handler keeps throwing ({backoff.Failures} frames in a row); retrying with backoff, up to every {FrameBackoff.MaxDelaySeconds:0.#} s{more}: {ex}");
            else
                SafeLog($"'{_modId}': {name} handler threw (it is retried with backoff if this repeats {MaxConsecutiveFailures} frames in a row){more}: {ex}");
        }

        // ---- infrequent events (IEventSink) ------------------------------------------------------

        // A scene / player change is when "throw until the player exists" mods become healthy again: retry every frame.
        public void OnSceneChanged(string sceneName) { ResetBackoff(); Dispatch(_sceneChanged, sceneName, "SceneChanged"); }
        public void OnLocalPlayerChanged(MetaPlayer player) { ResetBackoff(); Dispatch(_localPlayerChanged, player, "LocalPlayerChanged"); }
        private void ResetBackoff() { _updateBackoff.Reset(); _lateBackoff.Reset(); _perf.UpdateDisabled = false; _perf.LateUpdateDisabled = false; }
        public void OnMenuVisibilityChanged(bool visible) => Dispatch(_menuVisibilityChanged, visible, "MenuVisibilityChanged");
        public void OnAllModsLoaded() => Dispatch(_allModsLoaded, "AllModsLoaded");
        public void OnLanguageChanged(string language) => Dispatch(_languageChanged, language, "LanguageChanged");
        public void OnGameQuitting() => Dispatch(_gameQuitting, "GameQuitting");

        private void Dispatch(Action ev, string name)
        {
            if (_released || ev == null) return;
            foreach (var h in ev.GetInvocationList())
            {
                try { ((Action)h)(); }
                catch (Exception ex) { SafeLog($"'{_modId}': event '{name}' handler threw: {ex.Message}"); }
            }
        }

        private void Dispatch<T>(Action<T> ev, T arg, string name)
        {
            if (_released || ev == null) return;
            foreach (var h in ev.GetInvocationList())
            {
                try { ((Action<T>)h)(arg); }
                catch (Exception ex) { SafeLog($"'{_modId}': event '{name}' handler threw: {ex.Message}"); }
            }
        }

        private void SafeLog(string message)
        {
            try { _log?.LogError(message); } catch { }
        }
    }
}
