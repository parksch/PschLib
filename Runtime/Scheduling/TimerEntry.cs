using System;

namespace PschLib.Scheduling
{
    internal sealed class TimerEntry
    {
        private readonly Action callback;

        public TimerHandle Handle { get; }
        public TimerTimeMode TimeMode { get; }

        internal TimerEntry(float duration, TimerTimeMode timeMode, Action callback, bool startPaused, bool repeat, int repeatCount)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be finite and non-negative.");
            }

            if (repeat && duration == 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "A repeating timer must have a positive duration.");
            }

            if (repeatCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repeatCount), "Repeat count must be non-negative.");
            }

            if (timeMode != TimerTimeMode.Scaled && timeMode != TimerTimeMode.Unscaled)
            {
                throw new ArgumentOutOfRangeException(nameof(timeMode), "Unsupported timer time mode.");
            }

            TimeMode = timeMode;
            this.callback = callback;
            Handle = new TimerHandle(duration, startPaused, repeat, repeatCount);
        }

        internal bool Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (Handle.IsFinished)
            {
                return true;
            }

            if (!Handle.IsRunning)
            {
                return false;
            }

            var deltaTime = TimeMode == TimerTimeMode.Scaled ? scaledDeltaTime : unscaledDeltaTime;

            Handle.Advance(deltaTime);

            if (Handle.ElapsedTime < Handle.Duration)
            {
                return false;
            }

            if (Handle.IsRepeating)
            {
                while (Handle.IsRunning && Handle.ElapsedTime >= Handle.Duration)
                {
                    var isComplete = Handle.FinishCycle();
                    callback?.Invoke();

                    if (isComplete)
                    {
                        return true;
                    }
                }

                return Handle.IsFinished;
            }

            if (!Handle.Complete())
            {
                return Handle.IsFinished;
            }

            callback?.Invoke();
            return true;
        }
    }
}
