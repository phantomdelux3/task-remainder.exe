using MarqueeReminder.Core.Security;
using Microsoft.Win32;

namespace MarqueeReminder.Infrastructure
{
    /// <summary>
    /// The optional app password that protects opening the main window and quitting the app.
    /// Only its salted hash is stored. There is deliberately no recovery: uninstalling the app
    /// removes it.
    /// </summary>
    internal static class AppPasswordStore
    {
        private const string ValueName = "AppPassword";

        public static bool IsSet => Load() != null;

        /// <summary>Checks a password against the current app password. False when none is set.</summary>
        public static bool Verify(string password) => Load()?.Verify(password) ?? false;

        public static void Set(string password)
        {
            var hash = PasswordHash.Create(password);
            using (var key = Registry.CurrentUser.CreateSubKey(AppRegistry.SettingsKeyPath))
            {
                key.SetValue(ValueName, hash.ToString(), RegistryValueKind.String);
            }
        }

        public static void Remove()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AppRegistry.SettingsKeyPath, writable: true))
            {
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }

        private static PasswordHash? Load()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(AppRegistry.SettingsKeyPath))
            {
                return PasswordHash.TryParse(key?.GetValue(ValueName) as string, out var hash) ? hash : null;
            }
        }
    }
}
