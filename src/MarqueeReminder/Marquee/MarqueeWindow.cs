using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using MarqueeReminder.Core.Models;
using MarqueeReminder.Forms;

namespace MarqueeReminder.Marquee
{
    /// <summary>Why a marquee window went away.</summary>
    internal enum MarqueeEndReason
    {
        /// <summary>Closed by the app (quitting, replaced, reminder deleted, Windows shutting down).</summary>
        Closed,

        /// <summary>Its display time ran out.</summary>
        Expired,

        /// <summary>The user closed it with the password (or clicked a preview).</summary>
        Dismissed,
    }

    /// <summary>
    /// A borderless, always-on-top bar that scrolls a reminder's text. It never steals focus from
    /// the app the user is working in. Clicking it asks for the reminder's password; only the right
    /// password (or the display time running out) closes it. The text size follows the bar's thickness.
    /// </summary>
    internal sealed class MarqueeWindow : Form
    {
        private const int WsExTopmost = 0x00000008;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int FrameIntervalMs = 15;

        private readonly IContainer _components = new Container();
        private readonly string _message;
        private readonly Func<string, bool>? _verifyPassword;
        private readonly SolidBrush _textBrush;
        private readonly StringFormat _textFormat;
        private readonly Stopwatch _elapsed = new Stopwatch();
        private readonly Timer _frameTimer;
        private readonly Timer? _lifetimeTimer;
        private Font? _messageFont;
        private int _fontThickness;
        private SizeF? _textSize;
        private PasswordPromptForm? _prompt;
        private bool _closeAllowed;

        /// <param name="verifyPassword">Checks the close password, or null for a preview that closes on a click.</param>
        public MarqueeWindow(Guid reminderId, string message, MarqueeStyle style, TimeSpan? displayDuration, Func<string, bool>? verifyPassword)
        {
            ReminderId = reminderId;
            Style = style;
            _message = message;
            _verifyPassword = verifyPassword;
            _textBrush = new SolidBrush(style.ForeColor);
            _textFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
            _textFormat.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;

            Text = "Reminder: " + message;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = style.BackColor;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            var toolTip = new ToolTip(_components);
            toolTip.SetToolTip(this, IsPreview ? "Preview. Click to close." : "Click to close (password required)");

            _frameTimer = new Timer(_components) { Interval = FrameIntervalMs };
            _frameTimer.Tick += (sender, e) => Invalidate();

            if (displayDuration.HasValue)
            {
                _lifetimeTimer = new Timer(_components) { Interval = ToTimerInterval(displayDuration.Value) };
                _lifetimeTimer.Tick += (sender, e) => End(MarqueeEndReason.Expired);
            }
        }

        public Guid ReminderId { get; }

        public MarqueeStyle Style { get; }

        /// <summary>Previews close with a plain click and are not recorded as the reminder's marquee.</summary>
        public bool IsPreview => _verifyPassword == null;

        public MarqueeEndReason EndReason { get; private set; } = MarqueeEndReason.Closed;

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExTopmost | WsExToolWindow | WsExNoActivate;
                return parameters;
            }
        }

        /// <summary>Closes the bar, recording why. The only way the app closes a bar.</summary>
        public void End(MarqueeEndReason reason)
        {
            EndReason = reason;
            _closeAllowed = true;
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _elapsed.Start();
            _frameTimer.Start();
            _lifetimeTimer?.Start();
        }

        // Refuse any close the app did not ask for (e.g. Alt+F4). Windows shutting down, or the
        // app exiting, still closes the bar; it is then shown again on the next start.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_closeAllowed)
            {
                e.Cancel = true;
            }

            base.OnFormClosing(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (_verifyPassword == null)
            {
                End(MarqueeEndReason.Dismissed);
            }
            else
            {
                ShowPasswordPrompt(_verifyPassword);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Paint in a virtual horizontal space: x runs along the bar, y across it.
            var length = Style.IsVertical ? ClientSize.Height : ClientSize.Width;
            var thickness = Style.IsVertical ? ClientSize.Width : ClientSize.Height;
            if (length <= 0 || thickness <= 0)
            {
                return;
            }

            var graphics = e.Graphics;
            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            var font = FontFor(thickness);
            RotateForEdge(graphics);

            var textSize = _textSize ?? (_textSize = graphics.MeasureString(_message, font, PointF.Empty, _textFormat)).Value;

            // Text enters from the far end, travels the full bar, then starts over. Deriving the
            // position from elapsed time (not frame count) keeps the speed steady even if frames drop.
            var cycleLength = length + textSize.Width;
            var travelled = (float)(_elapsed.Elapsed.TotalSeconds * Style.ScrollSpeed % cycleLength);
            var x = length - travelled;
            var y = (thickness - textSize.Height) / 2f;

            graphics.DrawString(_message, font, _textBrush, x, y, _textFormat);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _components.Dispose();
                _messageFont?.Dispose();
                _textBrush.Dispose();
                _textFormat.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ShowPasswordPrompt(Func<string, bool> verify)
        {
            if (_prompt != null)
            {
                _prompt.Activate();
                return;
            }

            _prompt = new PasswordPromptForm("Close reminder", $"Enter the password to close this reminder:\n\n{_message}", verify);
            _prompt.Verified += (sender, e) => End(MarqueeEndReason.Dismissed);
            _prompt.FormClosed += (sender, e) => _prompt = null;

            // Owned by the bar: it stays above it and closes with it (e.g. when the time runs out).
            _prompt.Show(this);
        }

        /// <summary>The text font for the current thickness, recreated only when the bar is resized.</summary>
        private Font FontFor(int thickness)
        {
            if (_messageFont == null || _fontThickness != thickness)
            {
                _messageFont?.Dispose();
                _messageFont = MarqueeFonts.CreateFitted(Style, thickness);
                _fontThickness = thickness;
                _textSize = null;
            }

            return _messageFont;
        }

        /// <summary>Left bars read bottom-to-top, right bars top-to-bottom, like book spines.</summary>
        private void RotateForEdge(Graphics graphics)
        {
            switch (Style.Position)
            {
                case MarqueePosition.Left:
                    graphics.TranslateTransform(0, ClientSize.Height);
                    graphics.RotateTransform(-90);
                    break;
                case MarqueePosition.Right:
                    graphics.TranslateTransform(ClientSize.Width, 0);
                    graphics.RotateTransform(90);
                    break;
            }
        }

        private static int ToTimerInterval(TimeSpan duration) =>
            (int)Math.Max(1, Math.Min(int.MaxValue, duration.TotalMilliseconds));
    }
}
