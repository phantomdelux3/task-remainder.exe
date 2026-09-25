using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MarqueeReminder.Infrastructure
{
    /// <summary>Who made the app and how to reach them.</summary>
    internal static class AppInfo
    {
        public const string DeveloperName = "deadpool1555";
        public const string SupportEmail = "phantomdeluxe.0555@gmail.com";

        /// <summary>
        /// Starts a new email to support in the user's mail app. If none is set up, copies the
        /// address to the clipboard and says so instead.
        /// </summary>
        public static void EmailSupport(IWin32Window? owner)
        {
            try
            {
                Process.Start(new ProcessStartInfo($"mailto:{SupportEmail}?subject=Marquee%20Reminder") { UseShellExecute = true })?.Dispose();
                return;
            }
            catch (Win32Exception)
            {
                // No app is registered for mailto: links.
            }

            var copied = TryCopyToClipboard(SupportEmail);
            MessageBox.Show(
                owner,
                copied
                    ? $"No email app is set up on this PC, so the address was copied to the clipboard:\n\n{SupportEmail}"
                    : $"No email app is set up on this PC. Please write to:\n\n{SupportEmail}",
                "Marquee Reminder support",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static bool TryCopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (ExternalException)
            {
                return false; // Another app is holding the clipboard.
            }
        }
    }
}
