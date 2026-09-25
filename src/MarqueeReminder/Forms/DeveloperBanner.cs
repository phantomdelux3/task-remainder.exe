using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MarqueeReminder.Infrastructure;

namespace MarqueeReminder.Forms
{
    /// <summary>
    /// The developer credit strip at the top of the main window: "Developer: deadpool1555", with the
    /// name in gold, a shine sweeping across it and sparkles twinkling around it.
    /// </summary>
    internal sealed class DeveloperBanner : Control
    {
        private const string Caption = "Developer:";

        private const int FrameIntervalMs = 33;
        private const float ShineCycleSeconds = 2.6f;
        private const int SparkleCount = 12;

        private static readonly Color Background = Color.FromArgb(0x11, 0x18, 0x27);
        private static readonly Color BackgroundEdge = Color.FromArgb(0x1F, 0x29, 0x37);
        private static readonly Color CaptionColor = Color.FromArgb(0xD1, 0xD5, 0xDB);
        private static readonly Color DarkGold = Color.FromArgb(0xB8, 0x86, 0x0B);
        private static readonly Color Gold = Color.FromArgb(0xFF, 0xD7, 0x00);
        private static readonly Color PaleGold = Color.FromArgb(0xFF, 0xF8, 0xDC);

        private readonly Timer _frameTimer = new Timer { Interval = FrameIntervalMs };
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly Random _random = new Random();
        private readonly Sparkle[] _sparkles = new Sparkle[SparkleCount];
        private readonly Font _captionFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        private readonly Font _nameFont = new Font("Segoe UI", 12f, FontStyle.Bold);
        private RectangleF _nameBounds;

        public DeveloperBanner()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = 38;
            TabStop = false;
            AccessibleName = $"{Caption} {AppInfo.DeveloperName}";
            AccessibleRole = AccessibleRole.StaticText;

            for (var i = 0; i < _sparkles.Length; i++)
            {
                _sparkles[i] = new Sparkle { Phase = (float)_random.NextDouble() };
            }

            _frameTimer.Tick += (sender, e) => Invalidate();
        }

        // Only animate while actually visible, so a hidden or minimized window costs nothing.
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            _frameTimer.Enabled = Visible;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var background = new LinearGradientBrush(ClientRectangle, BackgroundEdge, Background, LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(background, ClientRectangle);
            }

            var seconds = (float)_clock.Elapsed.TotalSeconds;
            var captionSize = graphics.MeasureString(Caption, _captionFont);
            using (var namePath = new GraphicsPath())
            {
                // The name is drawn as a shape so it can be filled with a moving gold gradient.
                var emSize = graphics.DpiY * _nameFont.SizeInPoints / 72f;
                namePath.AddString(AppInfo.DeveloperName, _nameFont.FontFamily, (int)_nameFont.Style, emSize, PointF.Empty, StringFormat.GenericTypographic);
                var raw = namePath.GetBounds();

                var gap = captionSize.Height * 0.35f;
                var totalWidth = captionSize.Width + gap + raw.Width;
                var left = (ClientSize.Width - totalWidth) / 2f;
                var nameLeft = left + captionSize.Width + gap;
                var nameTop = (ClientSize.Height - raw.Height) / 2f;

                using (var move = new Matrix())
                {
                    move.Translate(nameLeft - raw.Left, nameTop - raw.Top);
                    namePath.Transform(move);
                }

                _nameBounds = namePath.GetBounds();

                using (var captionBrush = new SolidBrush(CaptionColor))
                {
                    graphics.DrawString(Caption, _captionFont, captionBrush, left, (ClientSize.Height - captionSize.Height) / 2f);
                }

                using (var glow = new Pen(Color.FromArgb(70, Gold), 4f) { LineJoin = LineJoin.Round })
                {
                    graphics.DrawPath(glow, namePath);
                }

                using (var goldBrush = CreateShineBrush(_nameBounds, seconds))
                {
                    graphics.FillPath(goldBrush, namePath);
                }

                using (var outline = new Pen(Color.FromArgb(160, DarkGold), 0.8f))
                {
                    graphics.DrawPath(outline, namePath);
                }
            }

            DrawSparkles(graphics, seconds);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _frameTimer.Dispose();
                _captionFont.Dispose();
                _nameFont.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>A gold gradient with a bright band that sweeps across the name and repeats.</summary>
        private static LinearGradientBrush CreateShineBrush(RectangleF bounds, float seconds)
        {
            var bandWidth = Math.Max(1f, bounds.Width);
            var brush = new LinearGradientBrush(new RectangleF(0, 0, bandWidth, 1), DarkGold, DarkGold, LinearGradientMode.Horizontal)
            {
                InterpolationColors = new ColorBlend
                {
                    Colors = new[] { DarkGold, Gold, Gold, PaleGold, Gold, Gold, DarkGold },
                    Positions = new[] { 0f, 0.2f, 0.42f, 0.5f, 0.58f, 0.8f, 1f },
                },
                WrapMode = WrapMode.Tile,
            };

            var progress = seconds % ShineCycleSeconds / ShineCycleSeconds;
            brush.TranslateTransform(bounds.Left + (progress - 0.5f) * bandWidth, 0);
            return brush;
        }

        private void DrawSparkles(Graphics graphics, float seconds)
        {
            if (_nameBounds.Width <= 0)
            {
                return;
            }

            // Sparkles live in a region a little larger than the name.
            var area = RectangleF.Inflate(_nameBounds, _nameBounds.Height * 0.6f, _nameBounds.Height * 0.25f);
            foreach (var sparkle in _sparkles)
            {
                var cycle = (seconds / sparkle.Period + sparkle.Phase) % 1f;
                if (cycle < sparkle.LastCycle || sparkle.Size <= 0)
                {
                    Respawn(sparkle);
                }

                sparkle.LastCycle = cycle;

                // Fade in and out over each cycle.
                var twinkle = (float)Math.Sin(cycle * Math.PI);
                var center = new PointF(area.Left + sparkle.X * area.Width, area.Top + sparkle.Y * area.Height);
                DrawStar(graphics, center, sparkle.Size * (0.4f + 0.6f * twinkle), (int)(255 * twinkle));
            }
        }

        private void Respawn(Sparkle sparkle)
        {
            sparkle.X = (float)_random.NextDouble();
            sparkle.Y = (float)_random.NextDouble();
            sparkle.Size = 3f + (float)_random.NextDouble() * 4f;
            sparkle.Period = 0.9f + (float)_random.NextDouble() * 1.4f;
        }

        /// <summary>A four-pointed twinkle star.</summary>
        private static void DrawStar(Graphics graphics, PointF center, float radius, int alpha)
        {
            if (alpha <= 0 || radius <= 0)
            {
                return;
            }

            var waist = radius * 0.22f;
            var points = new[]
            {
                new PointF(center.X, center.Y - radius),
                new PointF(center.X + waist, center.Y - waist),
                new PointF(center.X + radius, center.Y),
                new PointF(center.X + waist, center.Y + waist),
                new PointF(center.X, center.Y + radius),
                new PointF(center.X - waist, center.Y + waist),
                new PointF(center.X - radius, center.Y),
                new PointF(center.X - waist, center.Y - waist),
            };

            using (var brush = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), PaleGold)))
            {
                graphics.FillPolygon(brush, points);
            }
        }

        private sealed class Sparkle
        {
            public float X;
            public float Y;
            public float Size;
            public float Period = 1f;
            public float Phase;
            public float LastCycle;
        }
    }
}
