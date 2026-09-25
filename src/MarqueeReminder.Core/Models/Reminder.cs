using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MarqueeReminder.Core.Models
{
    public sealed class Reminder
    {
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        private string _text = string.Empty;

        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>The message scrolled across the marquee. Kept on a single line.</summary>
        public string Text
        {
            get => _text;
            set => _text = Whitespace.Replace(value ?? string.Empty, " ").Trim();
        }

        public bool IsEnabled { get; set; } = true;

        public Schedule Schedule { get; set; } = new Schedule();

        public MarqueeStyle Style { get; set; } = new MarqueeStyle();

        /// <summary>
        /// Occurrences scheduled before this moment are ignored. Set whenever the reminder is
        /// created, edited or re-enabled so it never fires for a time that had already passed.
        /// </summary>
        public DateTime EffectiveFrom { get; set; }

        /// <summary>The scheduled occurrence that most recently fired, so it never fires twice.</summary>
        public DateTime? LastTriggered { get; set; }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (Text.Length == 0)
            {
                errors.Add("Enter the text to display.");
            }

            errors.AddRange(Schedule.Validate());
            return errors;
        }

        public Reminder Clone()
        {
            var copy = (Reminder)MemberwiseClone();
            copy.Schedule = Schedule.Clone();
            copy.Style = Style.Clone();
            return copy;
        }
    }
}
