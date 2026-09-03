
namespace PschLib.Scheduling
{
    public sealed class TimerHandle
    {
        public TimerState State { get; private set; }

        public bool IsRunning => State == TimerState.Running;
        public bool IsPaused => State == TimerState.Paused;
        public bool IsCompleted => State == TimerState.Completed;
        public bool IsCancelled => State == TimerState.Cancelled;
        public bool IsFinished => IsCompleted || IsCancelled;

        internal TimerHandle(bool startPaused)
        {
            State = startPaused ? TimerState.Paused : TimerState.Running;
        }

        public bool Pause()
        {
            if (!IsRunning)
            {
                return false;
            }

            State = TimerState.Paused;
            return true;
        }

        public bool Resume()
        {
            if (!IsPaused)
            {
                return false;
            }

            State = TimerState.Running;
            return true;
        }

        public bool Cancel()
        {
            if (IsFinished)
            {
                return false;
            }

            State = TimerState.Cancelled;
            return true;
        }

        internal bool Complete()
        {
            if (!IsRunning)
            {
                return false;
            }

            State = TimerState.Completed;
            return true;
        }
    }
}
