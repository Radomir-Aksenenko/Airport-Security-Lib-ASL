using System;
using System.Collections;
using System.Collections.Generic;
using ASL.Api;
using ASL.Core;
using Xunit;

namespace ASL.Tests
{
    public class SchedulerTests
    {
        private readonly List<string> _errors = new();
        private readonly Scheduler _s;

        public SchedulerTests() => _s = new Scheduler((ex, what) => _errors.Add(what + ": " + ex.Message));

        private void Frames(int n, float dt = 0.1f, float scaled = -1f)
        {
            for (int i = 0; i < n; i++) _s.Tick(scaled < 0 ? dt : scaled, dt);
        }

        [Fact]
        public void After_FiresOnceAfterDelay()
        {
            int n = 0;
            var t = _s.After(1f, () => n++);
            Frames(9);
            Assert.Equal(0, n);
            Assert.True(t.IsActive);
            Frames(2);
            Assert.Equal(1, n);
            Assert.False(t.IsActive);
            Frames(30);
            Assert.Equal(1, n);
            Assert.Equal(0, _s.ActiveCount);
        }

        [Fact]
        public void After_RespectsScaledVsUnscaled()
        {
            int unscaled = 0, scaled = 0;
            _s.After(1f, () => unscaled++, unscaled: true);
            _s.After(1f, () => scaled++, unscaled: false);
            Frames(10, dt: 0.1f, scaled: 0f);   // game paused: unscaled advances, scaled does not
            Assert.Equal(1, unscaled);
            Assert.Equal(0, scaled);
            Frames(10, dt: 0.1f, scaled: 0.1f);
            Assert.Equal(1, scaled);
        }

        [Fact]
        public void Every_RepeatsUntilCancelled()
        {
            int n = 0;
            var t = _s.Every(0.5f, () => n++);
            Frames(8, 0.25f);   // 2.0 s
            Assert.Equal(4, n);
            t.Cancel();
            Frames(8, 0.25f);
            Assert.Equal(4, n);
            Assert.False(t.IsActive);
        }

        [Fact]
        public void Every_FiresAtMostOncePerTick()
        {
            int n = 0;
            _s.Every(0.1f, () => n++);
            _s.Tick(5f, 5f);   // one huge frame
            Assert.Equal(1, n);
        }

        [Fact]
        public void NextFrame_RunsOnFirstTickOnly()
        {
            int n = 0;
            _s.NextFrame(() => n++);
            Assert.Equal(0, n);
            Frames(1);
            Assert.Equal(1, n);
            Frames(3);
            Assert.Equal(1, n);
        }

        [Fact]
        public void When_FiresOnceConditionHolds()
        {
            bool flag = false;
            int n = 0;
            _s.When(() => flag, () => n++);
            Frames(5);
            Assert.Equal(0, n);
            flag = true;
            Frames(1);
            Assert.Equal(1, n);
            Frames(5);
            Assert.Equal(1, n);
        }

        [Fact]
        public void When_TimesOutSilently()
        {
            int n = 0;
            var t = _s.When(() => false, () => n++, timeoutSeconds: 0.5f);
            Frames(4);
            Assert.True(t.IsActive);
            Frames(2);
            Assert.False(t.IsActive);
            Assert.Equal(0, n);
            Assert.Empty(_errors);
        }

        [Fact]
        public void Routine_YieldNull_ResumesNextFrame()
        {
            var log = new List<int>();
            IEnumerator R() { log.Add(1); yield return null; log.Add(2); yield return null; log.Add(3); }
            var t = _s.Start(R());
            Assert.Empty(log);        // first step is deferred to the next tick
            Frames(1); Assert.Equal(new[] { 1 }, log);
            Frames(1); Assert.Equal(new[] { 1, 2 }, log);
            Frames(1); Assert.Equal(new[] { 1, 2, 3 }, log);
            Assert.False(t.IsActive);
        }

        [Fact]
        public void Routine_YieldFloatAndInt_WaitSeconds()
        {
            var log = new List<string>();
            IEnumerator R() { log.Add("a"); yield return 0.5f; log.Add("b"); yield return 1; log.Add("c"); }
            _s.Start(R());
            Frames(1, 0.25f);
            Assert.Equal(new[] { "a" }, log);
            Frames(1, 0.25f); Assert.Equal(new[] { "a" }, log);
            Frames(1, 0.25f); Assert.Equal(new[] { "a", "b" }, log);
            Frames(3, 0.25f); Assert.Equal(new[] { "a", "b" }, log);
            Frames(1, 0.25f); Assert.Equal(new[] { "a", "b", "c" }, log);
        }

        [Fact]
        public void Routine_AslWait_SecondsUntilWhileFrames()
        {
            bool gate = false, running = true;
            var log = new List<string>();
            IEnumerator R()
            {
                yield return Wait.Seconds(0.5f); log.Add("sec");
                yield return Wait.Until(() => gate); log.Add("until");
                yield return Wait.While(() => running); log.Add("while");
                yield return Wait.Frames(3); log.Add("frames");
            }
            var t = _s.Start(R());
            Frames(1, 0.25f);               // starts, yields Seconds(0.5)
            Frames(1, 0.25f); Assert.Empty(log);
            Frames(1, 0.25f); Assert.Equal(new[] { "sec" }, log);
            Frames(3, 0.25f); Assert.Single(log);
            gate = true; Frames(1, 0.25f); Assert.Equal(2, log.Count);
            Frames(2, 0.25f); Assert.Equal(2, log.Count);
            running = false; Frames(1, 0.25f); Assert.Equal(3, log.Count);
            Frames(2, 0.25f); Assert.Equal(3, log.Count);
            Frames(1, 0.25f); Assert.Equal(4, log.Count);
            Assert.False(t.IsActive);
        }

