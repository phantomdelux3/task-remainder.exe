using System;
using System.Drawing;
using System.Windows.Forms;

namespace MarqueeReminder.Forms
{
    /// <summary>
    /// Asks for a password and checks it. Works both as a dialog (<see cref="Form.ShowDialog()"/> returns
    /// OK once the right password is entered) and as a free-standing window (raises <see cref="Verified"/>).
    /// After a wrong password, input pauses briefly to slow down guessing.
    /// </summary>
    internal sealed class PasswordPromptForm : Form
    {
        private const int RetryDelayMs = 1000;

        private readonly Func<string, bool> _verify;
        private readonly TextBox _passwordBox = new TextBox { UseSystemPasswordChar = true, Width = 280, Anchor = AnchorStyles.Left };
        private readonly Label _errorLabel = Ui.Label(string.Empty);
        private readonly Button _okButton = Ui.Button("OK");
        private readonly Timer _retryTimer = new Timer { Interval = RetryDelayMs };

        public PasswordPromptForm(string title, string message, Func<string, bool> verify)
        {
            _verify = verify ?? throw new ArgumentNullException(nameof(verify));

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = title;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var messageLabel = Ui.Label(message);
            messageLabel.MaximumSize = new Size(380, 0);
            _errorLabel.ForeColor = Color.Firebrick;
            _errorLabel.Visible = false;

            var cancelButton = Ui.Button("Cancel");
            cancelButton.DialogResult = DialogResult.Cancel;

            // DialogResult only closes modal dialogs; close a free-standing prompt explicitly.
            cancelButton.Click += (sender, e) =>
            {
                if (!Modal)
                {
                    Close();
                }
            };
            _okButton.Click += (sender, e) => CheckPassword();
            AcceptButton = _okButton;
            CancelButton = cancelButton;

            var buttons = Ui.Row(_okButton, cancelButton);
            buttons.Anchor = AnchorStyles.Right;

            var root = Ui.Grid(1);
            root.Padding = new Padding(12);
            root.Controls.Add(messageLabel);
            root.Controls.Add(Ui.Row(Ui.Label("Password:"), _passwordBox));
            root.Controls.Add(_errorLabel);
            root.Controls.Add(buttons);
            Controls.Add(root);
            ResumeLayout(false);
            PerformLayout();

            _retryTimer.Tick += (sender, e) =>
            {
                _retryTimer.Stop();
                SetInputEnabled(true);
            };
        }

        /// <summary>Raised once the correct password has been entered (after the window closes).</summary>
        public event EventHandler? Verified;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            _passwordBox.Focus();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _retryTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void CheckPassword()
        {
            bool correct;
            Cursor = Cursors.WaitCursor;
            try
            {
                correct = _verify(_passwordBox.Text);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            if (correct)
            {
                DialogResult = DialogResult.OK;
                if (!Modal)
                {
                    Close();
                }

                Verified?.Invoke(this, EventArgs.Empty);
                return;
            }

            _passwordBox.Clear();
            _errorLabel.Text = "Incorrect password. Try again.";
            _errorLabel.Visible = true;
            SetInputEnabled(false);
            _retryTimer.Start();
        }

        private void SetInputEnabled(bool enabled)
        {
            _passwordBox.Enabled = enabled;
            _okButton.Enabled = enabled;
            if (enabled)
            {
                _passwordBox.Focus();
            }
        }
    }
}
