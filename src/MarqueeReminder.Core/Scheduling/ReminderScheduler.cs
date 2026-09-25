using System;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Core.Scheduling
{
    /// <summary>
    /// Decides whether a reminder is due, given the current local time.
    /// </summary>
    /// <remarks>
    /// The scheduler is polled rather than armed with a single long timer, so it stays correct when
    /// the PC sleeps, the clock is adjusted, or daylight-saving time starts or ends:
    /// <list type="bullet">
    /// <item>An occurrence fires once the clock reaches it and is then recorded in
    /// <see cref="Reminder.LastTriggered"/>, so it never fires twice (even when the clock falls back
    /// an hour and the same wall-clock time happens again).</item>
    /// <item>An occurrence that falls in a skipped hour (clocks going forward) fires as soon as the
    /// clock jumps past it.</item>
    /// <item>An occurrence missed while the PC was asleep or the app was closed still fires if it is
    /// no older than the grace period; older ones are silently skipped instead of flooding the screen.</item>
    /// </list>
    /// </remarks>
    public sealed class ReminderScheduler
    {
        public static readonly TimeSpan DefaultMissedGracePeriod = TimeSpan.FromMinutes(30);

        private readonly TimeSpan _missedGracePeriod;

        public ReminderScheduler(TimeSpan missedGracePeriod)
        {
            if (missedGracePeriod < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(missedGracePeriod), "Grace period cannot be negative.");
            }

            _missedGracePeriod = missedGracePeriod;
        }

        /// <summary>
        /// Returns the occurrence that should fire now, or null. When several occurrences are pending
        /// only the latest is returned; the caller records it in <see cref="Reminder.LastTriggered"/>.
        /// </summary>
        public DateTime? FindDueOccurrence(Reminder reminder, DateTime now)
        {
            if (!reminder.IsEnabled)
            {
                return null;
            }

            var windowStart = Max(EarliestEligible(reminder), now - _missedGracePeriod);
            DateTime? latest = null;
            for (var occurrence = ScheduleCalculator.NextOccurrence(reminder.Schedule, windowStart);
                 occurrence.HasValue && occurrence.Value <= now;
                 occurrence = ScheduleCalculator.NextOccurrence(reminder.Schedule, occurrence.Value.AddTicks(1)))
            {
                latest = occurrence;
            }

            return latest;
        }

        /// <summary>The next time the reminder will fire after <paramref name="now"/>, or null if never.</summary>
        public DateTime? GetNextRun(Reminder reminder, DateTime now)
        {
            if (!reminder.IsEnabled)
            {
                return null;
            }

            var from = Max(EarliestEligible(reminder), now);
            return ScheduleCalculator.NextOccurrence(reminder.Schedule, from);
        }

        private static DateTime EarliestEligible(Reminder reminder)
        {
            var afterLastTriggered = reminder.LastTriggered?.AddTicks(1) ?? DateTime.MinValue;
            return Max(afterLastTriggered, reminder.EffectiveFrom);
        }

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    }
}
