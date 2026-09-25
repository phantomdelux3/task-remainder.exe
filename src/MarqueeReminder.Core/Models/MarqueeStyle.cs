using System;
using System.Drawing;

namespace MarqueeReminder.Core.Models
{
    /// <summary>How a reminder's marquee bar looks, where it sits on screen and how long it stays.</summary>
    public sealed class MarqueeStyle
    {
        public const int MinOffset = -400;
        public const int MaxOffset = 4000;
        public const int MinThickness = 16;
        public const int MaxThickness = 400;
        public const int MinScrollSpeed = 10;
        public const int MaxScrollSpeed = 2000;
        public const int MinDisplayMinutes = 1;
        public const int MaxDisplayMinutes = 24 * 60;
        public const string DefaultFontFamily = "Segoe UI";

        private string _fontFamily = DefaultFontFamily;
        private int _offset;
        private int _thickness = 48;
        private int _scrollSpeed = 150;
        private int _displayMinutes = 5;

        public Color BackColor { get; set; } = Color.FromArgb(0x1F, 0x29, 0x37);

        public Color ForeColor { get; set; } = Color.FromArgb(0xFA, 0xCC, 0x15);

        /// <summary>Font family of the text. Its size is not stored: text always fits the bar's thickness.</summary>
        public string FontFamily
        {
            get => _fontFamily;
            set => _fontFamily = string.IsNullOrWhiteSpace(value) ? DefaultFontFamily : value.Trim();
        }

        public bool Bold { get; set; } = true;

        public bool Italic { get; set; }

        public MarqueePosition Position { get; set; } = MarqueePosition.Top;

        /// <summary>
        /// Distance in pixels from the chosen screen edge. Positive values move the bar inward
        /// (towards the centre of the screen); negative values push it outward past the edge.
        /// </summary>
        public int Offset
        {
            get => _offset;
            set => _offset = Clamp(value, MinOffset, MaxOffset);
        }

        /// <summary>Height of a top/bottom bar, or width of a left/right bar, in pixels.</summary>
        public int Thickness
        {
            get => _thickness;
            set => _thickness = Clamp(value, MinThickness, MaxThickness);
        }

        /// <summary>Scroll speed in pixels per second.</summary>
        public int ScrollSpeed
        {
            get => _scrollSpeed;
            set => _scrollSpeed = Clamp(value, MinScrollSpeed, MaxScrollSpeed);
        }

        /// <summary>
        /// When true the bar has no time limit: it stays until closed with the reminder's password.
        /// </summary>
        public bool ShowUntilDismissed { get; set; } = true;

        /// <summary>How long the bar stays on screen when <see cref="ShowUntilDismissed"/> is false.</summary>
        public int DisplayMinutes
        {
            get => _displayMinutes;
            set => _displayMinutes = Clamp(value, MinDisplayMinutes, MaxDisplayMinutes);
        }

        /// <summary>How long the bar stays on screen, or null if it stays until dismissed.</summary>
        public TimeSpan? DisplayDuration =>
            ShowUntilDismissed ? (TimeSpan?)null : TimeSpan.FromMinutes(DisplayMinutes);

        public bool IsVertical => Position == MarqueePosition.Left || Position == MarqueePosition.Right;

        public MarqueeStyle Clone() => (MarqueeStyle)MemberwiseClone();

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
