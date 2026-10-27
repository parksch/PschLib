using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PschLib.Scheduling;
using PschLib.Unity.Scheduling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PschLib.Tests.UnityRuntime
{
    [Category("Scheduling")]
    public sealed class TimerSchedulerRunnerTests
    {
        private GameObject root;
        private TimerSchedulerRunner runner;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            root = new GameObject("TimerSchedulerRunner test");
            runner = root.AddComponent<TimerSchedulerRunner>();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        private static IEnumerator WaitForCompletion(TimerHandle timer)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (!timer.IsFinished && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(timer.IsCompleted, Is.True, "Timer did not complete within five real-time seconds.");
        }

        [UnityTest]
        public IEnumerator Update_ZeroDurationTimer_CompletesOnlyOnce()
        {
            var calls = 0;
            var timer = runner.Schedule(0f, () => calls++);
            yield return WaitForCompletion(timer);
            yield return null;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(runner.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Update_WhenTimeScaleIsZero_OnlyUnscaledTimerAdvances()
        {
            Time.timeScale = 0f;
            yield return null; // Let Unity calculate the next frame's scaled delta.
            var scaled = runner.Schedule(0.05f);
            var unscaled = runner.Schedule(0.05f, timeMode: TimerTimeMode.Unscaled);
            yield return WaitForCompletion(unscaled);

            Assert.That(scaled.IsRunning, Is.True);
            Assert.That(scaled.ElapsedTime, Is.Zero);
            runner.Clear();
        }

        [UnityTest]
        public IEnumerator Clear_CancelsTimerAndSuppressesCallback()
        {
            var calls = 0;
            var timer = runner.Schedule(0f, () => calls++);
            runner.Clear();
            yield return null;
            yield return null;

            Assert.That(timer.IsCancelled, Is.True);
            Assert.That(calls, Is.Zero);
            Assert.That(runner.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DestroyRunner_CancelsOutstandingTimer()
        {
            var timer = runner.Schedule(60f);
            Object.Destroy(root);
            yield return null;
            Assert.That(timer.IsCancelled, Is.True);
        }

        [UnityTest]
        public IEnumerator CallbackThrows_OtherTimersCompleteAndExceptionIsLogged()
        {
            LogAssert.Expect(LogType.Exception, new Regex("Expected timer callback failure"));
            var failing = runner.Schedule(0f, () => throw new InvalidOperationException("Expected timer callback failure"));
            var other = runner.Schedule(0f);
            yield return WaitForCompletion(other);
            Assert.That(failing.IsCompleted, Is.True);
            Assert.That(runner.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RepeatingTimer_CompletesAfterRequestedCount()
        {
            var calls = 0;
            var timer = runner.ScheduleRepeating(0.01f, () => calls++, repeatCount: 3);
            yield return WaitForCompletion(timer);
            Assert.That(calls, Is.EqualTo(3));
            Assert.That(timer.CompletedCount, Is.EqualTo(3));
            Assert.That(runner.Count, Is.Zero);
        }
    }
}
