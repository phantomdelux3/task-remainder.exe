using System;
using System.Globalization;
using System.Linq;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Core.Scheduling
{
    /// <summary>Turns a <see cref="Schedule"/> into a short human-readable sentence.</summary>
    public static class ScheduleDescriber
    {
        public static string Describe(Schedule schedule, CultureInfo culture)
        {
            var time = FormatTime(schedule.TimeOfDay, culture);
            switch (schedule.Recurrence)
            {
                case RecurrenceKind.Once:
                    return $"Once on {FormatDate(schedule.StartDate, culture)} at {time}";
                case RecurrenceKind.Daily:
                    return $"Every day at {time}";
                case RecurrenceKind.Weekly:
                    return $"{DescribeDays(schedule.Days, culture)} at {time}";
                case RecurrenceKind.Monthly:
                    var suffix = schedule.DayOfMonth > 28 ? " (or last day of month)" : string.Empty;
                    return $"Monthly on day {schedule.DayOfMonth}{suffix} at {time}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(schedule), schedule.Recurrence, "Unknown recurrence.");
            }
        }

        public static string FormatTime(TimeSpan timeOfDay, CultureInfo culture) =>
            DateTime.Today.Add(timeOfDay).ToString(culture.DateTimeFormat.ShortTimePattern, culture);

        public static string FormatDate(DateTime date, CultureInfo culture) =>
            date.ToString("ddd d MMM yyyy", culture);

        public static string FormatDateTime(DateTime value, CultureInfo culture) =>
            $"{FormatDate(value, culture)}, {value.ToString(culture.DateTimeFormat.ShortTimePattern, culture)}";

        private static string DescribeDays(WeekDays days, CultureInfo culture)
        {
            switch (days & WeekDays.All)
            {
                case WeekDays.All:
                    return "Every day";
                case WeekDays.Weekdays:
                    return "Weekdays";
                case WeekDays.Weekend:
                    return "Weekends";
            }

            var names = WeekDaysExtensions.WeekStartingOn(culture.DateTimeFormat.FirstDayOfWeek)
                .Where(day => days.Includes(day))
                .Select(day => culture.DateTimeFormat.GetAbbreviatedDayName(day));
            return "Every " + string.Join(", ", names);
        }
    }
}
