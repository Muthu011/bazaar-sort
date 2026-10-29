using Game.UI;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="HudLayout"/> (issues #21, #40): the level number in the top bar; booster slots, coin counter and the
    /// settings button in the bottom bar; all inside the safe area's reserves and at least the 48 dp touch floor.
    /// </summary>
    public class HudLayoutTests
    {
        private static readonly float Top = BoardLayoutSettings.Default.TopReserveFraction;
        private static readonly float Bottom = BoardLayoutSettings.Default.BottomReserveFraction;
        private const float E = 0.001f;

        private static HudMetrics For(float w, float h) => HudLayout.Compute(new Vector2(w, h), Top, Bottom);

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax - E && b.xMin < a.xMax - E && a.yMin < b.yMax - E && b.yMin < a.yMax - E;
        }

        // Safe areas in dp of the three phone sizes (issue #40) with simulated notch / status bar and gesture bar:
        // 1080x2400 @3 minus 110 px top and 63 px bottom; 720x1600 @2 minus 74 px top and 42 px bottom; 1080x1920 @3 is
        // full screen (640 dp), and 616 dp (minus a 24 dp bar) is kept as a stricter, shorter case. Plus the #21 sizes.
        [TestCase(360f, 742.33f)]
        [TestCase(360f, 616f)]
        [TestCase(360f, 742f)]
        [TestCase(360f, 640f)]
        [TestCase(360f, 800f)]
        [TestCase(412f, 915f)]
        public void SettingsButton_IsAtLeast48dp_AndInsideTheBottomReserve(float w, float h)
        {
            var m = For(w, h);
            Assert.GreaterOrEqual(m.Settings.width, 48f);
            Assert.GreaterOrEqual(m.Settings.height, 48f);
            Assert.IsTrue(m.BottomBarFits, "Clear of the shelves.");
            Assert.GreaterOrEqual(m.Settings.yMin, 0f, "Above the safe-area bottom (gesture bar).");
            Assert.LessOrEqual(m.Settings.xMax, w - HudLayout.SideMarginDp + E, "Margin from the right edge.");
        }

        [TestCase(360f, 742.33f)]
        [TestCase(360f, 616f)]
        [TestCase(412f, 915f)]
        public void BottomBar_BoostersThenCounterThenSettings_InOneRow_WithoutOverlap(float w, float h)
        {
            var m = For(w, h);
            Assert.LessOrEqual(m.Counter.xMax, m.Settings.xMin - HudLayout.GapDp + E, "Counter left of settings.");
            Assert.AreEqual(m.Settings.center.y, m.Counter.center.y, E, "One row.");
            Assert.AreEqual(HudLayout.BoosterSlots, m.Boosters.Length);
            Assert.AreEqual(HudLayout.SideMarginDp, m.Boosters[0].xMin, E, "Boosters start at the left margin.");
            for (int i = 0; i < m.Boosters.Length; i++)
            {
                var b = m.Boosters[i];
                Assert.GreaterOrEqual(b.width, 48f, $"Booster slot {i} width");
                Assert.GreaterOrEqual(b.height, 48f, $"Booster slot {i} height");
                Assert.LessOrEqual(b.width, HudLayout.BoosterMaxDp + E);
                Assert.AreEqual(m.Settings.center.y, b.center.y, E, "One row.");
                Assert.IsFalse(Overlaps(b, m.Counter), $"Booster slot {i} and counter overlap.");
                if (i > 0) Assert.LessOrEqual(m.Boosters[i - 1].xMax, b.xMin - HudLayout.GapDp + E, "Slots in order with a gap.");
            }
            Assert.LessOrEqual(m.Boosters[m.Boosters.Length - 1].xMax, m.Counter.xMin - HudLayout.GapDp + E);
        }

        // ---- Coin pill width (issue #44, PR #63) ----

        [TestCase(360f, 742.33f)]
        [TestCase(360f, 616f)]
        [TestCase(412f, 915f)]
        public void CoinPill_AtEveryWidth_StaysInTheRow_WithoutTouchingBoostersOrSettings(float w, float h)
        {
            foreach (var text in new[] { "0", "20", "999", "1250", "99999", Game.UI.CoinBalanceText.OverflowText })
            {
                float width = HudLayout.CounterWidthFor(text);
                var m = HudLayout.Compute(new Vector2(w, h), Top, Bottom, width);
                Assert.AreEqual(width, m.Counter.width, E, text);
                Assert.IsTrue(m.BottomBarFits, text);
                Assert.LessOrEqual(m.Counter.xMax, m.Settings.xMin - HudLayout.GapDp + E, $"{text}: left of settings.");
                var lastBooster = m.Boosters[m.Boosters.Length - 1];
                Assert.LessOrEqual(lastBooster.xMax, m.Counter.xMin - HudLayout.GapDp + E, $"{text}: clear of the booster slots.");
                foreach (var b in m.Boosters) Assert.GreaterOrEqual(b.width, 48f, $"{text}: booster touch floor.");
            }
        }

        [TestCase(360f, 742.33f)]
        [TestCase(412f, 915f)]
        public void CoinPill_GrowsLeftwards_AndTheBoostersNeverMove(float w, float h)
        {
            var narrow = HudLayout.Compute(new Vector2(w, h), Top, Bottom, HudLayout.CounterMinWidthDp);
            var wide = HudLayout.Compute(new Vector2(w, h), Top, Bottom, HudLayout.CounterMaxWidthDp);
            Assert.AreEqual(narrow.Counter.xMax, wide.Counter.xMax, E, "Right edge fixed beside settings.");
            Assert.Less(wide.Counter.xMin, narrow.Counter.xMin);
            for (int i = 0; i < narrow.Boosters.Length; i++) Assert.AreEqual(narrow.Boosters[i], wide.Boosters[i], $"Booster {i}");
        }

        [Test]
        public void CoinPill_WidthIsClamped()
        {
            var m = For(360f, 742f);
            Assert.AreEqual(HudLayout.CounterMinWidthDp, m.Counter.width, E, "Default is the minimum.");
            Assert.AreEqual(HudLayout.CounterMinWidthDp, HudLayout.Compute(new Vector2(360f, 742f), Top, Bottom, 10f).Counter.width, E);
            Assert.AreEqual(HudLayout.CounterMaxWidthDp, HudLayout.Compute(new Vector2(360f, 742f), Top, Bottom, 500f).Counter.width, E);
        }

        [Test]
        public void TinyReserve_KeepsTheTouchFloor_AndSitsOnTheBottom()
        {
            var m = For(360f, 200f);
            Assert.AreEqual(48f, m.Settings.height);
            float lowest = m.Settings.yMin;
            foreach (var b in m.Boosters) lowest = Mathf.Min(lowest, b.yMin);
            Assert.AreEqual(0f, lowest, E, "The tallest piece sits on the safe-area bottom.");
            Assert.IsFalse(m.BottomBarFits, "Reported as not fitting the 20 dp reserve.");
        }

        [TestCase(360f, 742.33f)]
        [TestCase(360f, 616f)]
        [TestCase(360f, 742f)]
        [TestCase(412f, 915f)]
        public void LevelLabel_IsCentredInTheTopBar_AboveTheStatusLine(float w, float h)
        {
            var m = For(w, h);
            Assert.IsTrue(m.TopBarFits, "Inside the top reserve: below the notch, above the shelves.");
            Assert.AreEqual(w * 0.5f, m.LevelLabel.center.x, E, "Centred.");
            float reserveBottom = h - m.TopReserve;
            float statusTop = reserveBottom + m.TopReserve * (BoardView.StatusCentreInReserve + BoardView.StatusMaxHeightInReserve * 0.5f);
            Assert.GreaterOrEqual(m.LevelLabel.yMin, statusTop - E, "The status line (\"Cleared!\") fits under the level number.");
        }

        [TestCase(360f, 742.33f)]
        [TestCase(360f, 616f)]
        [TestCase(360f, 742f)]
        public void TopBar_DevLevelsButtonAndFpsReadout_DoNotOverlapTheLevelLabel(float w, float h)
        {
            var m = For(w, h);
            var dev = DevLevelSelectLayout.Compute(new Vector2(w, h), Top, Bottom, 15);
            // DevLevelSelectLayout measures from the top-left (toggle) and top-right (fps); convert to bottom-left.
            var toggle = new Rect(dev.ToggleCentre.x - dev.Toggle.x * 0.5f, h + dev.ToggleCentre.y - dev.Toggle.y * 0.5f, dev.Toggle.x, dev.Toggle.y);
            var fps = new Rect(w + dev.FpsCentre.x - dev.FpsLine.x * 0.5f, h + dev.FpsCentre.y - dev.FpsLine.y * 0.5f, dev.FpsLine.x, dev.FpsLine.y);
            var worst = new Rect(w + dev.WorstCentre.x - dev.FpsLine.x * 0.5f, h + dev.WorstCentre.y - dev.FpsLine.y * 0.5f, dev.FpsLine.x, dev.FpsLine.y);
            Assert.IsFalse(Overlaps(toggle, m.LevelLabel), "Levels button and level label.");
            Assert.IsFalse(Overlaps(fps, m.LevelLabel), "Fps line and level label.");
            Assert.IsFalse(Overlaps(worst, m.LevelLabel), "Frame-time line and level label.");
            Assert.IsFalse(Overlaps(fps, worst), "Fps lines.");
            Assert.IsTrue(dev.FpsFitsInReserve, "Fps readout inside the top reserve.");
            Assert.LessOrEqual(fps.xMax, w - DevLevelSelectLayout.SideMarginDp + E, "Right margin.");
        }
    }
}
