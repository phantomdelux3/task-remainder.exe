using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security;
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
    /// shows marquees when reminders come due. Closing the main window keeps it running; quitting
    /// (Ctrl+Q or the tray menu) and opening the window ask for the app password when one is set.
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
        private PasswordPromptForm? _appPasswordPrompt;
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
            _marquees = new MarqueeManager(_service.VerifyClosePassword);
            _marquees.Finished += OnMarqueeFinished;

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
                    RequestOpenMainForm();
                }
            };

            _schedulerTimer.Tick += (sender, e) => ShowDueReminders();
            SystemEvents.TimeChanged += OnSystemTimeChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionEnded += OnSessionEnded;
            instance.ListenForActivation(() => _uiContext.Post(state => RequestOpenMainForm(), null));

            ApplyStartupDefault();
            RestoreActiveMarquees();
            _schedulerTimer.Start();
            if (!startMinimized)
            {
                RequestOpenMainForm();
            }

            ShowDueReminders();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.TimeChanged -= OnSystemTimeChanged;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.SessionEnded -= OnSessionEnded;
                _schedulerTimer.Dispose();
                _appPasswordPrompt?.Dispose();
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
            var open = menu.Items.Add("Open Marquee Reminder", null, (sender, e) => RequestOpenMainForm());
            open.Font = new Font(open.Font, FontStyle.Bold);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Quit", null, (sender, e) => RequestQuit()) { ShortcutKeyDisplayString = "Ctrl+Q" });
            return menu;
        }

        private static void ApplyStartupDefault()
        {
            try
            {
                StartupRegistration.ApplyDefault();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException || ex is IOException)
            {
                ErrorReporter.Log(ex, "Could not register the app to start with Windows.");
            }
        }

        /// <summary>Brings back marquees that were never closed with their password (e.g. across a restart).</summary>
        private void RestoreActiveMarquees()
        {
            foreach (var active in _service.RestoreActive())
            {
                _marquees.Show(active.Reminder, active.Remaining);
            }
        }

        private void ShowDueReminders()
        {
            foreach (var reminder in _service.CollectDue())
            {
                _marquees.Show(reminder, reminder.Style.DisplayDuration);
            }
        }

        private void OnMarqueeFinished(object sender, MarqueeFinishedEventArgs e)
        {
            try
            {
                _service.MarkFinished(e.ReminderId);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ErrorReporter.Log(ex, "Could not record that a marquee was closed.");
            }
        }

        private void RequestOpenMainForm()
        {
            if (_mainForm != null)
            {
                ShowMainForm();
                return;
            }

            RunWithAppPassword("Open Marquee Reminder", "Enter the app password to open Marquee Reminder.", ShowMainForm);
        }

        private void RequestQuit() =>
            RunWithAppPassword(
                "Quit Marquee Reminder",
                "Enter the app password to quit Marquee Reminder. Reminders will not appear until it runs again.",
                ExitApplication);

        /// <summary>Runs the action straight away if no app password is set; otherwise only after it is entered.</summary>
        private void RunWithAppPassword(string title, string message, Action action)
        {
            if (_exiting)
            {
                return;
            }

            if (!AppPasswordStore.IsSet)
            {
                action();
                return;
            }

            // One password window at a time; asking again just brings it forward.
            if (_appPasswordPrompt != null)
            {
                _appPasswordPrompt.Activate();
                return;
            }

            _appPasswordPrompt = new PasswordPromptForm(title, message, AppPasswordStore.Verify) { Icon = _appIcon };
            _appPasswordPrompt.Verified += (sender, e) => action();
            _appPasswordPrompt.FormClosed += (sender, e) => _appPasswordPrompt = null;
            _appPasswordPrompt.Show();
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
                _mainForm.ExitRequested += (sender, e) => RequestQuit();
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
                    "Your reminders stay active. Click the tray icon to reopen it; press Ctrl+Q in the window to quit.",
                    ToolTipIcon.Info);
            }
        }

        /// <summary>
        /// Quits. Marquees still on screen are closed but stay recorded as active, so they come back
        /// the next time the app starts: quitting is not a way around their passwords.
        /// </summary>
        private void ExitApplication()
        {
            _exiting = true;
            _appPasswordPrompt?.Close();
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

        // Windows is signing out or shutting down, or an installer (Restart Manager) needs the app
        // closed. That cannot be refused, so exit promptly without a password. Marquees still on
        // screen stay recorded as active and come back when the app next starts.
        private void OnSessionEnded(object sender, SessionEndedEventArgs e) =>
            _uiContext.Send(state => ExitApplication(), null);

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                _uiContext.Post(state => ShowDueReminders(), null);
            }
        }
    }
}
