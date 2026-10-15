using System;
using System.Collections;
using System.Diagnostics;
using ASL.Api;
using ASL.Core;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslTimers"/>: one pure <see cref="Scheduler"/> per mod, ticked once per frame from the
    /// event bus (scaled = <c>Time.deltaTime</c>, unscaled = <c>Time.unscaledDeltaTime</c>). The scheduler is only
    /// registered with the bus once the mod creates its first timer, so mods that never use timers cost nothing.
    /// Callback exceptions are logged with the mod id and cancel just that timer. <see cref="Release"/> (unload /
    /// hot reload) cancels everything and turns the facade inert.
    /// </summary>
    internal sealed class ModTimers : IAslTimers, IFrameParticipant
    {
        private readonly string _modId;
        private readonly ManualLogSource _log;
        private readonly EventBus _bus;
        private readonly ModPerf _perf;
        private readonly Scheduler _sched;
        private bool _attached;
        private bool _released;

        public ModTimers(string modId, ManualLogSource log, EventBus bus = null, ModPerf perf = null)
        {
            _modId = modId;
            _log = log ?? AslPlugin.Logger;
            _bus = bus;
            _perf = perf;
            _sched = new Scheduler(OnCallbackError);
            if (_perf != null) _perf.ActiveTimers = () => _sched.ActiveCount;
        }

        /// <summary>Timers and routines currently scheduled by this mod.</summary>
        public int ActiveCount => _sched.ActiveCount;

        public IAslTimer After(float seconds, Action action, bool unscaled = true) => Prepare() ? _sched.After(seconds, action, unscaled) : Inert;
        public IAslTimer Every(float seconds, Action action, bool unscaled = true) => Prepare() ? _sched.Every(seconds, action, unscaled) : Inert;
        public IAslTimer NextFrame(Action action) => Prepare() ? _sched.NextFrame(action) : Inert;
        public IAslTimer When(Func<bool> condition, Action action, float timeoutSeconds = 0f) => Prepare() ? _sched.When(condition, action, timeoutSeconds) : Inert;
        public IAslTimer Start(IEnumerator routine) => Prepare() ? _sched.Start(routine) : Inert;

        public void CancelAll() => _sched.CancelAll();

        /// <summary>Cancel everything, unhook from the bus and ignore further requests. Idempotent.</summary>
        public void Release()
        {
            if (_released) return;
            _released = true;
            _sched.CancelAll();
            if (_attached) { _attached = false; _bus?.Unregister((IFrameParticipant)this); }
        }

        // False after Release (callers then hand back an inert handle); registers with the bus on first use.
        private bool Prepare()
        {
            if (_released) return false;
            if (!_attached && _bus != null)
            {
                _attached = true;
                _bus.Register((IFrameParticipant)this);
            }
            return true;
        }

        public void FrameUpdate(float deltaTime, float unscaledDeltaTime)
        {
            if (_released || _sched.ActiveCount == 0) return;
            long t0 = Stopwatch.GetTimestamp();
            _sched.Tick(deltaTime, unscaledDeltaTime);
            if (_perf != null) _perf.TimerTicks += Stopwatch.GetTimestamp() - t0;
        }

        public void FrameLateUpdate() { }

        private void OnCallbackError(Exception ex, string what)
        {
            try { _log?.LogError($"'{_modId}': timer {what} threw (timer cancelled): {ex}"); } catch { }
        }

        private static readonly IAslTimer Inert = new InertTimer();

        private sealed class InertTimer : IAslTimer
        {
            public bool IsActive => false;
            public void Cancel() { }
        }
    }
}
