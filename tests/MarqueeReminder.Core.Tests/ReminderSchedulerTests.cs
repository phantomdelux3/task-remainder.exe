using System;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Scheduling;
using Xunit;
using static MarqueeReminder.Core.Tests.Make;

namespace MarqueeReminder.Core.Tests
{
    public class ReminderSchedulerTests
    {
        private readonly ReminderScheduler _scheduler = new ReminderScheduler(TimeSpan.FromMinutes(30));

        [Fact]
        public void NotDue_BeforeScheduledTime()
        {
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 3, 10, 8, 0));

            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 8, 59, 59)));
        }

        [Fact]
        public void Due_AtScheduledTime()
        {
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 3, 10, 8, 0));

            Assert.Equal(At(2026, 3, 10, 9, 0), _scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 0)));
        }

        [Fact]
        public void FiresOnlyOncePerOccurrence()
        {
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 3, 10, 8, 0));
            reminder.LastTriggered = _scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 0));

            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 0, 30)));
            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 10)));
            Assert.Equal(At(2026, 3, 11, 9, 0), _scheduler.FindDueOccurrence(reminder, At(2026, 3, 11, 9, 0)));
        }

        [Fact]
        public void IgnoresOccurrencesBeforeEffectiveFrom()
        {
            // Created at 09:00:30 for a 09:00 daily reminder: today's already passed.
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 3, 10, 9, 0, 30));

            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 1)));
            Assert.Equal(At(2026, 3, 11, 9, 0), _scheduler.GetNextRun(reminder, At(2026, 3, 10, 9, 1)));
        }

        [Fact]
        public void MissedWithinGracePeriod_StillFires()
        {
            // PC was asleep at 09:00 and woke at 09:20.
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 3, 10, 8, 0));

            Assert.Equal(At(2026, 3, 10, 9, 0), _scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 20)));
        }

        [Fact]
        public void MissedBeyondGracePeriod_IsSkipped()
        {
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 3, 10, 8, 0));

            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 31)));
            Assert.Equal(At(2026, 3, 11, 9, 0), _scheduler.GetNextRun(reminder, At(2026, 3, 10, 9, 31)));
        }

        [Fact]
        public void SeveralPending_ReturnsOnlyLatest()
        {
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 3, 1));
            var scheduler = new ReminderScheduler(TimeSpan.FromDays(3));

            Assert.Equal(At(2026, 3, 10, 9, 0), scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 5)));
        }

        [Fact]
        public void Disabled_NeverFires()
        {
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 3, 10, 8, 0));
            reminder.IsEnabled = false;

            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 0)));
            Assert.Null(_scheduler.GetNextRun(reminder, At(2026, 3, 10, 8, 0)));
        }

        [Fact]
        public void Once_FiresOnceThenNeverAgain()
        {
            var reminder = Reminder(Schedule(RecurrenceKind.Once, At(2026, 3, 10), 14, 0), At(2026, 3, 10, 8, 0));
            reminder.LastTriggered = _scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 14, 0));

            Assert.Equal(At(2026, 3, 10, 14, 0), reminder.LastTriggered);
            Assert.Null(_scheduler.GetNextRun(reminder, At(2026, 3, 10, 14, 1)));
            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 14, 5)));
        }

        [Fact]
        public void ClockFallingBack_DoesNotRepeatReminder()
        {
            // Daylight saving ends: 01:30 happens twice. The reminder must fire only the first time.
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 1, 30), At(2026, 10, 25, 0, 0));
            reminder.LastTriggered = _scheduler.FindDueOccurrence(reminder, At(2026, 10, 25, 1, 30));

            // Clock is set back from 02:00 to 01:00 and reaches 01:30 again.
            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 10, 25, 1, 0)));
            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 10, 25, 1, 30)));
        }

        [Fact]
        public void ClockSpringingForward_FiresWhenSkippedTimeIsPassed()
        {
            // Daylight saving starts: clock jumps from 02:00 to 03:00, so 02:30 never shows on the clock.
            var reminder = Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 2, 30), At(2026, 3, 29, 0, 0));

            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 29, 1, 59, 59)));
            Assert.Equal(At(2026, 3, 29, 2, 30), _scheduler.FindDueOccurrence(reminder, At(2026, 3, 29, 3, 0)));
        }

        [Fact]
        public void Weekly_FiresOnlyOnSelectedDays()
        {
            // 2026-03-09 is a Monday.
            var reminder = Reminder(
                Schedule(RecurrenceKind.Weekly, At(2026, 1, 1), 9, 0, WeekDays.Monday | WeekDays.Friday),
                At(2026, 3, 1));

            Assert.NotNull(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 9, 9, 0)));
            Assert.Null(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 10, 9, 0)));
            Assert.NotNull(_scheduler.FindDueOccurrence(reminder, At(2026, 3, 13, 9, 0)));
        }
    }
}
