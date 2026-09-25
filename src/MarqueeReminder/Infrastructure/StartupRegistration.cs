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
        private const string SettingsKeyPath = @"Software\MarqueeReminder";
        private const string DefaultAppliedValueName = "StartupDefaultApplied";

        private static string Command => $"\"{Application.ExecutablePath}\" {MinimizedArgument}";

        /// <summary>True only if the registered command points at this copy of the executable.</summary>
        public static bool IsEnabled =>
            string.Equals(RegisteredCommand, Command, StringComparison.OrdinalIgnoreCase);

        private static string? RegisteredCommand
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    return key?.GetValue(ValueName) as string;
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

        /// <summary>
        /// Starting with Windows is on by default: it is switched on the first time the app runs.
        /// After that the user's choice is respected. If the app was moved, the entry is updated
        /// to point at this copy so it keeps starting.
        /// </summary>
        public static void ApplyDefault()
        {
            using (var settings = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
            {
                if (settings.GetValue(DefaultAppliedValueName) == null)
                {
                    SetEnabled(true);
                    settings.SetValue(DefaultAppliedValueName, 1, RegistryValueKind.DWord);
                    return;
                }
            }

            if (RegisteredCommand != null && !IsEnabled)
            {
                SetEnabled(true);
            }
        }
    }
}
