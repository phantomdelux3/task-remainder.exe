using System;

namespace MarqueeReminder.Core.Scheduling
{
    /// <summary>Source of the current local time, abstracted so scheduling can be tested.</summary>
    public interface IClock
    {
        DateTime Now { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}
