using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Scheduling;
using MarqueeReminder.Core.Storage;

namespace MarqueeReminder.Core
{
    /// <summary>
    /// Owns the reminder list: every change is validated and persisted, and callers only ever
    /// receive copies so they cannot mutate state behind the service's back.
    /// </summary>
    public sealed class ReminderService
    {
        private readonly IReminderStore _store;
        private readonly IClock _clock;
        private readonly ReminderScheduler _scheduler;
        private readonly List<Reminder> _reminders;

        public ReminderService(IReminderStore store, IClock clock, ReminderScheduler scheduler)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _reminders = _store.Load().ToList();
        }

        /// <summary>Raised after any reminder is added, changed, removed or fired.</summary>
        public event EventHandler? Changed;

        /// <summary>Raised when recording fired reminders to disk fails during <see cref="CollectDue"/>.</summary>
        public event EventHandler<SaveFailedEventArgs>? SaveFailed;

        public IReadOnlyList<Reminder> GetAll() => _reminders.Select(r => r.Clone()).ToList();

        public DateTime? GetNextRun(Reminder reminder) => _scheduler.GetNextRun(reminder, _clock.Now);

        public Reminder Add(Reminder reminder)
        {
            var copy = PrepareForSave(reminder);
            if (_reminders.Any(r => r.Id == copy.Id))
            {
                copy.Id = Guid.NewGuid();
            }

            copy.ActiveSince = null;
            _reminders.Add(copy);
            Commit();
            return copy.Clone();
        }

        public void Update(Reminder reminder)
        {
            var index = IndexOf(reminder.Id);
            var copy = PrepareForSave(reminder);

            // Editing never closes a marquee that is on screen; only its password does.
            copy.ActiveSince = _reminders[index].ActiveSince;
            _reminders[index] = copy;
            Commit();
        }

        /// <summary>Checks a password against the reminder's close password. False if the reminder no longer exists.</summary>
        public bool VerifyClosePassword(Guid id, string password)
        {
            var reminder = _reminders.Find(r => r.Id == id);
            return reminder != null && reminder.ClosePassword.Verify(password);
        }

        /// <summary>Records that the reminder's marquee has gone: closed with its password, or its time ran out.</summary>
        public void MarkFinished(Guid id)
        {
            var reminder = _reminders.Find(r => r.Id == id);
            if (reminder?.ActiveSince == null)
            {
                return;
            }

            reminder.ActiveSince = null;
            Commit();
        }

        /// <summary>
        /// The marquees that were on screen when the app last stopped and should be shown again.
        /// Timed marquees whose time ran out meanwhile are marked finished instead.
        /// </summary>
        public IReadOnlyList<ActiveMarquee> RestoreActive()
        {
            var now = _clock.Now;
            var restored = new List<ActiveMarquee>();
            var changed = false;
            foreach (var reminder in _reminders.Where(r => r.ActiveSince.HasValue))
            {
                var duration = reminder.Style.DisplayDuration;
                if (!duration.HasValue)
                {
                    restored.Add(new ActiveMarquee(reminder.Clone(), null));
                    continue;
                }

                // If the clock went backwards, never grant more than the full duration.
                var elapsed = now - reminder.ActiveSince!.Value;
                var remaining = elapsed < TimeSpan.Zero ? duration.Value : duration.Value - elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    restored.Add(new ActiveMarquee(reminder.Clone(), remaining));
                }
                else
                {
                    reminder.ActiveSince = null;
                    changed = true;
                }
            }

            if (changed)
            {
                Commit();
            }

            return restored;
        }

        public void Remove(Guid id) => RemoveAll(new[] { id });

        /// <summary>Removes several reminders in one change. Unknown ids are rejected before anything is removed.</summary>
        public void RemoveAll(IEnumerable<Guid> ids)
        {
            var toRemove = new HashSet<Guid>(ids);
            foreach (var id in toRemove)
            {
                IndexOf(id);
            }

            if (_reminders.RemoveAll(r => toRemove.Contains(r.Id)) > 0)
            {
                Commit();
            }
        }

        public void SetEnabled(Guid id, bool enabled)
        {
            var reminder = _reminders[IndexOf(id)];
            if (reminder.IsEnabled == enabled)
            {
                return;
            }

            reminder.IsEnabled = enabled;
            if (enabled)
            {
                // Don't fire for occurrences that passed while the reminder was switched off.
                reminder.EffectiveFrom = _clock.Now;
            }

            Commit();
        }

        /// <summary>
        /// Returns copies of the reminders that are due right now and marks them as fired.
        /// Call this frequently (every second or so).
        /// </summary>
        public IReadOnlyList<Reminder> CollectDue()
        {
            var now = _clock.Now;
            var due = new List<Reminder>();
            foreach (var reminder in _reminders)
            {
                var occurrence = _scheduler.FindDueOccurrence(reminder, now);
                if (occurrence.HasValue)
                {
                    reminder.LastTriggered = occurrence;
                    reminder.ActiveSince = now;
                    due.Add(reminder.Clone());
                }
            }

            if (due.Count > 0)
            {
                // A failed write must not stop the reminder from being shown; report it instead.
                try
                {
                    _store.Save(_reminders);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    SaveFailed?.Invoke(this, new SaveFailedEventArgs(ex));
                }

                Changed?.Invoke(this, EventArgs.Empty);
            }

            return due;
        }

        private Reminder PrepareForSave(Reminder reminder)
        {
            var errors = reminder.Validate();
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(reminder));
            }

            var copy = reminder.Clone();
            copy.EffectiveFrom = _clock.Now;
            copy.LastTriggered = null;
            return copy;
        }

        private int IndexOf(Guid id)
        {
            var index = _reminders.FindIndex(r => r.Id == id);
            if (index < 0)
            {
                throw new KeyNotFoundException($"Reminder {id} does not exist.");
            }

            return index;
        }

        private void Commit()
        {
            _store.Save(_reminders);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
