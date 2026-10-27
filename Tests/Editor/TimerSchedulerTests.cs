using System;
using NUnit.Framework;
using PschLib.Scheduling;

namespace PschLib.Tests
{
    public sealed class TimerSchedulerTests
    {
        [Test]
        public void InvalidDurationOrDelta_IsRejectedWithoutAdvancingTimers()
        {
            var scheduler = new TimerScheduler();
            foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(invalid));
            var timer = scheduler.Schedule(1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Tick(0.5f, float.NaN));
            Assert.That(timer.ElapsedTime, Is.Zero);
            Assert.That(scheduler.Count, Is.EqualTo(1));
        }

        [Test]
        public void TimeModes_UseTheirOwnClockAndCompleteOnlyOnce()
        {
            var scheduler = new TimerScheduler();
            var calls = 0;
            var scaled = scheduler.Schedule(1f, () => calls++);
            var unscaled = scheduler.Schedule(1f, () => calls++, TimerTimeMode.Unscaled);
            scheduler.Tick(0f, 2f);
            Assert.That(scaled.ElapsedTime, Is.Zero);
            Assert.That(unscaled.IsCompleted, Is.True);
            scheduler.Tick(2f, 2f);
            scheduler.Tick(2f, 2f);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(scaled.Progress, Is.EqualTo(1f));
            Assert.That(scheduler.Count, Is.Zero);
        }

