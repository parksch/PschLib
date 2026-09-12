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

        public TimerHandle Schedule(float duration, Action callback = null, TimerTimeMode timeMode = TimerTimeMode.Scaled, bool startPaused = false, bool repeat = false, int repeatCount = 0)
        {
            return scheduler.Schedule(duration, callback, timeMode, startPaused, repeat, repeatCount);
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
