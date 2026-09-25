using System;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Core.Scheduling
{
    /// <summary>Pure calendar math: which local date-times a <see cref="Schedule"/> occurs at.</summary>
    public static class ScheduleCalculator
    {
        // Longest possible gap between two occurrences is 31 days (monthly); scan comfortably past it.
        private const int MaxScanDays = 400;

        /// <summary>Returns the first occurrence at or after <paramref name="from"/>, or null if there is none.</summary>
        public static DateTime? NextOccurrence(Schedule schedule, DateTime from)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            var firstDay = schedule.StartDate > from.Date ? schedule.StartDate : from.Date;
            if (firstDay > DateTime.MaxValue.Date.AddDays(-MaxScanDays))
            {
                return null;
            }

            for (var offset = 0; offset <= MaxScanDays; offset++)
            {
                var day = firstDay.AddDays(offset);
                if (!OccursOn(schedule, day))
                {
                    continue;
                }

                var candidate = day + schedule.TimeOfDay;
                if (candidate >= from)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Whether the schedule has an occurrence on the given calendar date.</summary>
        public static bool OccursOn(Schedule schedule, DateTime date)
        {
            date = date.Date;
            if (date < schedule.StartDate)
            {
                return false;
            }

            switch (schedule.Recurrence)
            {
                case RecurrenceKind.Once:
                    return date == schedule.StartDate;
                case RecurrenceKind.Daily:
                    return true;
                case RecurrenceKind.Weekly:
                    return schedule.Days.Includes(date.DayOfWeek);
                case RecurrenceKind.Monthly:
                    var lastDay = DateTime.DaysInMonth(date.Year, date.Month);
                    return date.Day == Math.Min(schedule.DayOfMonth, lastDay);
                default:
                    throw new ArgumentOutOfRangeException(nameof(schedule), schedule.Recurrence, "Unknown recurrence.");
            }
        }
    }
}