        [Test]
        public void PausedZeroDurationTimer_CompletesOnFirstTickAfterResume()
        {
            var scheduler = new TimerScheduler();
            var calls = 0;
            var timer = scheduler.Schedule(0f, () => calls++, startPaused: true);
            scheduler.Tick(1f, 1f);
            Assert.That(calls, Is.Zero);
            Assert.That(timer.Resume(), Is.True);
            scheduler.Tick(0f, 0f);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.Cancel(), Is.False);
            Assert.That(timer.Pause(), Is.False);
        }

        [Test]
        public void Cancel_SuppressesCallbackAndIsIdempotent()
        {
            var scheduler = new TimerScheduler();
            var timer = scheduler.Schedule(0f, () => Assert.Fail("Cancelled timer ran."));
            Assert.That(timer.Cancel(), Is.True);
            Assert.That(timer.Cancel(), Is.False);
            scheduler.Tick(1f, 1f);
            Assert.That(scheduler.Count, Is.Zero);
        }

        [Test]
        public void ScheduleDuringCallback_DoesNotConsumeCurrentTick()
        {
            var scheduler = new TimerScheduler();
            TimerHandle added = null;
            scheduler.Schedule(0f, () => added = scheduler.Schedule(1f));
            scheduler.Tick(10f, 10f);
            Assert.That(added.ElapsedTime, Is.Zero);
            scheduler.Tick(1f, 1f);
            Assert.That(added.IsCompleted, Is.True);
        }

        [Test]
        public void ClearDuringCallback_CancelsOldAndPendingTimersButPreservesLaterSchedule()
        {
            var scheduler = new TimerScheduler();
            var old = scheduler.Schedule(10f);
            TimerHandle pending = null;
            TimerHandle afterClear = null;
            scheduler.Schedule(0f, () =>
            {
                pending = scheduler.Schedule(10f);
                scheduler.Clear();
                afterClear = scheduler.Schedule(1f);
            });
            scheduler.Tick(1f, 1f);
            Assert.That(old.IsCancelled, Is.True);
            Assert.That(pending.IsCancelled, Is.True);
            Assert.That(afterClear.ElapsedTime, Is.Zero);
            Assert.That(scheduler.Count, Is.EqualTo(1));
            scheduler.Tick(1f, 1f);
            Assert.That(afterClear.IsCompleted, Is.True);
        }

        [Test]
        public void RecursiveTick_IsRejectedAndSchedulerRecovers()
        {
            var scheduler = new TimerScheduler();
            scheduler.Schedule(0f, () => scheduler.Tick(0f, 0f));
            Assert.Throws<InvalidOperationException>(() => scheduler.Tick(1f, 1f));
            var next = scheduler.Schedule(0f);
            scheduler.Tick(0f, 0f);
            Assert.That(next.IsCompleted, Is.True);
            Assert.That(scheduler.Count, Is.Zero);
        }

        [Test]
        public void MultipleCallbacksThrow_ReportsAllFailuresAfterProcessingOtherTimers()
        {
            var scheduler = new TimerScheduler();
            var first = new InvalidOperationException("First");
            var second = new ArgumentException("Second");
            var other = scheduler.Schedule(0f);
            scheduler.Schedule(0f, () => throw first);
            scheduler.Schedule(0f, () => throw second);
            var error = Assert.Throws<AggregateException>(() => scheduler.Tick(1f, 1f));
            Assert.That(error.InnerExceptions, Is.EquivalentTo(new Exception[] { first, second }));
            Assert.That(other.IsCompleted, Is.True);
            Assert.That(scheduler.Count, Is.Zero);
        }

        [Test]
        public void CallbackClearsThenThrows_CancelsOldTimersAndCommitsNewTimer()
        {
            var scheduler = new TimerScheduler();
            var old = scheduler.Schedule(2f, () => Assert.Fail("Cleared timer ran."));
            var expected = new InvalidOperationException("Expected");
            TimerHandle added = null;
            scheduler.Schedule(0f, () =>
            {
                scheduler.Clear();
                added = scheduler.Schedule(0f);
                throw expected;
            });
            Assert.That(Assert.Throws<InvalidOperationException>(() => scheduler.Tick(1f, 1f)), Is.SameAs(expected));
            Assert.That(old.IsCancelled, Is.True);
            Assert.That(added.IsCompleted, Is.False);
            Assert.That(scheduler.Count, Is.EqualTo(1));
            scheduler.Tick(0f, 0f);
            Assert.That(added.IsCompleted, Is.True);
        }

        [Test]
        public void CallbackThrows_AdvancesRemainingTimersBeforeRethrowing()
        {
            var scheduler = new TimerScheduler();
            var other = scheduler.Schedule(2f);
            var failing = scheduler.Schedule(0f, () => throw new InvalidOperationException("Expected"));
            Assert.Throws<InvalidOperationException>(() => scheduler.Tick(1f, 1f));
            Assert.That(failing.IsCompleted, Is.True);
            Assert.That(other.ElapsedTime, Is.EqualTo(1f));
            scheduler.Tick(1f, 1f);
            Assert.That(other.IsCompleted, Is.True);
        }

        [Test]
        public void RepeatingTimer_RunsEveryDurationAndPreservesRemainder()
        {
            var scheduler = new TimerScheduler();
            var calls = 0;
            var timer = scheduler.ScheduleRepeating(1f, () => calls++, overflowMode: TimerOverflowMode.Preserve);

            scheduler.Tick(2.5f, 2.5f);

            Assert.That(calls, Is.EqualTo(2));
            Assert.That(timer.IsRepeating, Is.True);
            Assert.That(timer.IsRunning, Is.True);
            Assert.That(timer.ElapsedTime, Is.EqualTo(0.5f));
            Assert.That(timer.Progress, Is.EqualTo(0.5f));
            Assert.That(scheduler.Count, Is.EqualTo(1));
        }

        [Test]
        public void RepeatingTimer_CanCancelItselfFromCallback()
        {
            var scheduler = new TimerScheduler();
            var calls = 0;
            TimerHandle timer = null;
            timer = scheduler.ScheduleRepeating(1f, () =>
            {
                calls++;
                timer.Cancel();
            }, overflowMode: TimerOverflowMode.Preserve);

            scheduler.Tick(5f, 5f);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.IsCancelled, Is.True);
            Assert.That(scheduler.Count, Is.Zero);
        }

        [Test]
        public void RepeatingTimer_CanPauseAndResume()
        {
            var scheduler = new TimerScheduler();
            var calls = 0;
            TimerHandle timer = null;
            timer = scheduler.ScheduleRepeating(1f, () =>
            {
                calls++;
                timer.Pause();
            }, overflowMode: TimerOverflowMode.Preserve);

            scheduler.Tick(3f, 3f);
            scheduler.Tick(3f, 3f);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(timer.ElapsedTime, Is.EqualTo(2f));

            timer.Resume();
            scheduler.Tick(0f, 0f);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(timer.IsPaused, Is.True);
        }

        [Test]
        public void ZeroDurationRepeatingTimer_IsRejected()
        {
            var scheduler = new TimerScheduler();
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleRepeating(0f, overflowMode: TimerOverflowMode.Preserve));
        }

        [Test]
        public void RepeatingTimer_WithCount_CompletesAfterRequestedExecutions()
        {
            var scheduler = new TimerScheduler();
            var calls = 0;
            var timer = scheduler.ScheduleRepeating(1f, () => calls++, overflowMode: TimerOverflowMode.Preserve, repeatCount: 3);

            scheduler.Tick(10f, 10f);

            Assert.That(calls, Is.EqualTo(3));
            Assert.That(timer.RepeatCount, Is.EqualTo(3));
            Assert.That(timer.CompletedCount, Is.EqualTo(3));
            Assert.That(timer.IsCompleted, Is.True);
            Assert.That(timer.Progress, Is.EqualTo(1f));
            Assert.That(scheduler.Count, Is.Zero);
        }

        [Test]
        public void RepeatingTimer_ZeroCount_RemainsInfiniteUntilCancelled()
        {
            var scheduler = new TimerScheduler();
            var calls = 0;
            var timer = scheduler.ScheduleRepeating(1f, () => calls++, overflowMode: TimerOverflowMode.Preserve);

            scheduler.Tick(3f, 3f);

            Assert.That(calls, Is.EqualTo(3));
            Assert.That(timer.RepeatCount, Is.Zero);
            Assert.That(timer.CompletedCount, Is.EqualTo(3));
            Assert.That(timer.IsRunning, Is.True);
        }

        [Test]
        public void NegativeRepeatCount_IsRejected()
        {
            var scheduler = new TimerScheduler();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                scheduler.ScheduleRepeating(1f, overflowMode: TimerOverflowMode.Preserve, repeatCount: -1));
        }
    }
}
