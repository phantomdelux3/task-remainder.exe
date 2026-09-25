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

        private static int ClampX(Rectangle area, int x, int thickness) =>
            Clamp(x, area.Left - thickness + VisiblePart(thickness), area.Right - VisiblePart(thickness));

        private static int ClampY(Rectangle area, int y, int thickness) =>
            Clamp(y, area.Top - thickness + VisiblePart(thickness), area.Bottom - VisiblePart(thickness));

        private static int VisiblePart(int thickness) => Math.Min(MinVisiblePixels, thickness);

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
