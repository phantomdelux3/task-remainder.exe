using System;
using System.IO;

namespace MarqueeReminder.Infrastructure
{
    internal static class AppPaths
    {
        public static string DataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarqueeReminder");

        public static string RemindersFile { get; } = Path.Combine(DataDirectory, "reminders.json");

        public static string ErrorLogFile { get; } = Path.Combine(DataDirectory, "error.log");
    }
}
