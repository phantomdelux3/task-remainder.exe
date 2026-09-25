using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Scheduling;
using MarqueeReminder.Core.Security;
using MarqueeReminder.Marquee;

namespace MarqueeReminder.Forms
{
    /// <summary>Creates or edits a single reminder: its text, schedule, close password and marquee appearance.</summary>
    internal sealed class ReminderEditorForm : Form
    {
        private const string SampleText = "Your reminder text will scroll like this";
        private const int MinSampleHeight = 24;
        private const int MaxSampleHeight = 120;
        private static readonly TimeSpan PreviewDuration = TimeSpan.FromSeconds(10);

        private readonly Reminder _reminder;
        private readonly bool _isNew;
        private readonly MarqueeManager _marquees;
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

        private readonly TextBox _textBox = new TextBox { Width = 460, Anchor = AnchorStyles.Left };

        private readonly ComboBox _recurrenceCombo = Ui.DropDown(
            new Choice<RecurrenceKind>("Once", RecurrenceKind.Once),
            new Choice<RecurrenceKind>("Every day", RecurrenceKind.Daily),
            new Choice<RecurrenceKind>("Weekly, on selected days", RecurrenceKind.Weekly),
            new Choice<RecurrenceKind>("Monthly", RecurrenceKind.Monthly));

        private readonly Label _dateLabel = Ui.Label("Date:");
        private readonly DateTimePicker _datePicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "ddd d MMM yyyy", Width = 160 };
        private readonly DateTimePicker _timePicker = new DateTimePicker { Format = DateTimePickerFormat.Custom, ShowUpDown = true, Width = 100 };
        private readonly Label _daysLabel = Ui.Label("On:");
        private readonly FlowLayoutPanel _daysPanel;
        private readonly Dictionary<DayOfWeek, CheckBox> _dayChecks = new Dictionary<DayOfWeek, CheckBox>();
        private readonly Label _dayOfMonthLabel = Ui.Label("Day of month:");
        private readonly NumericUpDown _dayOfMonthInput = Ui.Number(1, 31, 60);
        private readonly FlowLayoutPanel _dayOfMonthRow;
        private readonly Label _nextRunLabel = Ui.Label(string.Empty);

        private readonly NumericUpDown _durationInput = Ui.Number(MarqueeStyle.MinDisplayMinutes, MarqueeStyle.MaxDisplayMinutes);
        private readonly CheckBox _untilDismissedCheck = new CheckBox { Text = "Until closed with the password (no time limit)", AutoSize = true, Margin = new Padding(12, 5, 3, 3) };
        private readonly TextBox _passwordBox = new TextBox { UseSystemPasswordChar = true, Width = 160, Anchor = AnchorStyles.Left };
        private readonly CheckBox _showPasswordCheck = new CheckBox { Text = "Show", AutoSize = true, Margin = new Padding(6, 5, 3, 3) };

        private readonly Button _backColorButton = Ui.Button(string.Empty);
        private readonly Button _foreColorButton = Ui.Button(string.Empty);
        private readonly ComboBox _fontCombo = Ui.DropDown(MarqueeFonts.InstalledFamilies());
        private readonly CheckBox _boldCheck = new CheckBox { Text = "Bold", AutoSize = true, Margin = new Padding(12, 5, 3, 3) };
        private readonly CheckBox _italicCheck = new CheckBox { Text = "Italic", AutoSize = true, Margin = new Padding(6, 5, 3, 3) };
        private readonly ComboBox _positionCombo = Ui.DropDown(
            new Choice<MarqueePosition>("Top", MarqueePosition.Top),
            new Choice<MarqueePosition>("Bottom", MarqueePosition.Bottom),
            new Choice<MarqueePosition>("Left", MarqueePosition.Left),
            new Choice<MarqueePosition>("Right", MarqueePosition.Right));