        [Fact]
        public void Routine_UnknownYieldBehavesLikeNull()
        {
            int steps = 0;
            IEnumerator R() { steps++; yield return "huh"; steps++; }
            _s.Start(R());
            Frames(1); Assert.Equal(1, steps);
            Frames(1); Assert.Equal(2, steps);
        }

        [Fact]
        public void Cancel_RoutineStopsIt()
        {
            int steps = 0;
            IEnumerator R() { while (true) { steps++; yield return null; } }
            var t = _s.Start(R());
            Frames(3);
            Assert.Equal(3, steps);
            t.Cancel();
            Frames(3);
            Assert.Equal(3, steps);
        }

        [Fact]
        public void Cancel_IsIdempotent_AndCountsStayCorrect()
        {
            var t = _s.After(1f, () => { });
            Assert.Equal(1, _s.ActiveCount);
            t.Cancel(); t.Cancel();
            Assert.Equal(0, _s.ActiveCount);
            _s.CancelAll();
            Assert.Equal(0, _s.ActiveCount);
        }

        [Fact]
        public void CancelAll_StopsEverything()
        {
            int n = 0;
            _s.After(0.1f, () => n++); _s.Every(0.1f, () => n++); _s.NextFrame(() => n++); _s.When(() => true, () => n++);
            Assert.Equal(4, _s.ActiveCount);
            _s.CancelAll();
            Frames(5);
            Assert.Equal(0, n);
        }

        [Fact]
        public void Callback_CanAddTimer_ItStartsNextTick()
        {
            int outer = 0, inner = 0;
            _s.NextFrame(() => { outer++; _s.NextFrame(() => inner++); });
            Frames(1);
            Assert.Equal(1, outer);
            Assert.Equal(0, inner);   // added during the tick: not processed in it
            Frames(1);
            Assert.Equal(1, inner);
        }

        [Fact]
        public void Callback_CanCancelOtherTimer_InSameTick()
        {
            int second = 0;
            IAslTimer victim = null;
            _s.NextFrame(() => victim.Cancel());
            victim = _s.NextFrame(() => second++);
            Frames(1);
            Assert.Equal(0, second);
            Assert.False(victim.IsActive);
        }

        [Fact]
        public void Callback_CanCancelItself()
        {
            int n = 0;
            IAslTimer t = null;
            t = _s.Every(0.1f, () => { n++; t.Cancel(); });
            Frames(5);
            Assert.Equal(1, n);
        }

        [Fact]
        public void Callback_CanCancelAll()
        {
            int a = 0, b = 0;
            _s.NextFrame(() => { a++; _s.CancelAll(); });
            _s.NextFrame(() => b++);
            Frames(1);
            Assert.Equal(1, a);
            Assert.Equal(0, b);
            Assert.Equal(0, _s.ActiveCount);
            _s.NextFrame(() => b++);   // scheduler is still usable afterwards
            Frames(1);
            Assert.Equal(1, b);
        }

        [Fact]
        public void Callback_ReentrantTick_IsIgnored()
        {
            int n = 0;
            _s.Every(0.1f, () => { n++; _s.Tick(1f, 1f); });
            Frames(1);
            Assert.Equal(1, n);
        }

        [Fact]
        public void Exception_IsReported_CancelsOnlyThatTimer()
        {
            int ok = 0;
            var bad = _s.Every(0.1f, () => throw new InvalidOperationException("boom"));
            _s.Every(0.1f, () => ok++);
            Frames(5);
            Assert.False(bad.IsActive);
            Assert.Single(_errors);
            Assert.Contains("boom", _errors[0]);
            Assert.Equal(5, ok);
        }

        [Fact]
        public void Exception_InConditionAndRoutine_IsReported()
        {
            _s.When(() => throw new Exception("cond"), () => { });
            IEnumerator R() { yield return null; throw new Exception("routine"); }
            _s.Start(R());
            Frames(4);
            Assert.Equal(2, _errors.Count);
            Assert.Equal(0, _s.ActiveCount);
        }

        [Fact]
        public void ErrorSink_Throwing_DoesNotBreakScheduler()
        {
            var s = new Scheduler((ex, w) => throw new Exception("sink"));
            int n = 0;
            s.NextFrame(() => throw new Exception("x"));
            s.NextFrame(() => n++);
            s.Tick(0.1f, 0.1f);
            Assert.Equal(1, n);
        }

        [Fact]
        public void NullArguments_GiveInertHandles()
        {
            Assert.False(_s.After(1f, null).IsActive);
            Assert.False(_s.Every(1f, null).IsActive);
            Assert.False(_s.NextFrame(null).IsActive);
            Assert.False(_s.When(null, () => { }).IsActive);
            Assert.False(_s.Start(null).IsActive);
            Assert.Equal(0, _s.ActiveCount);
        }

        [Fact]
        public void NegativeAndNaNDeltas_AreSafe()
        {
            int n = 0;
            _s.After(0.1f, () => n++);
            _s.Tick(-1f, -1f);
            _s.Tick(float.NaN, float.NaN);
            Assert.Equal(0, n);
            _s.Tick(0.2f, 0.2f);
            Assert.Equal(1, n);
        }

        [Fact]
        public void IdleTick_DoesNotAllocate()
        {
            _s.Tick(0.016f, 0.016f);   // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _s.Tick(0.016f, 0.016f);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0, after - before);
        }

        [Fact]
        public void BusyTick_WithRepeatingTimer_DoesNotAllocate()
        {
            int n = 0;
            _s.Every(10f, () => n++);
            _s.Tick(0.016f, 0.016f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _s.Tick(0.016f, 0.016f);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0, after - before);
        }
    }
}
