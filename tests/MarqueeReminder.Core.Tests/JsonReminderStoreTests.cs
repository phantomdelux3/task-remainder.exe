using System;
using System.Drawing;
using System.IO;
using System.Text;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Storage;
using Xunit;
using static MarqueeReminder.Core.Tests.Make;

namespace MarqueeReminder.Core.Tests
{
    public sealed class JsonReminderStoreTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "MarqueeReminderTests", Guid.NewGuid().ToString("N"));
        private readonly string _file;

        public JsonReminderStoreTests()
        {
            _file = Path.Combine(_directory, "reminders.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Fact]
        public void Load_MissingFile_ReturnsEmpty()
        {
            Assert.Empty(new JsonReminderStore(_file).Load());
        }

        [Fact]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            var original = Reminder(
                Schedule(RecurrenceKind.Weekly, At(2026, 3, 1), 17, 45, WeekDays.Monday | WeekDays.Saturday, dayOfMonth: 31),
                At(2026, 3, 10, 8, 15, 42).AddTicks(1234567),
                text: "Team stand-up \"daily\" — ünïcødé");
            original.IsEnabled = false;
            original.LastTriggered = At(2026, 3, 9, 17, 45);
            original.Style = new MarqueeStyle
            {
                BackColor = Color.FromArgb(0x12, 0x34, 0x56),
                ForeColor = Color.FromArgb(0xAB, 0xCD, 0xEF),
                FontFamily = "Consolas",
                FontSize = 18.5f,
                Bold = false,
                Italic = true,
                Position = MarqueePosition.Right,
                Offset = -25,
                Thickness = 72,
                ScrollSpeed = 300,
                DisplayMinutes = 0,
            };

            var store = new JsonReminderStore(_file);
            store.Save(new[] { original });
            var loaded = Assert.Single(store.Load());

            Assert.Equal(original.Id, loaded.Id);
            Assert.Equal(original.Text, loaded.Text);
            Assert.False(loaded.IsEnabled);
            Assert.Equal(original.EffectiveFrom, loaded.EffectiveFrom);
            Assert.Equal(original.LastTriggered, loaded.LastTriggered);

            Assert.Equal(RecurrenceKind.Weekly, loaded.Schedule.Recurrence);
            Assert.Equal(At(2026, 3, 1), loaded.Schedule.StartDate);
            Assert.Equal(new TimeSpan(17, 45, 0), loaded.Schedule.TimeOfDay);
            Assert.Equal(WeekDays.Monday | WeekDays.Saturday, loaded.Schedule.Days);
            Assert.Equal(31, loaded.Schedule.DayOfMonth);

            Assert.Equal(original.Style.BackColor.ToArgb(), loaded.Style.BackColor.ToArgb());
            Assert.Equal(original.Style.ForeColor.ToArgb(), loaded.Style.ForeColor.ToArgb());
            Assert.Equal("Consolas", loaded.Style.FontFamily);
            Assert.Equal(18.5f, loaded.Style.FontSize);
            Assert.False(loaded.Style.Bold);
            Assert.True(loaded.Style.Italic);
            Assert.Equal(MarqueePosition.Right, loaded.Style.Position);
            Assert.Equal(-25, loaded.Style.Offset);
            Assert.Equal(72, loaded.Style.Thickness);
            Assert.Equal(300, loaded.Style.ScrollSpeed);
            Assert.Equal(0, loaded.Style.DisplayMinutes);
        }

        [Fact]
        public void Save_OverwritesExistingFile()
        {
            var store = new JsonReminderStore(_file);
            store.Save(new[] { Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 1, 1), "first") });
            store.Save(new[] { Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 1, 1), "second") });

            Assert.Equal("second", Assert.Single(store.Load()).Text);
            Assert.False(File.Exists(_file + ".tmp"));
        }

        [Fact]
        public void Load_CorruptFile_ReturnsEmptyAndKeepsBackup()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_file, "{ this is not json");

            Assert.Empty(new JsonReminderStore(_file).Load());
            Assert.Single(Directory.GetFiles(_directory, "reminders.json.corrupt-*"));
        }

        [Fact]
        public void Load_AcceptsFileWithByteOrderMark()
        {
            var store = new JsonReminderStore(_file);
            store.Save(new[] { Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 0), At(2026, 1, 1)) });
            File.WriteAllText(_file, File.ReadAllText(_file), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Assert.Single(store.Load());
            Assert.Empty(Directory.GetFiles(_directory, "reminders.json.corrupt-*"));
        }

        [Fact]
        public void Load_SkipsUnreadableEntries_KeepsTheRest()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_file, @"{
  ""version"": 1,
  ""reminders"": [
    { ""id"": ""not-a-guid"", ""text"": ""broken"", ""recurrence"": ""Daily"", ""startDate"": ""2026-01-01"", ""time"": ""09:00"" },
    { ""id"": ""3f2504e0-4f89-11d3-9a0c-0305e82c3301"", ""text"": ""fine"", ""enabled"": true, ""recurrence"": ""Monthly"",
      ""startDate"": ""2026-01-01"", ""time"": ""07:05"", ""dayOfMonth"": 99, ""thickness"": 5000, ""position"": ""Sideways"" }
  ]
}");

            var loaded = new JsonReminderStore(_file).Load();

            var reminder = Assert.Single(loaded);
            Assert.Equal("fine", reminder.Text);
            Assert.Equal(new TimeSpan(7, 5, 0), reminder.Schedule.TimeOfDay);
            Assert.Equal(31, reminder.Schedule.DayOfMonth);
            Assert.Equal(MarqueeStyle.MaxThickness, reminder.Style.Thickness);
            Assert.Equal(MarqueePosition.Top, reminder.Style.Position);
        }

        [Fact]
        public void Save_WritesReadableJson()
        {
            new JsonReminderStore(_file).Save(new[] { Reminder(Schedule(RecurrenceKind.Daily, At(2026, 1, 1), 9, 5), At(2026, 1, 1)) });

            var json = File.ReadAllText(_file);
            Assert.Contains("\"time\":\"09:05\"", json.Replace(" ", string.Empty));
            Assert.Contains("\"recurrence\":\"Daily\"", json.Replace(" ", string.Empty));
            Assert.True(json.Split('\n').Length > 5, "JSON should be indented.");
            Assert.DoesNotContain("\\/Date", json);
        }
    }
}
