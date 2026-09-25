using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows.Forms;
using MarqueeReminder.Core;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Core.Scheduling;
using MarqueeReminder.Infrastructure;
using MarqueeReminder.Marquee;

namespace MarqueeReminder.Forms
{
    /// <summary>Lists all reminders and lets the user add, edit, delete, pause and preview them.</summary>
    internal sealed class MainForm : Form
    {
        private static readonly TimeSpan PreviewDuration = TimeSpan.FromSeconds(10);

        private readonly ReminderService _service;
        private readonly MarqueeManager _marquees;
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

        private readonly ListView _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = true,
            CheckBoxes = true,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };

        private readonly Label _emptyHint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText,
            Text = "No reminders yet.\nClick \"New reminder\" to create one.",
        };

        private readonly ToolStripButton _editButton = new ToolStripButton("Edit");
        private readonly ToolStripButton _deleteButton = new ToolStripButton("Delete");
        private readonly ToolStripButton _previewButton = new ToolStripButton("Preview");
        private readonly SettingsPanel _settings = new SettingsPanel();
        private readonly ToolStripStatusLabel _summaryLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ToolStripStatusLabel _clockLabel = new ToolStripStatusLabel();
        private readonly Timer _clockTimer = new Timer { Interval = 1000 };

        private bool _populating;
        private bool _suppressCheckToggle;
        private DateTime _lastRefreshMinute;

        public MainForm(ReminderService service, MarqueeManager marquees, Icon icon)
        {
            _service = service;
            _marquees = marquees;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Marquee Reminder";
            Icon = icon;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(900, 440);
            MinimumSize = new Size(640, 300);
            StartPosition = FormStartPosition.CenterScreen;

            _list.Columns.Add("Reminder", 280);
            _list.Columns.Add("Schedule", 250);
            _list.Columns.Add("Next run", 190);
            _list.Columns.Add("Position", 130);
            _list.Controls.Add(_emptyHint);

            var remindersPage = new TabPage("Reminders");
            remindersPage.Controls.Add(_list);
            remindersPage.Controls.Add(BuildToolStrip());
            var settingsPage = new TabPage("Settings");
            settingsPage.Controls.Add(_settings);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(remindersPage);
            tabs.TabPages.Add(settingsPage);

            Controls.Add(tabs);
            Controls.Add(new StatusStrip { Items = { _summaryLabel, _clockLabel }, SizingGrip = false });
            ResumeLayout(false);
            PerformLayout();

            WireEvents();
        }

        /// <summary>Raised when the user asks to quit the app completely (Ctrl+Q).</summary>
        public event EventHandler? ExitRequested;

        /// <summary>Reloads the list, keeping the current selection.</summary>
        public void RefreshList()
        {
            var selectedIds = new HashSet<Guid>(SelectedReminders.Select(r => r.Id));
            var reminders = _service.GetAll();

            _populating = true;
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                foreach (var reminder in reminders)
                {
                    var item = new ListViewItem(reminder.Text) { Checked = reminder.IsEnabled, Tag = reminder };
                    item.SubItems.Add(ScheduleDescriber.Describe(reminder.Schedule, _culture));
                    item.SubItems.Add(DescribeNextRun(reminder));
                    item.SubItems.Add(DescribePosition(reminder.Style));
                    item.Selected = selectedIds.Contains(reminder.Id);
                    _list.Items.Add(item);
                }
            }
            finally
            {
                _list.EndUpdate();
                _populating = false;
            }

            var active = reminders.Count(r => r.IsEnabled);
            _summaryLabel.Text = $"{reminders.Count} reminder(s), {active} active  ·  Closing this window keeps reminders running. Ctrl+Q quits.";
            _emptyHint.Visible = reminders.Count == 0;
            _lastRefreshMinute = TruncateToMinute(DateTime.Now);
            UpdateCommandState();
        }

        // Populate only once the ListView's native handle exists: creating the handle re-applies
        // checkbox states and raises ItemChecked, which would otherwise read as the user unticking items.
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RefreshList();
            UpdateClock();
            _clockTimer.Start();
        }

        // Settings live outside this window (registry), so re-read them whenever it comes to the front.
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _settings.RefreshState();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Q))
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _service.Changed -= OnServiceChanged;
                _clockTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        private IReadOnlyList<Reminder> SelectedReminders =>
            _list.SelectedItems.Cast<ListViewItem>().Select(item => (Reminder)item.Tag).ToList();

        /// <summary>The selected reminder when exactly one is selected (editing works on one at a time).</summary>
        private Reminder? SingleSelectedReminder
        {
            get
            {
                var selected = SelectedReminders;
                return selected.Count == 1 ? selected[0] : null;
            }
        }

        private ToolStrip BuildToolStrip()
        {
            var newButton = new ToolStripButton("New reminder");
            newButton.Click += (sender, e) => AddReminder();
            _editButton.Click += (sender, e) => EditSelected();
            _deleteButton.Click += (sender, e) => DeleteSelected();
            _previewButton.Click += (sender, e) => PreviewSelected();

            return new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(6, 2, 6, 2),
                Items = { newButton, _editButton, _deleteButton, new ToolStripSeparator(), _previewButton },
            };
        }

        private void WireEvents()
        {
            _service.Changed += OnServiceChanged;
            _clockTimer.Tick += (sender, e) => UpdateClock();
            _list.SelectedIndexChanged += (sender, e) => UpdateCommandState();
            _list.ItemActivate += (sender, e) => EditSelected();
            _list.ItemCheck += OnItemCheck;
            _list.ItemChecked += OnItemChecked;
            _list.MouseDown += (sender, e) => _suppressCheckToggle = e.Clicks > 1;
            _list.MouseUp += (sender, e) => _suppressCheckToggle = false;
            _list.KeyDown += OnListKeyDown;
            _emptyHint.DoubleClick += (sender, e) => AddReminder();
        }

        private void OnServiceChanged(object sender, EventArgs e) => RefreshList();

        // A ListView with checkboxes toggles the check on double-click; double-click means "edit" here.
        private void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_suppressCheckToggle && !_populating)
            {
                e.NewValue = e.CurrentValue;
            }
        }

        private void OnItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_populating || !_list.Created || !(e.Item.Tag is Reminder reminder) || reminder.IsEnabled == e.Item.Checked)
            {
                return;
            }

            var id = reminder.Id;
            var enabled = e.Item.Checked;

            // Rebuilding the list from inside its own ItemChecked event is unsafe; defer it.
            BeginInvoke((Action)(() => RunSafely(() => _service.SetEnabled(id, enabled), "Could not update the reminder.")));
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.A && e.Control)
            {
                SelectAll();
                e.Handled = true;
            }
        }

        private void AddReminder()
        {
            using (var editor = new ReminderEditorForm(null, _marquees))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    RunSafely(() => _service.Add(editor.Result), "Could not save the reminder.");
                }
            }
        }

        private void EditSelected()
        {
            var reminder = SingleSelectedReminder;
            if (reminder == null)
            {
                return;
            }

            using (var editor = new ReminderEditorForm(reminder, _marquees))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    RunSafely(() => _service.Update(editor.Result), "Could not save the reminder.");
                }
            }
        }

        private void DeleteSelected()
        {
            var selected = SelectedReminders;
            if (selected.Count == 0)
            {
                return;
            }

            var question = selected.Count == 1
                ? $"Delete the reminder \"{selected[0].Text}\"?"
                : $"Delete these {selected.Count} reminders?";
            var answer = MessageBox.Show(
                this,
                question,
                selected.Count == 1 ? "Delete reminder" : "Delete reminders",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes)
            {
                var ids = selected.Select(r => r.Id).ToList();
                RunSafely(() => _service.RemoveAll(ids), "Could not delete the reminders.");
                _marquees.Close(ids);
            }
        }

        private void PreviewSelected()
        {
            foreach (var reminder in SelectedReminders)
            {
                _marquees.ShowPreview(reminder, PreviewDuration);
            }
        }

        private void SelectAll()
        {
            _list.BeginUpdate();
            foreach (ListViewItem item in _list.Items)
            {
                item.Selected = true;
            }

            _list.EndUpdate();
        }

        private void UpdateCommandState()
        {
            var count = _list.SelectedItems.Count;
            _editButton.Enabled = count == 1;
            _deleteButton.Enabled = count > 0;
            _deleteButton.Text = count > 1 ? $"Delete ({count})" : "Delete";
            _previewButton.Enabled = count > 0;
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            _clockLabel.Text = $"Now: {ScheduleDescriber.FormatDate(now, _culture)}, {now.ToString(_culture.DateTimeFormat.LongTimePattern, _culture)}";

            // "Next run" values move on as time passes; refresh them once a minute.
            if (TruncateToMinute(now) != _lastRefreshMinute)
            {
                RefreshList();
            }
        }

        private string DescribeNextRun(Reminder reminder)
        {
            if (!reminder.IsEnabled)
            {
                return "Paused";
            }

            var next = _service.GetNextRun(reminder);
            return next.HasValue ? ScheduleDescriber.FormatDateTime(next.Value, _culture) : "No upcoming run";
        }

        private static string DescribePosition(MarqueeStyle style)
        {
            if (style.Offset == 0)
            {
                return style.Position.ToString();
            }

            var sign = style.Offset > 0 ? "+" : "−";
            return $"{style.Position} ({sign}{Math.Abs(style.Offset)} px)";
        }

        private void RunSafely(Action action, string failureMessage)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException || ex is ArgumentException)
            {
                ErrorReporter.Report(ex, failureMessage);
            }
        }

        private static DateTime TruncateToMinute(DateTime value) =>
            new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);
    }
}
