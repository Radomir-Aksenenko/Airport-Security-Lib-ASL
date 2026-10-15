using System;
using System.Collections;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslTimers"/>. Owned by the Core agent (pure scheduler ticked from the bus).
    /// Placeholder: the constructor must stay side-effect free; <see cref="CancelAll"/> is a safe no-op
    /// so unload paths never throw.
    /// </summary>
    internal sealed class ModTimers : IAslTimers
    {
        private readonly string _modId;
        private readonly ManualLogSource _log;

        public ModTimers(string modId, ManualLogSource log)
        {
            _modId = modId;
            _log = log;
        }

        public IAslTimer After(float seconds, Action action, bool unscaled = true) => throw NotYet("Timers.After");
        public IAslTimer Every(float seconds, Action action, bool unscaled = true) => throw NotYet("Timers.Every");
        public IAslTimer NextFrame(Action action) => throw NotYet("Timers.NextFrame");
        public IAslTimer When(Func<bool> condition, Action action, float timeoutSeconds = 0f) => throw NotYet("Timers.When");
        public IAslTimer Start(IEnumerator routine) => throw NotYet("Timers.Start");
        public void CancelAll() { }

        private static NotImplementedException NotYet(string what) => new NotImplementedException($"ASL 2.0: {what} not implemented yet");
    }
}
