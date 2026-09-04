using System;

namespace PschLib.Scheduling
{
    internal sealed class TimerEntry 
    {
        private readonly Action callback;

        public TimerHandle Handle { get;}
        public TimerTimeMode TimeMode { get;}

        internal TimerEntry(float duration, TimerTimeMode timeMode , Action callback, bool startPaused)
        {
            if (duration < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
            }

            TimeMode = timeMode;
            this.callback = callback;
            Handle = new TimerHandle(duration, startPaused);
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

            if (!Handle.Complete())
            { 
                return Handle.IsFinished;
            }

            callback?.Invoke();
            return true;
        }
    }
}
