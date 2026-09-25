using System;
using System.Drawing;

namespace MarqueeReminder.Core.Models
{
    /// <summary>How a reminder's marquee bar looks and where it sits on screen.</summary>
    public sealed class MarqueeStyle
    {
        public const int MinOffset = -400;
        public const int MaxOffset = 4000;
        public const int MinThickness = 16;
        public const int MaxThickness = 400;
        public const int MinScrollSpeed = 10;
        public const int MaxScrollSpeed = 2000;
        public const int MaxDisplayMinutes = 24 * 60;
        public const float MinFontSize = 6f;
        public const float MaxFontSize = 200f;
        public const string DefaultFontFamily = "Segoe UI";

        private string _fontFamily = DefaultFontFamily;
        private float _fontSize = 20f;
        private int _offset;
        private int _thickness = 48;
        private int _scrollSpeed = 150;
        private int _displayMinutes = 1;

        public Color BackColor { get; set; } = Color.FromArgb(0x1F, 0x29, 0x37);

        public Color ForeColor { get; set; } = Color.FromArgb(0xFA, 0xCC, 0x15);

        public string FontFamily
        {
            get => _fontFamily;
            set => _fontFamily = string.IsNullOrWhiteSpace(value) ? DefaultFontFamily : value.Trim();
        }

        /// <summary>Font size in points.</summary>
        public float FontSize
        {
            get => _fontSize;
            set => _fontSize = Math.Max(MinFontSize, Math.Min(MaxFontSize, value));
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

        /// <summary>How long the marquee stays on screen, in minutes. Zero means until dismissed.</summary>
        public int DisplayMinutes
        {
            get => _displayMinutes;
            set => _displayMinutes = Clamp(value, 0, MaxDisplayMinutes);
        }

        public bool IsVertical => Position == MarqueePosition.Left || Position == MarqueePosition.Right;

        public MarqueeStyle Clone() => (MarqueeStyle)MemberwiseClone();

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
