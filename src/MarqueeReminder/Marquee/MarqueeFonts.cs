using System;
using System.Drawing;
using System.Linq;
using MarqueeReminder.Core.Layout;
using MarqueeReminder.Core.Models;

namespace MarqueeReminder.Marquee
{
    /// <summary>Creates fonts for marquee text, sized to fit a given bar thickness.</summary>
    internal static class MarqueeFonts
    {
        // Scanning every installed font is slow, so it is done once per run.
        private static readonly Lazy<string[]> Installed = new Lazy<string[]>(() =>
            FontFamily.Families
                .Where(family => family.IsStyleAvailable(FontStyle.Regular))
                .Select(family => family.Name)
                .ToArray());

        /// <summary>Installed font families that can draw regular text, for the font picker.</summary>
        public static string[] InstalledFamilies() => (string[])Installed.Value.Clone();

        /// <summary>
        /// A font whose line of text fills most of a bar of the given thickness. Falls back to a
        /// supported style, then to the default family, because GDI+ throws when asked for a style
        /// a font does not have.
        /// </summary>
        public static Font CreateFitted(MarqueeStyle style, int thickness)
        {
            var requested = (style.Bold ? FontStyle.Bold : FontStyle.Regular) | (style.Italic ? FontStyle.Italic : FontStyle.Regular);
            var candidates = new[] { requested, requested & ~FontStyle.Italic, FontStyle.Regular, FontStyle.Bold };

            foreach (var family in new[] { TryGetFamily(style.FontFamily), TryGetFamily(MarqueeStyle.DefaultFontFamily), FontFamily.GenericSansSerif })
            {
                if (family == null)
                {
                    continue;
                }

                foreach (var fontStyle in candidates.Where(family.IsStyleAvailable))
                {
                    var lineSpacingRatio = family.GetLineSpacing(fontStyle) / (float)family.GetEmHeight(fontStyle);
                    return new Font(family, MarqueeLayout.FitFontPixels(thickness, lineSpacingRatio), fontStyle, GraphicsUnit.Pixel);
                }
            }

            return new Font(FontFamily.GenericSansSerif, MarqueeLayout.FitFontPixels(thickness, 1.2f), FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private static FontFamily? TryGetFamily(string name)
        {
            try
            {
                return new FontFamily(name);
            }
            catch (ArgumentException)
            {
                return null; // Not installed.
            }
        }
    }
}
