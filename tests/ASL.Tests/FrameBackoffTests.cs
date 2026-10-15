using ASL.Core;
using Xunit;

namespace ASL.Tests
{
    public class FrameBackoffTests
    {
        private const long Hz = 1000;   // 1 tick = 1 ms

        [Fact]
        public void RunsEveryFrame_ForTheFirstFailures()
        {
            var b = new FrameBackoff();
            for (int i = 0; i < FrameBackoff.FreeFailures - 1; i++)
            {
                Assert.True(b.ShouldRun(i));
                b.Failure(i, Hz, out _);
                Assert.False(b.InBackoff);
            }
            Assert.True(b.ShouldRun(100));
        }

        [Fact]
        public void LogsFirstFailure_ThenCountsSuppressed()
        {
            var b = new FrameBackoff();
            Assert.True(b.Failure(0, Hz, out int s0)); Assert.Equal(0, s0);
            for (int i = 2; i < FrameBackoff.FreeFailures; i++) Assert.False(b.Failure(i, Hz, out _));
            Assert.True(b.Failure(10, Hz, out int s)); Assert.Equal(FrameBackoff.FreeFailures - 2, s);   // failures 2..9
        }

        [Fact]
        public void BacksOff_Exponentially_CappedAtFiveSeconds_NeverPermanent()
        {
            var b = new FrameBackoff();
            long now = 0;
            for (int i = 0; i < FrameBackoff.FreeFailures; i++) b.Failure(now, Hz, out _);
            Assert.True(b.InBackoff);

            long[] expectedDelays = { 500, 1000, 2000, 4000, 5000, 5000, 5000 };
            // The 10th failure scheduled the first retry 0.5 s out.
            foreach (var delay in expectedDelays)
            {
                Assert.False(b.ShouldRun(now + delay - 1));
                Assert.True(b.ShouldRun(now + delay));
                now += delay;
                b.Failure(now, Hz, out _);   // the retry fails again -> next window
            }
        }

        [Fact]
        public void EveryRetryIsLogged_WithNothingSuppressedWhileBackingOff()
        {
            var b = new FrameBackoff();
            for (int i = 0; i < FrameBackoff.FreeFailures; i++) b.Failure(i, Hz, out _);
            Assert.True(b.Failure(1000, Hz, out int s));
            Assert.Equal(0, s);
        }

        [Fact]
        public void Success_ResetsToEveryFrame()
        {
            var b = new FrameBackoff();
            for (int i = 0; i < 25; i++) b.Failure(i, Hz, out _);
            Assert.False(b.ShouldRun(26));
            b.Success();
            Assert.True(b.ShouldRun(26));
            Assert.False(b.InBackoff);
            Assert.True(b.Failure(30, Hz, out _));   // a new streak logs its first failure again
        }

        [Fact]
        public void Reset_ReturnsToEveryFrame()
        {
            var b = new FrameBackoff();
            for (int i = 0; i < 40; i++) b.Failure(i, Hz, out _);
            b.Reset();
            Assert.True(b.ShouldRun(41));
        }
    }
}
