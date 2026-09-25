using System;
using System.Collections.Generic;

namespace MarqueeReminder.Core.Models
{
    /// <summary>
    /// When a reminder fires. All values are local wall-clock time, which is what a user means
    /// by "remind me at 09:00": it stays 09:00 across daylight-saving and time-zone changes.
    /// </summary>
    public sealed class Schedule
    {
        private DateTime _startDate = DateTime.Today;
        private TimeSpan _timeOfDay = new TimeSpan(9, 0, 0);
        private int _dayOfMonth = 1;

        public RecurrenceKind Recurrence { get; set; } = RecurrenceKind.Daily;

        /// <summary>The date of a one-off reminder, or the first date a recurring one may fire.</summary>
        public DateTime StartDate
        {
            get => _startDate;
            set => _startDate = value.Date;
        }

        /// <summary>Time of day, at minute precision.</summary>
        public TimeSpan TimeOfDay
        {
            get => _timeOfDay;
            set
            {
                if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Time of day must be within a single day.");
                }

                _timeOfDay = new TimeSpan(value.Hours, value.Minutes, 0);
            }
        }

        /// <summary>Days a <see cref="RecurrenceKind.Weekly"/> reminder fires on.</summary>
        public WeekDays Days { get; set; } = WeekDays.None;

        /// <summary>
        /// Day a <see cref="RecurrenceKind.Monthly"/> reminder fires on (1 to 31). In shorter
        /// months it fires on the last day of the month instead of being skipped.
        /// </summary>
        public int DayOfMonth
        {
            get => _dayOfMonth;
            set
            {
                if (value < 1 || value > 31)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Day of month must be between 1 and 31.");
                }

                _dayOfMonth = value;
            }
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (Recurrence == RecurrenceKind.Weekly && (Days & WeekDays.All) == WeekDays.None)
            {
                errors.Add("Select at least one day of the week.");
            }

            return errors;
        }

        public Schedule Clone() => (Schedule)MemberwiseClone();
    }
}
