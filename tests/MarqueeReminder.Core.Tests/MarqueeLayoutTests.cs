using System.Drawing;
using MarqueeReminder.Core.Layout;
using MarqueeReminder.Core.Models;
using Xunit;

namespace MarqueeReminder.Core.Tests
{
    public class MarqueeLayoutTests
    {
        // A 1920x1080 screen with a 40 px taskbar at the bottom.
        private static readonly Rectangle WorkArea = new Rectangle(0, 0, 1920, 1040);

        [Theory]
        [InlineData(MarqueePosition.Top, 0, 0, 0, 1920, 50)]
        [InlineData(MarqueePosition.Bottom, 0, 0, 990, 1920, 50)]
        [InlineData(MarqueePosition.Left, 0, 0, 0, 50, 1040)]
        [InlineData(MarqueePosition.Right, 0, 1870, 0, 50, 1040)]
        [InlineData(MarqueePosition.Top, 100, 0, 100, 1920, 50)]
        [InlineData(MarqueePosition.Bottom, 100, 0, 890, 1920, 50)]
        [InlineData(MarqueePosition.Left, 100, 100, 0, 50, 1040)]
        [InlineData(MarqueePosition.Right, 100, 1770, 0, 50, 1040)]
        public void PositiveOffset_MovesInward(MarqueePosition position, int offset, int x, int y, int width, int height)
        {
            Assert.Equal(new Rectangle(x, y, width, height), MarqueeLayout.Compute(WorkArea, position, 50, offset, 0));
        }

        [Fact]
        public void NegativeOffset_MovesOutward()
        {
            Assert.Equal(-20, MarqueeLayout.Compute(WorkArea, MarqueePosition.Top, 50, -20, 0).Y);
            Assert.Equal(1010, MarqueeLayout.Compute(WorkArea, MarqueePosition.Bottom, 50, -20, 0).Y);
            Assert.Equal(-20, MarqueeLayout.Compute(WorkArea, MarqueePosition.Left, 50, -20, 0).X);
            Assert.Equal(1890, MarqueeLayout.Compute(WorkArea, MarqueePosition.Right, 50, -20, 0).X);
        }

        [Fact]
        public void ExtremeOffsets_KeepPartOfBarVisible()
        {
            var pushedOut = MarqueeLayout.Compute(WorkArea, MarqueePosition.Top, 50, -400, 0);
            var pushedIn = MarqueeLayout.Compute(WorkArea, MarqueePosition.Top, 50, 4000, 0);

            Assert.Equal(MarqueeLayout.MinVisiblePixels, pushedOut.Bottom - WorkArea.Top);
            Assert.Equal(MarqueeLayout.MinVisiblePixels, WorkArea.Bottom - pushedIn.Top);
        }

        [Fact]
        public void StackOffset_PlacesSecondBarBesideFirst()
        {
            var first = MarqueeLayout.Compute(WorkArea, MarqueePosition.Bottom, 50, 0, 0);
            var second = MarqueeLayout.Compute(WorkArea, MarqueePosition.Bottom, 50, 0, 50);

            Assert.Equal(first.Top, second.Bottom);
        }

        [Theory]
        [InlineData(48, 1.25f)]
        [InlineData(100, 1.33f)]
        [InlineData(300, 1.2f)]
        public void FitFontPixels_TextLineFillsSetShareOfBar(int thickness, float lineSpacingRatio)
        {
            var lineHeight = MarqueeLayout.FitFontPixels(thickness, lineSpacingRatio) * lineSpacingRatio;

            Assert.Equal(thickness * MarqueeLayout.TextFillRatio, lineHeight, 3);
        }

        [Fact]
        public void FitFontPixels_GrowsWithThickness_AndHasFloor()
        {
            Assert.True(MarqueeLayout.FitFontPixels(200, 1.3f) > MarqueeLayout.FitFontPixels(100, 1.3f));
            Assert.Equal(MarqueeLayout.MinFontPixels, MarqueeLayout.FitFontPixels(1, 1.3f));
        }

        [Fact]
        public void RespectsWorkAreaOrigin_OnSecondaryLayouts()
        {
            var area = new Rectangle(100, 40, 1820, 1040);

            Assert.Equal(new Rectangle(100, 40, 1820, 30), MarqueeLayout.Compute(area, MarqueePosition.Top, 30, 0, 0));
            Assert.Equal(new Rectangle(1890, 40, 30, 1040), MarqueeLayout.Compute(area, MarqueePosition.Right, 30, 0, 0));
        }
    }
}
