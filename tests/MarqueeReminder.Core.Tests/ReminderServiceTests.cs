using System;
using System.Collections.Generic;
using System.IO;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Scheduling;
using MarqueeReminder.Core.Storage;
using Xunit;
using static MarqueeReminder.Core.Tests.Make;

namespace MarqueeReminder.Core.Tests
{
    public class ReminderServiceTests
    {
        private readonly FakeClock _clock = new FakeClock(At(2026, 3, 10, 8, 0));
        private readonly InMemoryStore _store = new InMemoryStore();
        private readonly ReminderService _service;

        public ReminderServiceTests()
        {
            _service = new ReminderService(_store, _clock, new ReminderScheduler(TimeSpan.FromMinutes(30)));
        }

        [Fact]
        public void Add_PersistsAndStampsEffectiveFrom()
        {
            var added = _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default));

            Assert.Equal(_clock.Now, added.EffectiveFrom);
            Assert.Single(_store.Saved);
        }

        [Fact]
        public void Add_RejectsInvalidReminder()
        {
            var invalid = Reminder(Schedule(RecurrenceKind.Weekly, At(2026, 1, 1), 9, 0), default, text: "  ");

            var error = Assert.Throws<ArgumentException>(() => _service.Add(invalid));
            Assert.Contains("text", error.Message);
            Assert.Contains("day of the week", error.Message);
        }

        [Fact]
        public void CollectDue_ReturnsDueRemindersOnce_AndPersistsFiring()
        {
            _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default));
            var savesBefore = _store.SaveCount;

            _clock.Now = At(2026, 3, 10, 8, 59, 59);
            Assert.Empty(_service.CollectDue());

            _clock.Now = At(2026, 3, 10, 9, 0, 0);
            Assert.Single(_service.CollectDue());
            Assert.Equal(At(2026, 3, 10, 9, 0), _store.Saved[0].LastTriggered);
            Assert.Equal(savesBefore + 1, _store.SaveCount);

            _clock.Now = At(2026, 3, 10, 9, 0, 1);
            Assert.Empty(_service.CollectDue());
        }

        [Fact]
        public void CollectDue_StillReturnsReminder_WhenSavingFails()
        {
            var store = new FailingStore();
            var service = new ReminderService(store, _clock, new ReminderScheduler(TimeSpan.FromMinutes(30)));
            service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default));
            store.Fail = true;
            Exception? reported = null;
            service.SaveFailed += (sender, e) => reported = e.Exception;

            _clock.Now = At(2026, 3, 10, 9, 0);

            Assert.Single(service.CollectDue());
            Assert.IsType<IOException>(reported);
        }

        [Fact]
        public void ReEnabling_DoesNotFireForTimePassedWhileDisabled()
        {
            var reminder = _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default));
            _service.SetEnabled(reminder.Id, false);

            _clock.Now = At(2026, 3, 10, 9, 5);
            _service.SetEnabled(reminder.Id, true);

            Assert.Empty(_service.CollectDue());
            Assert.Equal(At(2026, 3, 11, 9, 0), _service.GetNextRun(_service.GetAll()[0]));
        }

        [Fact]
        public void Update_ReplacesReminderAndResetsHistory()
        {
            var reminder = _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default));
            _clock.Now = At(2026, 3, 10, 9, 0);
            _service.CollectDue();

            var edited = _service.GetAll()[0];
            edited.Text = "Drink water";
            edited.Schedule.TimeOfDay = new TimeSpan(10, 0, 0);
            _service.Update(edited);

            var stored = Assert.Single(_service.GetAll());
            Assert.Equal(reminder.Id, stored.Id);
            Assert.Equal("Drink water", stored.Text);
            Assert.Null(stored.LastTriggered);
            Assert.Equal(At(2026, 3, 10, 10, 0), _service.GetNextRun(stored));
        }

        [Fact]
        public void GetAll_ReturnsCopies()
        {
            _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default));

            _service.GetAll()[0].Text = "Mutated";

            Assert.Equal("Stand up and stretch", _service.GetAll()[0].Text);
        }

        [Fact]
        public void Remove_DeletesReminder()
        {
            var reminder = _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default));

            _service.Remove(reminder.Id);

            Assert.Empty(_service.GetAll());
            Assert.Empty(_store.Saved);
        }

        [Fact]
        public void RemoveAll_DeletesSeveralRemindersInOneSave()
        {
            var first = _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default, "first"));
            var second = _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default, "second"));
            _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default, "kept"));
            var savesBefore = _store.SaveCount;

            _service.RemoveAll(new[] { first.Id, second.Id });

            Assert.Equal("kept", Assert.Single(_service.GetAll()).Text);
            Assert.Equal(savesBefore + 1, _store.SaveCount);
        }

        [Fact]
        public void RemoveAll_WithUnknownId_RemovesNothing()
        {
            var reminder = _service.Add(Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), default));

            Assert.Throws<KeyNotFoundException>(() => _service.RemoveAll(new[] { reminder.Id, Guid.NewGuid() }));
            Assert.Single(_service.GetAll());
        }

        private sealed class FailingStore : IReminderStore
        {
            public bool Fail { get; set; }

            public IList<Reminder> Load() => new List<Reminder>();

            public void Save(IEnumerable<Reminder> reminders)
            {
                if (Fail)
                {
                    throw new IOException("Disk full");
                }
            }
        }
    }
}
