using System;
using System.Drawing;
using System.Windows.Forms;
using MarqueeReminder.Core.Security;

namespace MarqueeReminder.Forms
{
    /// <summary>Sets or changes a password: asks for the current one (when there is one), the new one, and a confirmation.</summary>
    internal sealed class SetPasswordForm : Form
    {
        private readonly Func<string, bool>? _verifyCurrent;
        private readonly TextBox _currentBox = PasswordBox();
        private readonly TextBox _newBox = PasswordBox();
        private readonly TextBox _confirmBox = PasswordBox();
        private readonly Label _errorLabel = Ui.Label(string.Empty);

        /// <param name="verifyCurrent">Checks the current password, or null when none is set yet.</param>
        public SetPasswordForm(string title, Func<string, bool>? verifyCurrent)
        {
            _verifyCurrent = verifyCurrent;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = title;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            _errorLabel.ForeColor = Color.Firebrick;
            _errorLabel.Visible = false;

            var grid = Ui.Grid(2);
            if (verifyCurrent != null)
            {
                Ui.AddRow(grid, Ui.Label("Current password:"), _currentBox);
            }

            Ui.AddRow(grid, Ui.Label("New password:"), _newBox);
            Ui.AddRow(grid, Ui.Label("Confirm new password:"), _confirmBox);
            Ui.AddRow(grid, Ui.Label(string.Empty), Ui.Hint($"At least {PasswordRules.MinimumLength} characters."));

            var okButton = Ui.Button("OK");
            okButton.Click += (sender, e) => Accept();
            var cancelButton = Ui.Button("Cancel");
            cancelButton.DialogResult = DialogResult.Cancel;
            AcceptButton = okButton;
            CancelButton = cancelButton;
            var buttons = Ui.Row(okButton, cancelButton);
            buttons.Anchor = AnchorStyles.Right;

            var root = Ui.Grid(1);
            root.Padding = new Padding(12);
            root.Controls.Add(grid);
            root.Controls.Add(_errorLabel);
            root.Controls.Add(buttons);
            Controls.Add(root);
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>The new password. Only meaningful once the dialog returns <see cref="DialogResult.OK"/>.</summary>
        public string NewPassword { get; private set; } = string.Empty;

        private static TextBox PasswordBox() => new TextBox { UseSystemPasswordChar = true, Width = 220, Anchor = AnchorStyles.Left };

        private void Accept()
        {
            string? error = null;
            if (_verifyCurrent != null && !_verifyCurrent(_currentBox.Text))
            {
                error = "The current password is incorrect.";
                _currentBox.Clear();
            }
            else if (PasswordRules.Validate(_newBox.Text) is string ruleError)
            {
                error = ruleError;
            }
            else if (_newBox.Text != _confirmBox.Text)
            {
                error = "The new passwords do not match.";
                _confirmBox.Clear();
            }

            if (error != null)
            {
                _errorLabel.Text = error;
                _errorLabel.Visible = true;
                return;
            }

            NewPassword = _newBox.Text;
            DialogResult = DialogResult.OK;
        }
    }
}
