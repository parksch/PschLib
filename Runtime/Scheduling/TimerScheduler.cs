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
        private bool isNotifyingDebugStateChanged;
        private Action debugStateChanged;
        private Delegate[] debugListenerSnapshot = Array.Empty<Delegate>();

        public event Action DebugStateChanged
        {
            add
            {
                debugStateChanged += value;
                debugListenerSnapshot = debugStateChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            }
            remove
            {
                debugStateChanged -= value;
                debugListenerSnapshot = debugStateChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
            }
        }
#endif

        public int Count => clearRequested
            ? CountActive(pendingEntries)
            : CountActive(entries) + CountActive(pendingEntries);

        public TimerHandle Schedule(float duration, Action callback = null, TimerTimeMode timeMode = TimerTimeMode.Scaled, bool startPaused = false)
        {
            return ScheduleInternal(duration, callback, timeMode, startPaused, false, 0, TimerOverflowMode.Discard);
        }

        public TimerHandle ScheduleRepeating(float interval, Action callback = null, int repeatCount = 0,
            TimerTimeMode timeMode = TimerTimeMode.Scaled, TimerOverflowMode overflowMode = TimerOverflowMode.Discard,
            bool startPaused = false)
        {
            return ScheduleInternal(interval, callback, timeMode, startPaused, true, repeatCount, overflowMode);
        }

        private TimerHandle ScheduleInternal(float duration, Action callback, TimerTimeMode timeMode, bool startPaused,
            bool repeat, int repeatCount, TimerOverflowMode overflowMode)
        {
            var entry = new TimerEntry(duration, timeMode, callback, startPaused, repeat, repeatCount, overflowMode);

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
                for (var i = 0; i < entries.Count; i++)
                {
                    if (clearRequested)
                    {
                        break;
                    }

                    var entry = entries[i];

                    try
                    {
                        entry.Tick(scaledDeltaTime, unscaledDeltaTime);
                    }
                    catch (Exception exception)
                    {
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
                else
                {
                    RemoveFinished(entries);
                }

                RemoveFinished(pendingEntries);

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

        private static void RemoveFinished(List<TimerEntry> targetEntries)
        {
            var activeCount = 0;

            for (var i = 0; i < targetEntries.Count; i++)
            {
                var entry = targetEntries[i];

                if (entry.Handle.IsFinished)
                {
                    continue;
                }

                if (activeCount != i)
                {
                    targetEntries[activeCount] = entry;
                }

                activeCount++;
            }

            if (activeCount < targetEntries.Count)
            {
                targetEntries.RemoveRange(activeCount, targetEntries.Count - activeCount);
            }
        }

        private static int CountActive(List<TimerEntry> targetEntries)
        {
            var count = 0;

            for (var i = 0; i < targetEntries.Count; i++)
            {
                if (!targetEntries[i].Handle.IsFinished)
                {
                    count++;
                }
            }

            return count;
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
            var listeners = debugListenerSnapshot;
            if (listeners.Length == 0 || isNotifyingDebugStateChanged)
            {
                return;
            }

            isNotifyingDebugStateChanged = true;

            try
            {
                for (var i = 0; i < listeners.Length; i++)
                {
                    try
                    {
                        ((Action)listeners[i])();
                    }
                    catch (Exception exception)
                    {
                        PschLib.Debugging.DebugObserverExceptionReporter.Report(nameof(TimerScheduler), exception);
                    }
                }
            }
            finally
            {
                isNotifyingDebugStateChanged = false;
            }
#endif
        }

#if UNITY_EDITOR
        int ITimerSchedulerDebugInfo.TimerCount => Count;
        int ITimerSchedulerDebugInfo.PendingTimerCount => CountActive(pendingEntries);
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

                if (entry.Handle.IsFinished)
                {
                    continue;
                }

                results.Add(new TimerDebugEntry(entry.Handle, entry.TimeMode, entry.OverflowMode, isPending));
            }
        }
#endif
    }
}
