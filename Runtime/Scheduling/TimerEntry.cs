using System;

namespace PschLib.Scheduling
{
    internal sealed class TimerEntry
    {
        private const int MaxCallbacksPerTick = 5;

        private readonly Action callback;

        public TimerHandle Handle { get; }
        public TimerTimeMode TimeMode { get; }
        public TimerOverflowMode OverflowMode { get; }

        internal TimerEntry(float duration, TimerTimeMode timeMode, Action callback, bool startPaused,
            bool repeat, int repeatCount, TimerOverflowMode overflowMode)
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

            if (overflowMode != TimerOverflowMode.Discard && overflowMode != TimerOverflowMode.Preserve)
            {
                throw new ArgumentOutOfRangeException(nameof(overflowMode), "Unsupported timer overflow mode.");
            }

            TimeMode = timeMode;
            OverflowMode = overflowMode;
            this.callback = callback;
            Handle = new TimerHandle(duration, startPaused, repeat, repeatCount);
        }

        internal void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (Handle.IsFinished)
            {
                return;
            }

            if (!Handle.IsRunning)
            {
                return;
            }

            var deltaTime = TimeMode == TimerTimeMode.Scaled ? scaledDeltaTime : unscaledDeltaTime;

            Handle.Advance(deltaTime);

            if (Handle.ElapsedTime < Handle.Duration)
            {
                return;
            }

            if (Handle.IsRepeating)
            {
                var callbackCount = 0;

                try
                {
                    while (Handle.IsRunning && Handle.ElapsedTime >= Handle.Duration &&
                        callbackCount < MaxCallbacksPerTick)
                    {
                        var isComplete = Handle.FinishCycle();
                        callbackCount++;
                        callback?.Invoke();

                        if (isComplete)
                        {
                            return;
                        }
                    }

                    return;
                }
                finally
                {
                    if (OverflowMode == TimerOverflowMode.Discard && !Handle.IsFinished &&
                        Handle.ElapsedTime >= Handle.Duration)
                    {
                        Handle.DiscardOverflow();
                    }
                }
            }

            if (!Handle.Complete())
            {
                return;
            }

            callback?.Invoke();
        }
    }
}
