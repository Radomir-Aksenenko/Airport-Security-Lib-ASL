using System;
using System.Collections;

namespace ASL.Api
{
    /// <summary>
    /// Scheduling helpers, reached through <c>ctx.Timers</c>. Everything runs on the main thread, in
    /// frame order, and is cancelled automatically when your mod unloads. Exceptions in your callbacks
    /// are logged with your mod name and never break the game.
    /// <code>
    /// ctx.Timers.After(3f, () => ctx.Ui.Toast("3 seconds later"));
    /// ctx.Timers.Every(1f, Tick);
    /// ctx.Timers.Start(Routine());
    /// IEnumerator Routine() { yield return Wait.Seconds(2f); ctx.Log.Info("done"); yield return null; }
    /// </code>
    /// </summary>
    public interface IAslTimers
    {
        /// <summary>Run <paramref name="action"/> once after <paramref name="seconds"/>. <paramref name="unscaled"/> ignores <c>Time.timeScale</c>.</summary>
        IAslTimer After(float seconds, Action action, bool unscaled = true);

        /// <summary>Run <paramref name="action"/> every <paramref name="seconds"/> until cancelled.</summary>
        IAslTimer Every(float seconds, Action action, bool unscaled = true);

        /// <summary>Run <paramref name="action"/> on the next frame.</summary>
        IAslTimer NextFrame(Action action);

        /// <summary>Poll <paramref name="condition"/> each frame; run <paramref name="action"/> once it returns true. <paramref name="timeoutSeconds"/> &gt; 0 gives up after that long.</summary>
        IAslTimer When(Func<bool> condition, Action action, float timeoutSeconds = 0f);

        /// <summary>
        /// Run a coroutine-lite. Inside, <c>yield return null</c> waits one frame, <c>yield return 1.5f</c>
        /// (float or int) waits that many seconds, and <c>yield return Wait.Seconds/Until/While/Frames(...)</c> waits accordingly.
        /// </summary>
        IAslTimer Start(IEnumerator routine);

        /// <summary>Cancel every timer and routine this mod started.</summary>
        void CancelAll();
    }

    /// <summary>A handle to a scheduled timer or running routine.</summary>
    public interface IAslTimer
    {
        /// <summary>True until the timer has fired for the last time or been cancelled.</summary>
        bool IsActive { get; }

        /// <summary>Stop the timer. Safe to call more than once.</summary>
        void Cancel();
    }

    /// <summary>
    /// A wait instruction yielded from a routine started with <see cref="IAslTimers.Start"/>. Create them
    /// with <see cref="Wait"/>. ASL calls <see cref="Tick"/> once per frame until it returns true.
    /// </summary>
    public interface IAslWait
    {
        /// <summary>Advance by one frame of <paramref name="deltaSeconds"/>; return true when the wait is over.</summary>
        bool Tick(float deltaSeconds);
    }

    /// <summary>Factory for <see cref="IAslWait"/> instructions to <c>yield return</c> from routines.</summary>
    public static class Wait
    {
        /// <summary>Wait for <paramref name="seconds"/>.</summary>
        public static IAslWait Seconds(float seconds) => new SecondsWait(seconds);

        /// <summary>Wait until <paramref name="condition"/> returns true.</summary>
        public static IAslWait Until(Func<bool> condition) => new UntilWait(condition, true);

        /// <summary>Wait while <paramref name="condition"/> returns true.</summary>
        public static IAslWait While(Func<bool> condition) => new UntilWait(condition, false);

        /// <summary>Wait for <paramref name="frames"/> frames.</summary>
        public static IAslWait Frames(int frames) => new FramesWait(frames);

        private sealed class SecondsWait : IAslWait
        {
            private float _left;
            public SecondsWait(float seconds) => _left = seconds;
            public bool Tick(float deltaSeconds) { _left -= deltaSeconds; return _left <= 0f; }
        }

        private sealed class UntilWait : IAslWait
        {
            private readonly Func<bool> _cond;
            private readonly bool _until;
            public UntilWait(Func<bool> cond, bool until) { _cond = cond; _until = until; }
            public bool Tick(float deltaSeconds) => _cond == null || _cond() == _until;
        }

        private sealed class FramesWait : IAslWait
        {
            private int _left;
            public FramesWait(int frames) => _left = frames;
            public bool Tick(float deltaSeconds) => --_left <= 0;
        }
    }
}