        private readonly NumericUpDown _offsetInput = Ui.Number(MarqueeStyle.MinOffset, MarqueeStyle.MaxOffset);
        private readonly Label _offsetHint = Ui.Hint(string.Empty);
        private readonly Label _thicknessLabel = Ui.Label("Bar height:");
        private readonly NumericUpDown _thicknessInput = Ui.Number(MarqueeStyle.MinThickness, MarqueeStyle.MaxThickness);
        private readonly NumericUpDown _speedInput = Ui.Number(MarqueeStyle.MinScrollSpeed, MarqueeStyle.MaxScrollSpeed);
        private readonly Label _sample = new Label
        {
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Padding = new Padding(8, 0, 8, 0),
            Margin = new Padding(3, 8, 3, 3),
        };

        private Font? _sampleFont;

        public ReminderEditorForm(Reminder? reminder, MarqueeManager marquees)
        {
            _isNew = reminder == null;
            _reminder = reminder?.Clone() ?? CreateDefaultReminder();
            _marquees = marquees;

            _timePicker.CustomFormat = _culture.DateTimeFormat.ShortTimePattern;
            _fontCombo.Width = 220;
            _daysPanel = BuildDayChecks();
            _dayOfMonthRow = Ui.Row(_dayOfMonthInput, Ui.Hint("Shorter months use their last day."));

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = _isNew ? "New reminder" : "Edit reminder";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Controls.Add(BuildLayout());
            ResumeLayout(false);
            PerformLayout();

            LoadFromReminder();
            WireEvents();
            UpdateScheduleUi();
            UpdateAppearanceUi();
        }

        /// <summary>The edited reminder. Only meaningful once the dialog returns <see cref="DialogResult.OK"/>.</summary>
        public Reminder Result => _reminder.Clone();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sampleFont?.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Reminder CreateDefaultReminder()
        {
            var suggested = DateTime.Now.AddMinutes(5);
            return new Reminder
            {
                Schedule = new Schedule
                {
                    Recurrence = RecurrenceKind.Daily,
                    StartDate = suggested.Date,
                    TimeOfDay = new TimeSpan(suggested.Hour, suggested.Minute, 0),
                    Days = suggested.DayOfWeek.ToWeekDays(),
                    DayOfMonth = suggested.Day,
                },
            };
        }

