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
    /// Click-hold-drag moves the bar across the screen; right-click switches it between horizontal and vertical.
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
        private Point? _dragAnchor;
        private int _dragStartEdge;
        private bool _dragging;
        private bool _suppressClick;

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
            toolTip.SetToolTip(this, (IsPreview ? "Preview. Click to close." : "Click to close (password required).")
                + " Drag to move, right-click to turn.");
            ContextMenuStrip = BuildOrientationMenu();

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

        /// <summary>
        /// Where the user moved the bar: its wanted top (horizontal) or left (vertical) edge in screen
        /// pixels, or null while it still sits at its edge with the other bars there.
        /// </summary>
        public int? DraggedTo { get; private set; }

        /// <summary>Raised when the user moves or turns the bar, so it can be laid out again.</summary>
        public event EventHandler? PlacementChanged;

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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragAnchor = Cursor.Position;
                _dragStartEdge = Style.IsVertical ? Left : Top;
                _dragging = false;
                _suppressClick = false;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragAnchor == null || (MouseButtons & MouseButtons.Left) == 0)
            {
                return;
            }

            var cursor = Cursor.Position;
            var anchor = _dragAnchor.Value;
            if (!_dragging)
            {
                // A small wobble while clicking is still a click, not a drag.
                var dragSize = SystemInformation.DragSize;
                if (Math.Abs(cursor.X - anchor.X) < dragSize.Width && Math.Abs(cursor.Y - anchor.Y) < dragSize.Height)
                {
                    return;
                }

                _dragging = true;
                Cursor = Style.IsVertical ? Cursors.SizeWE : Cursors.SizeNS;
            }

            var moved = Style.IsVertical ? cursor.X - anchor.X : cursor.Y - anchor.Y;
            MoveTo(Style.Position, _dragStartEdge + moved);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                // The click that ends a drag must not open the password prompt.
                _suppressClick = _dragging;
                _dragAnchor = null;
                _dragging = false;
                Cursor = Cursors.Hand;
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left || _suppressClick)
            {
                _suppressClick = false;
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

        private ContextMenuStrip BuildOrientationMenu()
        {
            var horizontal = new ToolStripMenuItem("Horizontal");
            var vertical = new ToolStripMenuItem("Vertical");
            var menu = new ContextMenuStrip(_components);
            menu.Items.AddRange(new ToolStripItem[] { horizontal, vertical });

            // Remember where the menu was opened: the turned bar goes through that point.
            var openedAt = Point.Empty;
            menu.Opening += (sender, e) =>
            {
                openedAt = Cursor.Position;
                horizontal.Checked = !Style.IsVertical;
                vertical.Checked = Style.IsVertical;
            };
            horizontal.Click += (sender, e) => Turn(vertical: false, openedAt);
            vertical.Click += (sender, e) => Turn(vertical: true, openedAt);
            return menu;
        }

        /// <summary>
        /// Turns the bar, keeping its thickness and centring it on <paramref name="through"/>. It reads
        /// like a bar on the nearer screen edge (e.g. bottom-to-top on the left half).
        /// </summary>
        private void Turn(bool vertical, Point through)
        {
            if (vertical == Style.IsVertical)
            {
                return;
            }

            var thickness = Style.IsVertical ? Width : Height;
            var screen = Screen.FromPoint(through).WorkingArea;
            if (vertical)
            {
                var side = through.X < screen.Left + screen.Width / 2 ? MarqueePosition.Left : MarqueePosition.Right;
                MoveTo(side, through.X - thickness / 2);
            }
            else
            {
                var side = through.Y < screen.Top + screen.Height / 2 ? MarqueePosition.Top : MarqueePosition.Bottom;
                MoveTo(side, through.Y - thickness / 2);
            }
        }

        private void MoveTo(MarqueePosition position, int edge)
        {
            Style.Position = position;
            DraggedTo = edge;
            PlacementChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
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
