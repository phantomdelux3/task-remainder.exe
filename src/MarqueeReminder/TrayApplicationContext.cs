using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using MarqueeReminder.Core;
using MarqueeReminder.Core.Scheduling;
using MarqueeReminder.Core.Storage;
using MarqueeReminder.Forms;
using MarqueeReminder.Infrastructure;
using MarqueeReminder.Marquee;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace MarqueeReminder
{
    /// <summary>
    /// The app's lifetime: lives in the notification area, checks the schedule twice a second and
    /// shows marquees when reminders come due. Closing the main window keeps it running.
    /// </summary>
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private const int SchedulerIntervalMs = 500;

        private readonly SynchronizationContext _uiContext;
        private readonly ReminderService _service;
        private readonly MarqueeManager _marquees;
        private readonly Icon _appIcon;
        private readonly NotifyIcon _trayIcon;
        private readonly Timer _schedulerTimer = new Timer { Interval = SchedulerIntervalMs };

        private MainForm? _mainForm;
        private bool _trayHintShown;
        private bool _exiting;

        public TrayApplicationContext(SingleInstance instance, bool startMinimized)
        {
            _uiContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("The tray context must be created on the UI thread.");

            _service = new ReminderService(
                new JsonReminderStore(AppPaths.RemindersFile),
                new SystemClock(),
                new ReminderScheduler(ReminderScheduler.DefaultMissedGracePeriod));
            _service.SaveFailed += (sender, e) => ErrorReporter.Log(e.Exception, "Could not record a fired reminder.");
            _marquees = new MarqueeManager();

            _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
            _trayIcon = new NotifyIcon
            {
                Icon = _appIcon,
                Text = "Marquee Reminder",
                ContextMenuStrip = BuildTrayMenu(),
                Visible = true,
            };
            _trayIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowMainForm();
                }
            };

            _schedulerTimer.Tick += (sender, e) => ShowDueReminders();
            SystemEvents.TimeChanged += OnSystemTimeChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            instance.ListenForActivation(() => _uiContext.Post(state => ShowMainForm(), null));

            _schedulerTimer.Start();
            if (!startMinimized)
            {
                ShowMainForm();
            }

            ShowDueReminders();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.TimeChanged -= OnSystemTimeChanged;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                _schedulerTimer.Dispose();
                _mainForm?.Dispose();
                _marquees.Dispose();
                _trayIcon.Visible = false;
                _trayIcon.ContextMenuStrip?.Dispose();
                _trayIcon.Dispose();
                _appIcon.Dispose();
            }

            base.Dispose(disposing);
        }

        private ContextMenuStrip BuildTrayMenu()
        {
            var menu = new ContextMenuStrip();
            var open = menu.Items.Add("Open Marquee Reminder", null, (sender, e) => ShowMainForm());
            open.Font = new Font(open.Font, FontStyle.Bold);
            menu.Items.Add("Dismiss all marquees", null, (sender, e) => _marquees.CloseAll());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (sender, e) => ExitApplication());
            return menu;
        }

        private void ShowDueReminders()
        {
            foreach (var reminder in _service.CollectDue())
            {
                _marquees.Show(reminder);
            }
        }

        private void ShowMainForm()
        {
            if (_exiting)
            {
                return;
            }

            if (_mainForm == null)
            {
                _mainForm = new MainForm(_service, _marquees, _appIcon);
                _mainForm.FormClosed += OnMainFormClosed;
                _mainForm.Show();
            }

            if (_mainForm.WindowState == FormWindowState.Minimized)
            {
                _mainForm.WindowState = FormWindowState.Normal;
            }

            _mainForm.Activate();
        }

        private void OnMainFormClosed(object sender, FormClosedEventArgs e)
        {
            _mainForm = null;
            if (!_exiting && !_trayHintShown)
            {
                _trayHintShown = true;
                _trayIcon.ShowBalloonTip(
                    3000,
                    "Marquee Reminder is still running",
                    "Your reminders stay active. Use the tray icon to open the app or exit.",
                    ToolTipIcon.Info);
            }
        }

        private void ExitApplication()
        {
            _exiting = true;
            _mainForm?.Close();
            _marquees.CloseAll();
            ExitThread();
        }

        // Windows caches the time zone; drop the cache so DateTime.Now reflects the change.
        private void OnSystemTimeChanged(object sender, EventArgs e)
        {
            TimeZoneInfo.ClearCachedData();
            CultureInfo.CurrentCulture.ClearCachedData();
            _uiContext.Post(state =>
            {
                ShowDueReminders();
                _mainForm?.RefreshList();
            }, null);
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                _uiContext.Post(state => ShowDueReminders(), null);
            }
        }
    }
}
