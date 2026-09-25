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
    /// <summary>
    /// Shows marquee windows on the primary screen and keeps them laid out. Bars that share an
    /// edge are stacked inward so they never cover each other.
    /// </summary>
    internal sealed class MarqueeManager : IDisposable
    {
        private readonly List<MarqueeWindow> _windows = new List<MarqueeWindow>();
        private readonly SynchronizationContext _uiContext;

        public MarqueeManager()
        {
            _uiContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException("MarqueeManager must be created on the UI thread.");
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        /// <summary>
        /// Shows the reminder, replacing any marquee already showing for it.
        /// </summary>
        /// <param name="durationOverride">Display time to use instead of the reminder's own setting (e.g. for previews).</param>
        public void Show(Reminder reminder, TimeSpan? durationOverride = null)
        {
            _windows.FirstOrDefault(w => w.ReminderId == reminder.Id)?.Close();

            var duration = durationOverride
                ?? (reminder.Style.DisplayMinutes > 0 ? TimeSpan.FromMinutes(reminder.Style.DisplayMinutes) : (TimeSpan?)null);
            var window = new MarqueeWindow(reminder.Id, reminder.Text, reminder.Style.Clone(), duration);
            window.FormClosed += OnWindowClosed;
            _windows.Add(window);

            ArrangeWindows();
            window.Show();
        }

        public void Close(IEnumerable<Guid> reminderIds)
        {
            var ids = new HashSet<Guid>(reminderIds);
            foreach (var window in _windows.Where(w => ids.Contains(w.ReminderId)).ToArray())
            {
                window.Close();
            }
        }

        public void CloseAll()
        {
            foreach (var window in _windows.ToArray())
            {
                window.Close();
            }
        }

        public void Dispose()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            CloseAll();
        }

        private void OnWindowClosed(object sender, FormClosedEventArgs e)
        {
            var window = (MarqueeWindow)sender;
            window.FormClosed -= OnWindowClosed;
            _windows.Remove(window);
            ArrangeWindows();
        }

        // Raised on a system-events thread; hop back to the UI thread.
        private void OnDisplaySettingsChanged(object sender, EventArgs e) =>
            _uiContext.Post(state => ArrangeWindows(), null);

        private void ArrangeWindows()
        {
            var workArea = Screen.PrimaryScreen.WorkingArea;
            var scale = GetDpiScale();

            foreach (var edge in _windows.GroupBy(w => w.Style.Position))
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
