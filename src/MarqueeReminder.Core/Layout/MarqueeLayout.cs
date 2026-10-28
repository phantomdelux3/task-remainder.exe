using System;
using System.Drawing;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Core.Layout
{
    /// <summary>Computes where a marquee bar goes on screen.</summary>
    public static class MarqueeLayout
    {
        /// <summary>However far a bar is pushed outward or inward, this many pixels stay on screen.</summary>
        public const int MinVisiblePixels = 8;

        /// <summary>Share of the bar's thickness taken up by one line of text.</summary>
        public const float TextFillRatio = 0.7f;

        /// <summary>Smallest font size used, in pixels, however thin the bar is.</summary>
        public const float MinFontPixels = 6f;

        /// <summary>
        /// The font size (in pixels) at which one line of text fills <see cref="TextFillRatio"/> of the bar,
        /// so the text grows and shrinks with the bar's thickness.
        /// </summary>
        /// <param name="thickness">Bar thickness in pixels.</param>
        /// <param name="lineSpacingRatio">The font's line spacing divided by its em height (about 1.2 to 1.4).</param>
        public static float FitFontPixels(int thickness, float lineSpacingRatio)
        {
            if (lineSpacingRatio <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lineSpacingRatio), lineSpacingRatio, "Line spacing ratio must be positive.");
            }

            return Math.Max(MinFontPixels, thickness * TextFillRatio / lineSpacingRatio);
        }

        /// <param name="workArea">Usable screen area (excludes the taskbar).</param>
        /// <param name="position">Edge the bar is attached to.</param>
        /// <param name="thickness">Bar thickness in pixels.</param>
        /// <param name="offset">Distance from the edge; positive is inward, negative is outward.</param>
        /// <param name="stackOffset">Extra inward distance so bars sharing an edge don't overlap.</param>
        public static Rectangle Compute(Rectangle workArea, MarqueePosition position, int thickness, int offset, int stackOffset)
        {
            var inset = offset + stackOffset;
            switch (position)
            {
                case MarqueePosition.Top:
                    return new Rectangle(workArea.Left, ClampY(workArea, workArea.Top + inset, thickness), workArea.Width, thickness);
                case MarqueePosition.Bottom:
                    return new Rectangle(workArea.Left, ClampY(workArea, workArea.Bottom - thickness - inset, thickness), workArea.Width, thickness);
                case MarqueePosition.Left:
                    return new Rectangle(ClampX(workArea, workArea.Left + inset, thickness), workArea.Top, thickness, workArea.Height);
                case MarqueePosition.Right:
                    return new Rectangle(ClampX(workArea, workArea.Right - thickness - inset, thickness), workArea.Top, thickness, workArea.Height);
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), position, "Unknown position.");
            }
        }

        /// <summary>
        /// Places a bar the user dragged: it still spans the whole work area lengthwise, and is
        /// kept entirely on screen across its thickness.
        /// </summary>
        /// <param name="workArea">Usable screen area (excludes the taskbar).</param>
        /// <param name="vertical">True for a left/right (vertical) bar.</param>
        /// <param name="thickness">Bar thickness in pixels.</param>
        /// <param name="start">Wanted top (horizontal bar) or left (vertical bar) edge in screen pixels.</param>
        public static Rectangle Place(Rectangle workArea, bool vertical, int thickness, int start)
        {
            if (vertical)
            {
                var width = Math.Min(thickness, workArea.Width);
                return new Rectangle(Clamp(start, workArea.Left, workArea.Right - width), workArea.Top, width, workArea.Height);
            }

            var height = Math.Min(thickness, workArea.Height);
            return new Rectangle(workArea.Left, Clamp(start, workArea.Top, workArea.Bottom - height), workArea.Width, height);
        }

        private static int ClampX(Rectangle area, int x, int thickness) =>
            Clamp(x, area.Left - thickness + VisiblePart(thickness), area.Right - VisiblePart(thickness));

        private static int ClampY(Rectangle area, int y, int thickness) =>
            Clamp(y, area.Top - thickness + VisiblePart(thickness), area.Bottom - VisiblePart(thickness));

        private static int VisiblePart(int thickness) => Math.Min(MinVisiblePixels, thickness);

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
