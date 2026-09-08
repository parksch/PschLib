#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace PschLib.Scheduling
{
    public interface ITimerSchedulerDebugInfo
    {
        event Action DebugStateChanged;
        int TimerCount { get; }
        int PendingTimerCount { get; }
        bool IsTicking { get; }
        void GetTimerEntries(List<TimerDebugEntry> results);
    }
}
#endif
