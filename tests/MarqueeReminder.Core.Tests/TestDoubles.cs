using System;
using System.Collections.Generic;
using System.Linq;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Scheduling;
using MarqueeReminder.Core.Storage;

namespace MarqueeReminder.Core.Tests
{
    internal sealed class FakeClock : IClock
    {
        public FakeClock(DateTime now)
        {
            Now = now;
        }

        public DateTime Now { get; set; }

        public void Advance(TimeSpan by) => Now += by;
    }

    internal sealed class InMemoryStore : IReminderStore
    {
        public List<Reminder> Saved { get; private set; } = new List<Reminder>();

        public int SaveCount { get; private set; }

        public IList<Reminder> Load() => Saved.Select(r => r.Clone()).ToList();

        public void Save(IEnumerable<Reminder> reminders)
        {
            Saved = reminders.Select(r => r.Clone()).ToList();
            SaveCount++;
        }
    }

    internal static class Make
    {
        public static DateTime At(int year, int month, int day, int hour = 0, int minute = 0, int second = 0) =>
            new DateTime(year, month, day, hour, minute, second);

        public static Schedule Schedule(RecurrenceKind recurrence, DateTime start, int hour, int minute, WeekDays days = WeekDays.None, int dayOfMonth = 1) =>
            new Schedule
            {
                Recurrence = recurrence,
                StartDate = start,
                TimeOfDay = new TimeSpan(hour, minute, 0),
                Days = days,
                DayOfMonth = dayOfMonth,
            };

        public static Reminder Reminder(Schedule schedule, DateTime effectiveFrom, string text = "Stand up and stretch") =>
            new Reminder { Text = text, Schedule = schedule, EffectiveFrom = effectiveFrom };
    }
}
