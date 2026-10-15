using System;
using System.Diagnostics;
using ASL.Api;
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
    /// <item>A mod whose Update (or LateUpdate) throws on <see cref="MaxConsecutiveFailures"/> frames in a row is
    /// switched off for the rest of the session (one error log), so it cannot flood the log or the frame.</item>
    /// <item>The infrequent events are dispatched per handler with errors attributed to the mod.</item>
    /// <item>After <see cref="Release"/> the proxy is inert: subscriptions are ignored and nothing is invoked.</item>
    /// </list>
    /// </summary>
    internal sealed class ModEvents : IAslEvents, IFrameParticipant, IEventSink
    {
        /// <summary>Consecutive throwing frames after which a mod's Update (or LateUpdate) is disabled.</summary>
        public const int MaxConsecutiveFailures = 10;

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

        private int _updateFails, _lateFails;
        private bool _updateDisabled, _lateDisabled;
        private long _lastFailLogTicks;

        public ModEvents(string modId, IAslEvents sharedBus, ManualLogSource log = null, ModPerf perf = null)
        {
            _modId = modId;
            _bus = sharedBus as EventBus;
            _log = log ?? AslPlugin.Logger;
            _perf = perf ?? new ModPerf(modId);
        }

        public string ModId => _modId;

        /// <summary>True once Update (or LateUpdate) has been switched off after repeated exceptions.</summary>
        public bool UpdateDisabled => _updateDisabled;
        public bool LateUpdateDisabled => _lateDisabled;

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
            if (_released || _updateDisabled) return;
            var h = _update;
            if (h == null) return;

            long t0 = Stopwatch.GetTimestamp();
            try { h(); _updateFails = 0; }
            catch (Exception ex) { OnFrameFailure(ex, "Update", ref _updateFails, ref _updateDisabled); if (_updateDisabled) _perf.UpdateDisabled = true; }
            _perf.UpdateTicks += Stopwatch.GetTimestamp() - t0;
        }

        public void FrameLateUpdate()
        {
            if (_released || _lateDisabled) return;
            var h = _lateUpdate;
            if (h == null) return;

            long t0 = Stopwatch.GetTimestamp();
            try { h(); _lateFails = 0; }
            catch (Exception ex) { OnFrameFailure(ex, "LateUpdate", ref _lateFails, ref _lateDisabled); if (_lateDisabled) _perf.LateUpdateDisabled = true; }
            _perf.LateUpdateTicks += Stopwatch.GetTimestamp() - t0;
        }

        // Logs the first failure of a streak (at most about once a second overall) and one final line when the event is switched off.
        private void OnFrameFailure(Exception ex, string name, ref int fails, ref bool disabled)
        {
            fails++;
            long now = Stopwatch.GetTimestamp();
            if (fails >= MaxConsecutiveFailures)
            {
                disabled = true;
                SafeLog($"'{_modId}': {name} handlers threw on {fails} consecutive frames - disabling them for this session. Last error: {ex}");
            }
            else if (fails == 1 && (_lastFailLogTicks == 0 || now - _lastFailLogTicks > Stopwatch.Frequency))
            {
                _lastFailLogTicks = now;
                SafeLog($"'{_modId}': {name} handler threw (the mod is disabled if this repeats {MaxConsecutiveFailures} frames in a row): {ex}");
            }
        }

        // ---- infrequent events (IEventSink) ------------------------------------------------------

        public void OnSceneChanged(string sceneName) => Dispatch(_sceneChanged, sceneName, "SceneChanged");
        public void OnLocalPlayerChanged(MetaPlayer player) => Dispatch(_localPlayerChanged, player, "LocalPlayerChanged");
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