        private Control BuildLayout()
        {
            var textGrid = Ui.Grid(2);
            Ui.AddRow(textGrid, Ui.Label("Text to display:"), _textBox);

            var scheduleGrid = Ui.Grid(2);
            Ui.AddRow(scheduleGrid, Ui.Label("Repeat:"), _recurrenceCombo);
            Ui.AddRow(scheduleGrid, _dateLabel, Ui.Row(_datePicker, Ui.Label("at"), _timePicker));
            Ui.AddRow(scheduleGrid, _daysLabel, _daysPanel);
            Ui.AddRow(scheduleGrid, _dayOfMonthLabel, _dayOfMonthRow);
            Ui.AddRow(scheduleGrid, Ui.Label(string.Empty), _nextRunLabel);

            var closingGrid = Ui.Grid(2);
            Ui.AddRow(closingGrid, Ui.Label("Show for:"), Ui.Row(_durationInput, Ui.Hint("minutes"), _untilDismissedCheck));
            Ui.AddRow(closingGrid, Ui.Label("Close password:"), Ui.Row(_passwordBox, _showPasswordCheck, Ui.Hint(PasswordHint())));

            var appearanceGrid = Ui.Grid(2);
            Ui.AddRow(appearanceGrid, Ui.Label("Colours:"), Ui.Row(Ui.Label("Background"), _backColorButton, Ui.Label("Text"), _foreColorButton));
            Ui.AddRow(appearanceGrid, Ui.Label("Font:"), Ui.Row(_fontCombo, _boldCheck, _italicCheck));
            Ui.AddRow(appearanceGrid, Ui.Label("Position:"), _positionCombo);
            Ui.AddRow(appearanceGrid, Ui.Label("Offset (px):"), Ui.Row(_offsetInput, _offsetHint));
            Ui.AddRow(appearanceGrid, _thicknessLabel, Ui.Row(_thicknessInput, Ui.Hint("px. Text size adjusts to fit automatically.")));
            Ui.AddRow(appearanceGrid, Ui.Label("Scroll speed:"), Ui.Row(_speedInput, Ui.Hint("px per second")));
            var sampleRow = appearanceGrid.RowCount++;
            appearanceGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appearanceGrid.Controls.Add(_sample, 0, sampleRow);
            appearanceGrid.SetColumnSpan(_sample, 2);

            var previewButton = Ui.Button("Preview on screen");
            previewButton.Click += (sender, e) => PreviewOnScreen();
            var saveButton = Ui.Button("Save");
            saveButton.Click += (sender, e) => Save();
            var cancelButton = Ui.Button("Cancel");
            cancelButton.DialogResult = DialogResult.Cancel;
            AcceptButton = saveButton;
            CancelButton = cancelButton;

            var buttons = Ui.Grid(2);
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttons.Margin = new Padding(0, 8, 0, 0);
            buttons.Controls.Add(previewButton, 0, 0);
            buttons.Controls.Add(Ui.Row(saveButton, cancelButton), 1, 0);

            var root = Ui.Grid(1);
            root.Padding = new Padding(12);
            root.Controls.Add(textGrid);
            root.Controls.Add(Ui.Group("Schedule", scheduleGrid));
            root.Controls.Add(Ui.Group("Closing the marquee", closingGrid));
            root.Controls.Add(Ui.Group("Appearance", appearanceGrid));
            root.Controls.Add(buttons);
            return root;
        }

        private string PasswordHint() => _isNew
            ? $"Default is {PasswordHash.DefaultReminderPassword}. Needed to close the marquee."
            : "Leave blank to keep the current password.";

        private FlowLayoutPanel BuildDayChecks()
        {
            var panel = Ui.Row();
            foreach (var day in WeekDaysExtensions.WeekStartingOn(_culture.DateTimeFormat.FirstDayOfWeek))
            {
                var check = new CheckBox
                {
                    Text = _culture.DateTimeFormat.GetAbbreviatedDayName(day),
                    AutoSize = true,
                    Margin = new Padding(3, 3, 6, 3),
                };
                _dayChecks.Add(day, check);
                panel.Controls.Add(check);
            }

            return panel;
        }

        private void LoadFromReminder()
        {
            var schedule = _reminder.Schedule;
            var style = _reminder.Style;

            _textBox.Text = _reminder.Text;
            SelectChoice(_recurrenceCombo, schedule.Recurrence);
            _datePicker.Value = schedule.StartDate;
            _timePicker.Value = DateTime.Today.Add(schedule.TimeOfDay);
            foreach (var pair in _dayChecks)
            {
                pair.Value.Checked = schedule.Days.Includes(pair.Key);
            }

            _dayOfMonthInput.Value = schedule.DayOfMonth;
            _durationInput.Value = style.DisplayMinutes;
            _untilDismissedCheck.Checked = style.ShowUntilDismissed;
            _passwordBox.Text = _isNew ? PasswordHash.DefaultReminderPassword : string.Empty;

            SelectFont(style.FontFamily);
            _boldCheck.Checked = style.Bold;
            _italicCheck.Checked = style.Italic;
            SelectChoice(_positionCombo, style.Position);
            _offsetInput.Value = style.Offset;
            _thicknessInput.Value = style.Thickness;
            _speedInput.Value = style.ScrollSpeed;
        }

        private void SelectFont(string family)
        {
            var match = _fontCombo.Items.Cast<string>().FirstOrDefault(name => string.Equals(name, family, StringComparison.OrdinalIgnoreCase))
                ?? _fontCombo.Items.Cast<string>().FirstOrDefault(name => name == MarqueeStyle.DefaultFontFamily);
            if (match != null)
            {
                _fontCombo.SelectedItem = match;
            }
            else if (_fontCombo.Items.Count > 0)
            {
                _fontCombo.SelectedIndex = 0;
            }
        }

