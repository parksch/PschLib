using System;
using PschLib.Scheduling;
using UnityEngine;

namespace PschLib.Unity.Scheduling
{
    [DisallowMultipleComponent]
    public sealed class TimerSchedulerRunner : MonoBehaviour
    {
        private readonly TimerScheduler scheduler = new TimerScheduler();

        public int Count => scheduler.Count;

        public TimerHandle Schedule(float duration, Action callback = null, TimerTimeMode timeMode = TimerTimeMode.Scaled, bool startPaused = false)
        {
            return scheduler.Schedule(duration, callback, timeMode, startPaused);
        }

        public TimerHandle ScheduleRepeating(float interval, Action callback = null, int repeatCount = 0,
            TimerTimeMode timeMode = TimerTimeMode.Scaled, TimerOverflowMode overflowMode = TimerOverflowMode.Discard,
            bool startPaused = false)
        {
            return scheduler.ScheduleRepeating(interval, callback, repeatCount, timeMode, overflowMode, startPaused);
        }

        public void Clear()
        {
            scheduler.Clear();
        }

        private void Update()
        {
            try
            {
                scheduler.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            scheduler.Clear();
        }
    }
}
