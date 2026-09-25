using System;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Core
{
    /// <summary>A marquee that should be on screen, and how much longer it should stay.</summary>
    public sealed class ActiveMarquee
    {
        public ActiveMarquee(Reminder reminder, TimeSpan? remaining)
        {
            Reminder = reminder;
            Remaining = remaining;
        }

        public Reminder Reminder { get; }

        /// <summary>Time left on screen, or null if it stays until closed with the password.</summary>
        public TimeSpan? Remaining { get; }
    }
}
