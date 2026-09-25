using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MarqueeReminder.Core.Storage
{
    // On-disk shape of the reminders file. Dates and times are stored as invariant, local
    // wall-clock strings so the file is human-readable and free of time-zone conversions.

    [DataContract(Name = "document", Namespace = "")]
    internal sealed class ReminderDocumentDto
    {
        public const int CurrentVersion = 2;

        [DataMember(Name = "version", Order = 0)]
        public int Version { get; set; } = CurrentVersion;

        [DataMember(Name = "reminders", Order = 1)]
        public List<ReminderDto>? Reminders { get; set; }
    }

    [DataContract(Name = "reminder", Namespace = "")]
    internal sealed class ReminderDto
    {
        [DataMember(Name = "id", Order = 0)]
        public string? Id { get; set; }

        [DataMember(Name = "text", Order = 1)]
        public string? Text { get; set; }

        [DataMember(Name = "enabled", Order = 2)]
        public bool Enabled { get; set; }

        [DataMember(Name = "recurrence", Order = 3)]
        public string? Recurrence { get; set; }

        [DataMember(Name = "startDate", Order = 4)]
        public string? StartDate { get; set; }

        [DataMember(Name = "time", Order = 5)]
        public string? Time { get; set; }

        [DataMember(Name = "days", Order = 6)]
        public List<string>? Days { get; set; }

        [DataMember(Name = "dayOfMonth", Order = 7)]
        public int DayOfMonth { get; set; }

        [DataMember(Name = "backColor", Order = 8)]
        public string? BackColor { get; set; }

        [DataMember(Name = "foreColor", Order = 9)]
        public string? ForeColor { get; set; }

        [DataMember(Name = "fontFamily", Order = 10)]
        public string? FontFamily { get; set; }

        [DataMember(Name = "bold", Order = 12)]
        public bool Bold { get; set; }

        [DataMember(Name = "italic", Order = 13)]
        public bool Italic { get; set; }

        [DataMember(Name = "position", Order = 14)]
        public string? Position { get; set; }

        [DataMember(Name = "offset", Order = 15)]
        public int Offset { get; set; }

        [DataMember(Name = "thickness", Order = 16)]
        public int Thickness { get; set; }

        [DataMember(Name = "scrollSpeed", Order = 17)]
        public int ScrollSpeed { get; set; }

        [DataMember(Name = "displayMinutes", Order = 18)]
        public int DisplayMinutes { get; set; }

        [DataMember(Name = "effectiveFrom", Order = 19)]
        public string? EffectiveFrom { get; set; }

        [DataMember(Name = "lastTriggered", Order = 20)]
        public string? LastTriggered { get; set; }

        // Absent in files written before version 1.1; then derived from displayMinutes == 0.
        [DataMember(Name = "untilDismissed", Order = 21, EmitDefaultValue = false)]
        public bool? UntilDismissed { get; set; }

        [DataMember(Name = "closePassword", Order = 22)]
        public string? ClosePassword { get; set; }

        [DataMember(Name = "activeSince", Order = 23)]
        public string? ActiveSince { get; set; }
    }
}
