using System;
using System.Collections.Generic;
using System.Linq;

namespace MarqueeReminder.Core.Models
{
    /// <summary>A set of days of the week, bit-mapped so it serializes and compares cheaply.</summary>
    [Flags]
    public enum WeekDays
    {
        None = 0,
        Sunday = 1 << DayOfWeek.Sunday,
        Monday = 1 << DayOfWeek.Monday,
        Tuesday = 1 << DayOfWeek.Tuesday,
        Wednesday = 1 << DayOfWeek.Wednesday,
        Thursday = 1 << DayOfWeek.Thursday,
        Friday = 1 << DayOfWeek.Friday,
        Saturday = 1 << DayOfWeek.Saturday,
        Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
        Weekend = Saturday | Sunday,
        All = Weekdays | Weekend,
    }

    public static class WeekDaysExtensions
    {
        public static WeekDays ToWeekDays(this DayOfWeek day) => (WeekDays)(1 << (int)day);

        public static bool Includes(this WeekDays days, DayOfWeek day) => (days & day.ToWeekDays()) != 0;

        /// <summary>All seven days in calendar order, starting from <paramref name="firstDay"/>.</summary>
        public static IEnumerable<DayOfWeek> WeekStartingOn(DayOfWeek firstDay) =>
            Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)firstDay + i) % 7));
    }
}
