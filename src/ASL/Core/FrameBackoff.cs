using System;

namespace ASL.Core
{
    /// <summary>
    /// Failure throttle for a per-frame mod callback (pure logic, allocation-free, time passed in as ticks).
    /// A throwing handler keeps running every frame for the first <see cref="FreeFailures"/> consecutive failures
    /// (exactly like before ASL 2.0, so a mod that throws until the player exists still recovers). After that it
    /// is only retried on an exponential schedule (0.5 s, 1 s, 2 s ... capped at 5 s). Any success resets it to
    /// every-frame. It is never permanently disabled.
    /// </summary>
    public struct FrameBackoff
    {
        /// <summary>Consecutive failures that still run every frame.</summary>
        public const int FreeFailures = 10;
        /// <summary>First retry delay in seconds.</summary>
        public const double MinDelaySeconds = 0.5;
        /// <summary>Longest retry delay in seconds.</summary>
        public const double MaxDelaySeconds = 5.0;

        private int _fails;
        private int _suppressed;
        private long _nextRunTicks;

        /// <summary>Consecutive failures so far.</summary>
        public int Failures => _fails;

        /// <summary>True while the callback is being retried on a schedule instead of every frame.</summary>
        public bool InBackoff => _fails >= FreeFailures;

        /// <summary>Should the callback be invoked now?</summary>
        public bool ShouldRun(long nowTicks) => _fails < FreeFailures || nowTicks >= _nextRunTicks;

        /// <summary>The callback succeeded: back to every frame.</summary>
        public void Success() { _fails = 0; _suppressed = 0; _nextRunTicks = 0; }

        /// <summary>Forget the failure history (scene / local-player change).</summary>
        public void Reset() => Success();

        /// <summary>
        /// The callback threw. Returns true if this failure should be logged; <paramref name="suppressed"/> is then
        /// the number of earlier failures that were not logged since the last logged one.
        /// The first failure of a streak is logged; failures 2..9 are counted; from the 10th on every retry
        /// (at most one per backoff window) is logged with the count.
        /// </summary>
        public bool Failure(long nowTicks, long ticksPerSecond, out int suppressed)
        {
            if (_fails < int.MaxValue) _fails++;
            suppressed = 0;
            if (_fails >= FreeFailures)
            {
                int step = _fails - FreeFailures;                       // 0 -> 0.5 s, 1 -> 1 s, 2 -> 2 s, ...
                double delay = step >= 4 ? MaxDelaySeconds : Math.Min(MaxDelaySeconds, MinDelaySeconds * (1 << step));
                _nextRunTicks = nowTicks + (long)(delay * ticksPerSecond);
            }
            if (_fails == 1 || _fails >= FreeFailures)
            {
                suppressed = _suppressed;
                _suppressed = 0;
                return true;
            }
            _suppressed++;
            return false;
        }
    }
}