        private void WireEvents()
        {
            EventHandler scheduleChanged = (sender, e) => UpdateScheduleUi();
            _recurrenceCombo.SelectedIndexChanged += scheduleChanged;
            _datePicker.ValueChanged += scheduleChanged;
            _timePicker.ValueChanged += scheduleChanged;
            _dayOfMonthInput.ValueChanged += scheduleChanged;
            foreach (var check in _dayChecks.Values)
            {
                check.CheckedChanged += scheduleChanged;
            }

            EventHandler appearanceChanged = (sender, e) => UpdateAppearanceUi();
            _textBox.TextChanged += appearanceChanged;
            _positionCombo.SelectedIndexChanged += appearanceChanged;
            _fontCombo.SelectedIndexChanged += appearanceChanged;
            _boldCheck.CheckedChanged += appearanceChanged;
            _italicCheck.CheckedChanged += appearanceChanged;
            _thicknessInput.ValueChanged += appearanceChanged;
            _untilDismissedCheck.CheckedChanged += appearanceChanged;
            _showPasswordCheck.CheckedChanged += (sender, e) => _passwordBox.UseSystemPasswordChar = !_showPasswordCheck.Checked;
            _backColorButton.Click += (sender, e) => PickColor(isBackground: true);
            _foreColorButton.Click += (sender, e) => PickColor(isBackground: false);
        }

        /// <summary>Copies the form's current values into the working reminder (except the password, see <see cref="Save"/>).</summary>
        private void ApplyToReminder()
        {
            _reminder.Text = _textBox.Text;

            var schedule = _reminder.Schedule;
            schedule.Recurrence = SelectedChoice<RecurrenceKind>(_recurrenceCombo);
            schedule.StartDate = _datePicker.Value;
            schedule.TimeOfDay = _timePicker.Value.TimeOfDay;
            schedule.Days = _dayChecks.Where(pair => pair.Value.Checked)
                .Aggregate(WeekDays.None, (days, pair) => days | pair.Key.ToWeekDays());
            schedule.DayOfMonth = (int)_dayOfMonthInput.Value;

            var style = _reminder.Style;
            style.FontFamily = _fontCombo.SelectedItem as string ?? MarqueeStyle.DefaultFontFamily;
            style.Bold = _boldCheck.Checked;
            style.Italic = _italicCheck.Checked;
            style.Position = SelectedChoice<MarqueePosition>(_positionCombo);
            style.Offset = (int)_offsetInput.Value;
            style.Thickness = (int)_thicknessInput.Value;
            style.ScrollSpeed = (int)_speedInput.Value;
            style.ShowUntilDismissed = _untilDismissedCheck.Checked;
            style.DisplayMinutes = (int)_durationInput.Value;
        }

        private void UpdateScheduleUi()
        {
            var recurrence = SelectedChoice<RecurrenceKind>(_recurrenceCombo);
            _dateLabel.Text = recurrence == RecurrenceKind.Once ? "Date:" : "Starting:";
            _daysLabel.Visible = _daysPanel.Visible = recurrence == RecurrenceKind.Weekly;
            _dayOfMonthLabel.Visible = _dayOfMonthRow.Visible = recurrence == RecurrenceKind.Monthly;

            ApplyToReminder();
            _nextRunLabel.Text = DescribeNextRun();
        }

        private string DescribeNextRun()
        {
            var errors = _reminder.Schedule.Validate();
            if (errors.Count > 0)
            {
                return errors[0];
            }

            var now = DateTime.Now;
            var next = ScheduleCalculator.NextOccurrence(_reminder.Schedule, now);
            return next.HasValue
                ? $"Next reminder: {ScheduleDescriber.FormatDateTime(next.Value, _culture)} ({FormatRelative(next.Value - now)})"
                : "This date and time has already passed.";
        }

