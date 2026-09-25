using System.Globalization;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Scheduling;
using Xunit;
using static MarqueeReminder.Core.Tests.Make;

namespace MarqueeReminder.Core.Tests
{
    public class ScheduleDescriberTests
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        [Theory]
        [InlineData(RecurrenceKind.Once, WeekDays.None, 1, "Once on Fri 25 Sep 2026 at 09:05")]
        [InlineData(RecurrenceKind.Daily, WeekDays.None, 1, "Every day at 09:05")]
        [InlineData(RecurrenceKind.Weekly, WeekDays.Weekdays, 1, "Weekdays at 09:05")]
        [InlineData(RecurrenceKind.Weekly, WeekDays.Weekend, 1, "Weekends at 09:05")]
        [InlineData(RecurrenceKind.Weekly, WeekDays.All, 1, "Every day at 09:05")]
        [InlineData(RecurrenceKind.Weekly, WeekDays.Monday | WeekDays.Thursday, 1, "Every Mon, Thu at 09:05")]
        [InlineData(RecurrenceKind.Monthly, WeekDays.None, 15, "Monthly on day 15 at 09:05")]
        [InlineData(RecurrenceKind.Monthly, WeekDays.None, 31, "Monthly on day 31 (or last day of month) at 09:05")]
        public void Describes(RecurrenceKind recurrence, WeekDays days, int dayOfMonth, string expected)
        {
            var schedule = Schedule(recurrence, At(2026, 9, 25), 9, 5, days, dayOfMonth);

            Assert.Equal(expected, ScheduleDescriber.Describe(schedule, Culture));
        }
    }
}
