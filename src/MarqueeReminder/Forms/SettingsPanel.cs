using System;
using System.Drawing;
using System.IO;
using System.Security;
using System.Windows.Forms;
using MarqueeReminder.Infrastructure;

namespace MarqueeReminder.Forms
{
    /// <summary>The Settings tab: starting with Windows, and the app password.</summary>
    internal sealed class SettingsPanel : Panel
    {
        private readonly CheckBox _startWithWindows = new CheckBox
        {
            Text = "Start Marquee Reminder automatically when I sign in to Windows",
            AutoSize = true,
            Margin = new Padding(3, 3, 3, 12),
        };

        private readonly Label _passwordStatus = Ui.Label(string.Empty);
        private readonly Button _setPasswordButton = Ui.Button(string.Empty);
        private readonly Button _removePasswordButton = Ui.Button("Remove password…");
        private bool _refreshing;

        public SettingsPanel()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(12);

            _passwordStatus.MaximumSize = new Size(560, 0);
            var forgotHint = Ui.Hint(
                "Forgot the app password? Uninstall Marquee Reminder and install it again to reset it. " +
                "Your reminders are kept.");
            forgotHint.MaximumSize = new Size(560, 0);

            var passwordGrid = Ui.Grid(1);
            passwordGrid.Controls.Add(Ui.Hint("Protects opening this window and quitting the app."));
            passwordGrid.Controls.Add(_passwordStatus);
            passwordGrid.Controls.Add(Ui.Row(_setPasswordButton, _removePasswordButton));
            passwordGrid.Controls.Add(forgotHint);
            var passwordGroup = Ui.Group("App password", passwordGrid);
            passwordGroup.Dock = DockStyle.None;

            var supportLink = new LinkLabel
            {
                Text = AppInfo.SupportEmail,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 3, 6),
            };
            supportLink.LinkClicked += (sender, e) => AppInfo.EmailSupport(FindForm());
            var supportGrid = Ui.Grid(1);
            supportGrid.Controls.Add(Ui.Row(Ui.Label("Questions or problems? Email"), supportLink));
            supportGrid.Controls.Add(Ui.Hint($"Marquee Reminder is developed by {AppInfo.DeveloperName}."));
            var supportGroup = Ui.Group("Support", supportGrid);
            supportGroup.Dock = DockStyle.None;
            supportGroup.Margin = new Padding(3, 12, 3, 3);

            var root = Ui.Grid(1);
            root.Dock = DockStyle.Top;
            root.Controls.Add(_startWithWindows);
            root.Controls.Add(passwordGroup);
            root.Controls.Add(supportGroup);
            Controls.Add(root);

            _startWithWindows.CheckedChanged += (sender, e) => OnStartWithWindowsChanged();
            _setPasswordButton.Click += (sender, e) => SetPassword();
            _removePasswordButton.Click += (sender, e) => RemovePassword();
            RefreshState();
        }

        /// <summary>Re-reads the settings (they can change outside this panel, e.g. a moved app).</summary>
        public void RefreshState()
        {
            _refreshing = true;
            try
            {
                _startWithWindows.Checked = StartupRegistration.IsEnabled;
            }
            finally
            {
                _refreshing = false;
            }

            var isSet = AppPasswordStore.IsSet;
            _passwordStatus.Text = isSet
                ? "An app password is set."
                : "No app password is set. Anyone can open or quit the app.";
            _setPasswordButton.Text = isSet ? "Change password…" : "Set password…";
            _removePasswordButton.Visible = isSet;
        }

        private void OnStartWithWindowsChanged()
        {
            if (_refreshing)
            {
                return;
            }

            RunSafely(() => StartupRegistration.SetEnabled(_startWithWindows.Checked), "Could not change the Windows startup setting.");
            RefreshState();
        }

        private void SetPassword()
        {
            var isSet = AppPasswordStore.IsSet;
            using (var dialog = new SetPasswordForm(isSet ? "Change app password" : "Set app password", isSet ? AppPasswordStore.Verify : (Func<string, bool>?)null))
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                RunSafely(() => AppPasswordStore.Set(dialog.NewPassword), "Could not save the app password.");
            }

            RefreshState();
        }

        private void RemovePassword()
        {
            using (var prompt = new PasswordPromptForm("Remove app password", "Enter the current app password to remove it.", AppPasswordStore.Verify))
            {
                if (prompt.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }
            }

            RunSafely(AppPasswordStore.Remove, "Could not remove the app password.");
            RefreshState();
        }

        private static void RunSafely(Action action, string failureMessage)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                ErrorReporter.Report(ex, failureMessage);
            }
        }
    }
}