        private void UpdateAppearanceUi()
        {
            ApplyToReminder();
            var style = _reminder.Style;
            var edge = style.Position.ToString().ToLowerInvariant();

            _durationInput.Enabled = !style.ShowUntilDismissed;
            _offsetHint.Text = $"from the {edge} edge (+ moves inward, − moves outward)";
            _thicknessLabel.Text = style.IsVertical ? "Bar width:" : "Bar height:";
            _backColorButton.BackColor = style.BackColor;
            _foreColorButton.BackColor = style.ForeColor;

            // The sample is drawn as thick as the real bar (within reason), with the text fitted the same way.
            var scale = DeviceDpi / 96f;
            var sampleHeight = (int)Math.Round(Math.Max(MinSampleHeight, Math.Min(MaxSampleHeight, style.Thickness)) * scale);
            var sampleFont = MarqueeFonts.CreateFitted(style, sampleHeight);
            _sample.Height = sampleHeight;
            _sample.Font = sampleFont;
            _sampleFont?.Dispose();
            _sampleFont = sampleFont;

            _sample.BackColor = style.BackColor;
            _sample.ForeColor = style.ForeColor;
            _sample.Text = string.IsNullOrWhiteSpace(_textBox.Text) ? SampleText : _textBox.Text;
        }

        private void PickColor(bool isBackground)
        {
            var style = _reminder.Style;
            using (var dialog = new ColorDialog { Color = isBackground ? style.BackColor : style.ForeColor, FullOpen = true, AnyColor = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (isBackground)
                {
                    style.BackColor = dialog.Color;
                }
                else
                {
                    style.ForeColor = dialog.Color;
                }
            }

            UpdateAppearanceUi();
        }

        private void PreviewOnScreen()
        {
            ApplyToReminder();
            var preview = _reminder.Clone();
            if (preview.Text.Length == 0)
            {
                preview.Text = SampleText;
            }

            _marquees.ShowPreview(preview, PreviewDuration);
        }

        private void Save()
        {
            ApplyToReminder();
            var errors = _reminder.Validate().ToList();
            if (errors.Count == 0
                && _reminder.Schedule.Recurrence == RecurrenceKind.Once
                && ScheduleCalculator.NextOccurrence(_reminder.Schedule, DateTime.Now) == null)
            {
                errors.Add("The chosen date and time has already passed.");
            }

            // A new reminder always needs a password; an existing one keeps its own when left blank.
            var password = _passwordBox.Text;
            var changePassword = _isNew || password.Length > 0;
            if (changePassword && PasswordRules.Validate(password) is string passwordError)
            {
                errors.Add("Close password: " + passwordError);
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(this, string.Join(Environment.NewLine, errors), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (changePassword)
            {
                _reminder.ClosePassword = PasswordHash.Create(password);
            }

            DialogResult = DialogResult.OK;
        }

        private static string FormatRelative(TimeSpan span)
        {
            // Round up so "09:00" seen at 08:55:30 reads "in 5 min", not "in 4 min".
            var minutes = (long)Math.Ceiling(span.TotalMinutes);
            if (minutes < 60)
            {
                return $"in {minutes} min";
            }

            if (minutes < 24 * 60)
            {
                return $"in {minutes / 60} h {minutes % 60} min";
            }

            return $"in {minutes / (24 * 60)} d {minutes % (24 * 60) / 60} h";
        }

        private static void SelectChoice<T>(ComboBox combo, T value)
        {
            combo.SelectedItem = combo.Items.Cast<Choice<T>>().First(choice => EqualityComparer<T>.Default.Equals(choice.Value, value));
        }

        private static T SelectedChoice<T>(ComboBox combo) => ((Choice<T>)combo.SelectedItem).Value;
    }
}
