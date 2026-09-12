using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace PschLib.Scheduling
{
    public sealed class TimerScheduler
#if UNITY_EDITOR
        : ITimerSchedulerDebugInfo
#endif
    {
        private readonly List<TimerEntry> entries = new List<TimerEntry>();
        private readonly List<TimerEntry> pendingEntries = new List<TimerEntry>();

        private bool isTicking;
        private bool clearRequested;

#if UNITY_EDITOR
        public event Action DebugStateChanged;
#endif

        public int Count => clearRequested ? pendingEntries.Count : entries.Count + pendingEntries.Count;

        public TimerHandle Schedule(float duration, Action callback = null, TimerTimeMode timeMode = TimerTimeMode.Scaled, bool startPaused = false, bool repeat = false, int repeatCount = 0)
        {
            var entry = new TimerEntry(duration, timeMode, callback, startPaused, repeat, repeatCount);

            if (isTicking)
            {
                pendingEntries.Add(entry);
            }
            else
            {
                entries.Add(entry);
            }

            NotifyDebugStateChanged();
            return entry.Handle;
        }

        public void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            ValidateDeltaTime(scaledDeltaTime, nameof(scaledDeltaTime));
            ValidateDeltaTime(unscaledDeltaTime, nameof(unscaledDeltaTime));

            if (isTicking)
            {
                throw new InvalidOperationException("TimerScheduler.Tick cannot be called recursively.");
            }

            isTicking = true;
            List<Exception> failures = null;

            try
            {
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    if (clearRequested)
                    {
                        break;
                    }

                    var entry = entries[i];

                    try
                    {
                        if (entry.Tick(scaledDeltaTime, unscaledDeltaTime))
                        {
                            entries.RemoveAt(i);
                        }
                    }
                    catch (Exception exception)
                    {
                        if (entry.Handle.IsFinished)
                        {
                            entries.RemoveAt(i);
                        }

                        if (failures == null)
                        {
                            failures = new List<Exception>();
                        }

                        failures.Add(exception);
                    }
                }
            }
            finally
            {
                isTicking = false;

                if (clearRequested)
                {
                    entries.Clear();
                    clearRequested = false;
                }

                if (pendingEntries.Count > 0)
                {
                    entries.AddRange(pendingEntries);
                    pendingEntries.Clear();
                }

                NotifyDebugStateChanged();
            }

            if (failures != null)
            {
                if (failures.Count == 1)
                {
                    ExceptionDispatchInfo.Capture(failures[0]).Throw();
                }

                throw new AggregateException("Timer callbacks failed.", failures);
            }
        }

        public void Clear()
        {
            CancelEntries(entries);
            CancelEntries(pendingEntries);
            pendingEntries.Clear();

            if (isTicking)
            {
                clearRequested = true;
                return;
            }

            entries.Clear();
            NotifyDebugStateChanged();
        }

        private static void ValidateDeltaTime(float deltaTime, string parameterName)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Delta time must be finite and non-negative.");
            }
        }

        private static void CancelEntries(List<TimerEntry> targetEntries)
        {
            for (int i = 0; i < targetEntries.Count; i++)
            {
                targetEntries[i].Handle.Cancel();
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void NotifyDebugStateChanged()
        {
#if UNITY_EDITOR
            DebugStateChanged?.Invoke();
#endif
        }

#if UNITY_EDITOR
        int ITimerSchedulerDebugInfo.TimerCount => Count;
        int ITimerSchedulerDebugInfo.PendingTimerCount => pendingEntries.Count;
        bool ITimerSchedulerDebugInfo.IsTicking => isTicking;

        void ITimerSchedulerDebugInfo.GetTimerEntries(List<TimerDebugEntry> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (!clearRequested)
            {
                AddDebugEntries(entries, results, false);
            }

            AddDebugEntries(pendingEntries, results, true);
        }

        private static void AddDebugEntries(
            List<TimerEntry> source,
            List<TimerDebugEntry> results,
            bool isPending)
        {
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                results.Add(new TimerDebugEntry(entry.Handle, entry.TimeMode, isPending));
            }
        }
#endif
    }
}
