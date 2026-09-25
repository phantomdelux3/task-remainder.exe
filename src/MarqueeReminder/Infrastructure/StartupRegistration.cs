using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MarqueeReminder.Infrastructure
{
    /// <summary>Registers the app to start (minimized to the tray) when the user signs in to Windows.</summary>
    internal static class StartupRegistration
    {
        public const string MinimizedArgument = "--minimized";

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MarqueeReminder";

        private static string Command => $"\"{Application.ExecutablePath}\" {MinimizedArgument}";

        /// <summary>True only if the registered command points at this copy of the executable.</summary>
        public static bool IsEnabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    return key?.GetValue(ValueName) is string value
                        && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (enabled)
                {
                    key.SetValue(ValueName, Command, RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
        }
    }
}
