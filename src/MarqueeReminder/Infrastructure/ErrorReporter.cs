using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace MarqueeReminder.Infrastructure
{
    /// <summary>Logs unexpected errors to disk and tells the user, instead of crashing silently.</summary>
    internal static class ErrorReporter
    {
        public static void Report(Exception exception, string context)
        {
            Log(exception, context);
            MessageBox.Show(
                $"{context}\n\n{exception.Message}\n\nDetails were written to:\n{AppPaths.ErrorLogFile}",
                "Marquee Reminder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        public static void Log(Exception exception, string context)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                File.AppendAllText(AppPaths.ErrorLogFile, $"[{stamp}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Logging is best-effort; never let it mask the original error.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
