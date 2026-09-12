using System;

namespace PschLib.Scheduling
{
    public sealed class TimerHandle
    {
        public TimerState State { get; private set; }

        public float Duration { get; }
        public float ElapsedTime { get; private set; }
        public bool IsRepeating { get; }
        public int RepeatCount { get; }
        public int CompletedCount { get; private set; }

        public float RemainingTime => Math.Max(0f, Duration - ElapsedTime);

        public float Progress => Duration <= 0f ? 1f : Math.Min(1f, ElapsedTime / Duration);

        public bool IsRunning => State == TimerState.Running;
        public bool IsPaused => State == TimerState.Paused;
        public bool IsCompleted => State == TimerState.Completed;
        public bool IsCancelled => State == TimerState.Cancelled;
        public bool IsFinished => IsCompleted || IsCancelled;

        internal TimerHandle(float duration, bool startPaused, bool repeat, int repeatCount)
        {
            Duration = duration;
            IsRepeating = repeat;
            RepeatCount = repeat ? repeatCount : 1;
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

        internal void Advance(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            ElapsedTime += deltaTime;
        }

        internal bool FinishCycle()
        {
            CompletedCount++;

            if (RepeatCount > 0 && CompletedCount >= RepeatCount)
            {
                ElapsedTime = Duration;
                State = TimerState.Completed;
                return true;
            }

            ElapsedTime = Math.Max(0f, ElapsedTime - Duration);
            return false;
        }

        internal bool Complete()
        {
            if (!IsRunning)
            {
                return false;
            }

            ElapsedTime = Duration;
            CompletedCount = 1;
            State = TimerState.Completed;
            return true;
        }
    }
}
