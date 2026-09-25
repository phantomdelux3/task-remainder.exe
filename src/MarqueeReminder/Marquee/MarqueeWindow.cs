using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Marquee
{
    /// <summary>
    /// A borderless, always-on-top bar that scrolls a reminder's text. It never steals focus
    /// from the app the user is working in. Double-click (or right-click → Dismiss) closes it.
    /// </summary>
    internal sealed class MarqueeWindow : Form
    {
        private const int WsExTopmost = 0x00000008;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const int FrameIntervalMs = 15;

        private readonly IContainer _components = new Container();
        private readonly string _message;
        private readonly Font _messageFont;
        private readonly SolidBrush _textBrush;
        private readonly StringFormat _textFormat;
        private readonly Stopwatch _elapsed = new Stopwatch();
        private readonly Timer _frameTimer;
        private readonly Timer? _lifetimeTimer;
        private SizeF? _textSize;

        public MarqueeWindow(Guid reminderId, string message, MarqueeStyle style, TimeSpan? displayDuration)
        {
            ReminderId = reminderId;
            Style = style;
            _message = message;

            var fontStyle = (style.Bold ? FontStyle.Bold : FontStyle.Regular) | (style.Italic ? FontStyle.Italic : FontStyle.Regular);
            _messageFont = new Font(style.FontFamily, style.FontSize, fontStyle, GraphicsUnit.Point);
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
            toolTip.SetToolTip(this, "Double-click to dismiss");

            var menu = new ContextMenuStrip(_components);
            menu.Items.Add("Dismiss", null, (sender, e) => Close());
            ContextMenuStrip = menu;

            _frameTimer = new Timer(_components) { Interval = FrameIntervalMs };
            _frameTimer.Tick += (sender, e) => Invalidate();

            if (displayDuration.HasValue)
            {
                _lifetimeTimer = new Timer(_components) { Interval = ToTimerInterval(displayDuration.Value) };
                _lifetimeTimer.Tick += (sender, e) => Close();
            }
        }

        public Guid ReminderId { get; }

        public MarqueeStyle Style { get; }

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

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _elapsed.Start();
            _frameTimer.Start();
            _lifetimeTimer?.Start();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var graphics = e.Graphics;
            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            // Paint in a virtual horizontal space: x runs along the bar, y across it.
            var length = Style.IsVertical ? ClientSize.Height : ClientSize.Width;
            var thickness = Style.IsVertical ? ClientSize.Width : ClientSize.Height;
            RotateForEdge(graphics);

            var textSize = _textSize ?? (_textSize = graphics.MeasureString(_message, _messageFont, PointF.Empty, _textFormat)).Value;

            // Text enters from the far end, travels the full bar, then starts over. Deriving the
            // position from elapsed time (not frame count) keeps the speed steady even if frames drop.
            var cycleLength = length + textSize.Width;
            var travelled = (float)(_elapsed.Elapsed.TotalSeconds * Style.ScrollSpeed % cycleLength);
            var x = length - travelled;
            var y = (thickness - textSize.Height) / 2f;

            graphics.DrawString(_message, _messageFont, _textBrush, x, y, _textFormat);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _components.Dispose();
                _messageFont.Dispose();
                _textBrush.Dispose();
                _textFormat.Dispose();
            }

            base.Dispose(disposing);
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
