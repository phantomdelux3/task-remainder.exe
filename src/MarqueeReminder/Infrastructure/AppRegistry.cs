namespace MarqueeReminder.Infrastructure
{
    /// <summary>
    /// The app's own registry key (under HKEY_CURRENT_USER). The installer deletes it on uninstall,
    /// which is what resets a forgotten app password. Reminders are stored elsewhere and are kept.
    /// </summary>
    internal static class AppRegistry
    {
        public const string SettingsKeyPath = @"Software\MarqueeReminder";
    }
}
