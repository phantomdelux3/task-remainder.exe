using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Scheduling;
using Xunit;
using static MarqueeReminder.Core.Tests.Make;

namespace MarqueeReminder.Core.Tests
{
    public class ScheduleCalculatorTests
    {
        [Fact]
        public void Daily_BeforeTime_ReturnsSameDay()
        {
            var schedule = Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 30);

            Assert.Equal(At(2026, 3, 10, 9, 30), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 10, 8, 0)));
        }

        [Fact]
        public void Daily_ExactlyAtTime_IsInclusive()
        {
            var schedule = Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 30);

            Assert.Equal(At(2026, 3, 10, 9, 30), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 10, 9, 30)));
        }

        [Fact]
        public void Daily_AfterTime_ReturnsNextDay()
        {
            var schedule = Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 30);

            Assert.Equal(At(2026, 3, 11, 9, 30), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 10, 9, 30, 1)));
        }

        [Fact]
        public void Daily_AcrossYearEnd()
        {
            var schedule = Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 0, 0);

            Assert.Equal(At(2027, 1, 1, 0, 0), ScheduleCalculator.NextOccurrence(schedule, At(2026, 12, 31, 23, 59)));
        }

        [Fact]
        public void StartDateInFuture_IsRespected()
        {
            var schedule = Schedule(RecurrenceKind.Daily, At(2026, 6, 1), 7, 0);

            Assert.Equal(At(2026, 6, 1, 7, 0), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 10, 12, 0)));
        }

        [Fact]
        public void Once_InFuture_ReturnsThatMoment_EvenFarAhead()
        {
            var schedule = Schedule(RecurrenceKind.Once, At(2030, 2, 14), 18, 45);

            Assert.Equal(At(2030, 2, 14, 18, 45), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 10)));
        }

        [Fact]
        public void Once_InPast_ReturnsNull()
        {
            var schedule = Schedule(RecurrenceKind.Once, At(2026, 3, 10), 9, 0);

            Assert.Null(ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 10, 9, 0, 1)));
        }

        [Fact]
        public void Weekly_PicksNextSelectedDay()
        {
            // 2026-03-10 is a Tuesday.
            var schedule = Schedule(RecurrenceKind.Weekly, At(2026, 1, 1), 10, 0, WeekDays.Monday | WeekDays.Wednesday);

            Assert.Equal(At(2026, 3, 11, 10, 0), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 10, 8, 0)));
        }

        [Fact]
        public void Weekly_WrapsIntoNextWeek()
        {
            // 2026-03-14 is a Saturday; next Monday is 2026-03-16.
            var schedule = Schedule(RecurrenceKind.Weekly, At(2026, 1, 1), 10, 0, WeekDays.Monday);

            Assert.Equal(At(2026, 3, 16, 10, 0), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 14, 11, 0)));
        }

        [Fact]
        public void Weekly_SameDayLaterTimePassed_GoesToNextWeek()
        {
            // 2026-03-16 is a Monday.
            var schedule = Schedule(RecurrenceKind.Weekly, At(2026, 1, 1), 10, 0, WeekDays.Monday);

            Assert.Equal(At(2026, 3, 23, 10, 0), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 16, 10, 1)));
        }

        [Fact]
        public void Weekly_NoDays_NeverOccurs()
        {
            var schedule = Schedule(RecurrenceKind.Weekly, At(2026, 1, 1), 10, 0);

            Assert.Null(ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 10)));
        }

        [Fact]
        public void Monthly_NormalDay()
        {
            var schedule = Schedule(RecurrenceKind.Monthly, At(2026, 1, 1), 8, 0, dayOfMonth: 15);

            Assert.Equal(At(2026, 4, 15, 8, 0), ScheduleCalculator.NextOccurrence(schedule, At(2026, 3, 15, 8, 1)));
        }

        [Theory]
        [InlineData(2026, 2, 28)] // Non-leap February.
        [InlineData(2028, 2, 29)] // Leap-year February.
        [InlineData(2026, 4, 30)] // 30-day month.
        [InlineData(2026, 1, 31)] // 31-day month.
        public void Monthly_Day31_FallsBackToLastDayOfShortMonths(int year, int month, int expectedDay)
        {
            var schedule = Schedule(RecurrenceKind.Monthly, At(2020, 1, 1), 8, 0, dayOfMonth: 31);

            Assert.Equal(At(year, month, expectedDay, 8, 0), ScheduleCalculator.NextOccurrence(schedule, At(year, month, 1)));
        }

        [Fact]
        public void Monthly_Day31_DoesNotFireTwiceInShortMonth()
        {
            var schedule = Schedule(RecurrenceKind.Monthly, At(2020, 1, 1), 8, 0, dayOfMonth: 31);

            Assert.True(ScheduleCalculator.OccursOn(schedule, At(2026, 4, 30)));
            Assert.False(ScheduleCalculator.OccursOn(schedule, At(2026, 4, 29)));
            Assert.False(ScheduleCalculator.OccursOn(schedule, At(2026, 3, 30)));
        }

        [Fact]
        public void TimeOfDay_IsTruncatedToMinutes()
        {
            var schedule = new Schedule { TimeOfDay = new System.TimeSpan(9, 30, 59) };

            Assert.Equal(new System.TimeSpan(9, 30, 0), schedule.TimeOfDay);
        }
    }
}
