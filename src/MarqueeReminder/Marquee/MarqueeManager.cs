using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MarqueeReminder.Core.Layout;
using MarqueeReminder.Core.Models;
using Microsoft.Win32;

namespace MarqueeReminder.Marquee
{
    public sealed class MarqueeFinishedEventArgs : EventArgs
    {
        public MarqueeFinishedEventArgs(Guid reminderId)
        {
            ReminderId = reminderId;
        }

        public Guid ReminderId { get; }
    }

    /// <summary>
    /// Shows marquee windows on the primary screen and keeps them laid out. Bars that share an
    /// edge are stacked inward so they never cover each other. A bar the user dragged stays where it
    /// was put, always fully on screen.
    /// </summary>
    internal sealed class MarqueeManager : IDisposable
    {
        private readonly List<MarqueeWindow> _windows = new List<MarqueeWindow>();
        private readonly SynchronizationContext _uiContext;
        private readonly Func<Guid, string, bool> _verifyClosePassword;

        /// <param name="verifyClosePassword">Checks a password against a reminder's current close password.</param>
        public MarqueeManager(Func<Guid, string, bool> verifyClosePassword)
        {
            _verifyClosePassword = verifyClosePassword ?? throw new ArgumentNullException(nameof(verifyClosePassword));
            _uiContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("MarqueeManager must be created on the UI thread.");
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        /// <summary>
        /// Raised when a reminder's marquee is gone for good: closed with its password or its time
        /// ran out. Not raised for previews, or when the app closes a bar (e.g. on exit).
        /// </summary>
        public event EventHandler<MarqueeFinishedEventArgs>? Finished;

        /// <summary>Shows the reminder's marquee, replacing one already showing for it.</summary>
        /// <param name="duration">How long it stays, or null until closed with the password.</param>
        public void Show(Reminder reminder, TimeSpan? duration)
        {
            var id = reminder.Id;
            Open(reminder, duration, password => _verifyClosePassword(id, password));
        }

        /// <summary>Shows a short preview that closes on a click. Never affects the reminder's real marquee.</summary>
        public void ShowPreview(Reminder reminder, TimeSpan duration) => Open(reminder, duration, verifyPassword: null);

        /// <summary>Closes the marquees (and previews) of the given reminders, e.g. when they are deleted.</summary>
        public void Close(IEnumerable<Guid> reminderIds)
        {
            var ids = new HashSet<Guid>(reminderIds);
            foreach (var window in _windows.Where(w => ids.Contains(w.ReminderId)).ToArray())
            {
                window.End(MarqueeEndReason.Closed);
            }
        }

        public void CloseAll()
        {
            foreach (var window in _windows.ToArray())
            {
                window.End(MarqueeEndReason.Closed);
            }
        }

        public void Dispose()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            CloseAll();
        }

        private void Open(Reminder reminder, TimeSpan? duration, Func<string, bool>? verifyPassword)
        {
            var isPreview = verifyPassword == null;
            _windows.FirstOrDefault(w => w.ReminderId == reminder.Id && w.IsPreview == isPreview)?.End(MarqueeEndReason.Closed);

            var window = new MarqueeWindow(reminder.Id, reminder.Text, reminder.Style.Clone(), duration, verifyPassword);
            window.FormClosed += OnWindowClosed;
            window.PlacementChanged += OnPlacementChanged;
            _windows.Add(window);

            ArrangeWindows();
            window.Show();
        }

        private void OnWindowClosed(object sender, FormClosedEventArgs e)
        {
            var window = (MarqueeWindow)sender;
            window.FormClosed -= OnWindowClosed;
            window.PlacementChanged -= OnPlacementChanged;
            _windows.Remove(window);
            ArrangeWindows();

            if (!window.IsPreview && window.EndReason != MarqueeEndReason.Closed)
            {
                Finished?.Invoke(this, new MarqueeFinishedEventArgs(window.ReminderId));
            }
        }

        private void OnPlacementChanged(object sender, EventArgs e) => ArrangeWindows();

        // Raised on a system-events thread; hop back to the UI thread.
        private void OnDisplaySettingsChanged(object sender, EventArgs e) =>
            _uiContext.Post(state => ArrangeWindows(), null);

        private void ArrangeWindows()
        {
            var workArea = Screen.PrimaryScreen.WorkingArea;
            var scale = GetDpiScale();

            foreach (var window in _windows.Where(w => w.DraggedTo.HasValue))
            {
                window.Bounds = MarqueeLayout.Place(workArea, window.Style.IsVertical, Scale(window.Style.Thickness, scale), window.DraggedTo!.Value);
            }

            foreach (var edge in _windows.Where(w => !w.DraggedTo.HasValue).GroupBy(w => w.Style.Position))
            {
                var stackOffset = 0;
                foreach (var window in edge)
                {
                    var thickness = Scale(window.Style.Thickness, scale);
                    window.Bounds = MarqueeLayout.Compute(workArea, edge.Key, thickness, Scale(window.Style.Offset, scale), stackOffset);
                    stackOffset += thickness;
                }
            }
        }

        /// <summary>Sizes are entered at 100% scaling; grow them to match the display's scaling.</summary>
        private static float GetDpiScale()
        {
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                return graphics.DpiX / 96f;
            }
        }

        private static int Scale(int pixels, float scale) => (int)Math.Round(pixels * scale);
    }
}
