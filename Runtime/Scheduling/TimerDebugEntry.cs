#if UNITY_EDITOR
namespace PschLib.Scheduling
{
    public readonly struct TimerDebugEntry
    {
        public readonly TimerState State;
        public readonly TimerTimeMode TimeMode;
        public readonly float Duration;
        public readonly float ElapsedTime;
        public readonly float RemainingTime;
        public readonly float Progress;
        public readonly bool IsRepeating;
        public readonly int RepeatCount;
        public readonly int CompletedCount;
        public readonly bool IsPending;

        public TimerDebugEntry(TimerHandle handle, TimerTimeMode timeMode, bool isPending)
        {
            State = handle.State;
            TimeMode = timeMode;
            Duration = handle.Duration;
            ElapsedTime = handle.ElapsedTime;
            RemainingTime = handle.RemainingTime;
            Progress = handle.Progress;
            IsRepeating = handle.IsRepeating;
            RepeatCount = handle.RepeatCount;
            CompletedCount = handle.CompletedCount;
            IsPending = isPending;
        }
    }
}
#endif
