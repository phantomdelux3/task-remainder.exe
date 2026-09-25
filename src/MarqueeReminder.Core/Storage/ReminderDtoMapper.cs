using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Security;

namespace MarqueeReminder.Core.Storage
{
    internal static class ReminderDtoMapper
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "HH:mm";
        private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static ReminderDto ToDto(Reminder reminder)
        {
            var schedule = reminder.Schedule;
            var style = reminder.Style;
            return new ReminderDto
            {
                Id = reminder.Id.ToString("D"),
                Text = reminder.Text,
                Enabled = reminder.IsEnabled,
                Recurrence = schedule.Recurrence.ToString(),
                StartDate = schedule.StartDate.ToString(DateFormat, Invariant),
                Time = DateTime.MinValue.Add(schedule.TimeOfDay).ToString(TimeFormat, Invariant),
                Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>()
                    .Where(day => schedule.Days.Includes(day))
                    .Select(day => day.ToString())
                    .ToList(),
                DayOfMonth = schedule.DayOfMonth,
                BackColor = ToHex(style.BackColor),
                ForeColor = ToHex(style.ForeColor),
                FontFamily = style.FontFamily,
                Bold = style.Bold,
                Italic = style.Italic,
                Position = style.Position.ToString(),
                Offset = style.Offset,
                Thickness = style.Thickness,
                ScrollSpeed = style.ScrollSpeed,
                DisplayMinutes = style.DisplayMinutes,
                EffectiveFrom = reminder.EffectiveFrom.ToString(DateTimeFormat, Invariant),
                LastTriggered = reminder.LastTriggered?.ToString(DateTimeFormat, Invariant),
                UntilDismissed = style.ShowUntilDismissed,
                ClosePassword = reminder.ClosePassword.ToString(),
                ActiveSince = reminder.ActiveSince?.ToString(DateTimeFormat, Invariant),
            };
        }

        /// <summary>Converts a stored reminder back; returns null if its essential fields are unreadable.</summary>
        public static Reminder? FromDto(ReminderDto dto)
        {
            if (!Guid.TryParse(dto.Id, out var id)
                || !TryParseEnum(dto.Recurrence, out RecurrenceKind recurrence)
                || !DateTime.TryParseExact(dto.StartDate, DateFormat, Invariant, DateTimeStyles.None, out var startDate)
                || !DateTime.TryParseExact(dto.Time, TimeFormat, Invariant, DateTimeStyles.None, out var time))
            {
                return null;
            }

            var defaults = new MarqueeStyle();
            return new Reminder
            {
                Id = id,
                Text = dto.Text ?? string.Empty,
                IsEnabled = dto.Enabled,
                Schedule = new Schedule
                {
                    Recurrence = recurrence,
                    StartDate = startDate,
                    TimeOfDay = time.TimeOfDay,
                    Days = ParseDays(dto),
                    DayOfMonth = Math.Max(1, Math.Min(31, dto.DayOfMonth)),
                },
                Style = new MarqueeStyle
                {
                    BackColor = ParseColor(dto.BackColor, defaults.BackColor),
                    ForeColor = ParseColor(dto.ForeColor, defaults.ForeColor),
                    FontFamily = dto.FontFamily ?? defaults.FontFamily,
                    Bold = dto.Bold,
                    Italic = dto.Italic,
                    Position = TryParseEnum(dto.Position, out MarqueePosition position) ? position : defaults.Position,
                    Offset = dto.Offset,
                    Thickness = dto.Thickness > 0 ? dto.Thickness : defaults.Thickness,
                    ScrollSpeed = dto.ScrollSpeed > 0 ? dto.ScrollSpeed : defaults.ScrollSpeed,
                    ShowUntilDismissed = dto.UntilDismissed ?? dto.DisplayMinutes <= 0,
                    DisplayMinutes = dto.DisplayMinutes > 0 ? dto.DisplayMinutes : defaults.DisplayMinutes,
                },
                ClosePassword = PasswordHash.TryParse(dto.ClosePassword, out var closePassword)
                    ? closePassword!
                    : PasswordHash.DefaultForReminders,
                EffectiveFrom = ParseDateTime(dto.EffectiveFrom) ?? DateTime.Now,
                LastTriggered = ParseDateTime(dto.LastTriggered),
                ActiveSince = ParseDateTime(dto.ActiveSince),
            };
        }

        private static WeekDays ParseDays(ReminderDto dto) =>
            (dto.Days ?? Enumerable.Empty<string>())
                .Select(name => TryParseEnum(name, out DayOfWeek day) ? day.ToWeekDays() : WeekDays.None)
                .Aggregate(WeekDays.None, (all, day) => all | day);

        private static DateTime? ParseDateTime(string? value) =>
            DateTime.TryParseExact(value, DateTimeFormat, Invariant, DateTimeStyles.None, out var result)
                ? result
                : (DateTime?)null;

        private static bool TryParseEnum<TEnum>(string? value, out TEnum result)
            where TEnum : struct
        {
            // Enum.TryParse also accepts numbers; only accept defined names.
            return Enum.TryParse(value, ignoreCase: true, out result)
                && Enum.IsDefined(typeof(TEnum), result)
                && !int.TryParse(value, out _);
        }

        private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        private static Color ParseColor(string? value, Color fallback)
        {
            if (value == null || value.Length != 7 || value[0] != '#'
                || !int.TryParse(value.Substring(1), NumberStyles.HexNumber, Invariant, out var rgb))
            {
                return fallback;
            }

            return Color.FromArgb(255, Color.FromArgb(rgb));
        }
    }
}
