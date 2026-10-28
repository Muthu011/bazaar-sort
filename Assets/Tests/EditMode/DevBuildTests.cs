using Game.UI;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>Dev-only tools: the build guard, the level select layout, and the allocation-free fps readout.</summary>
    public class DevBuildTests
    {
        // ---- Guard ----

        [Test]
        public void LevelSelect_IsDisabledInReleaseBuilds()
        {
            Assert.IsFalse(DevBuild.IsLevelSelectEnabled(false));
            Assert.IsFalse(DevBuild.IsFpsReadoutEnabled(false));
        }

        [Test]
        public void LevelSelect_IsEnabledInDevelopmentBuilds()
        {
            Assert.IsTrue(DevBuild.IsLevelSelectEnabled(true));
            Assert.IsTrue(DevBuild.IsFpsReadoutEnabled(true));
        }

        [Test]
        public void DevTools_AreOnInTheEditor()
        {
            // The editor counts as a debug build, so the overlay shows in play mode; release players report false.
            Assert.IsTrue(Debug.isDebugBuild);
            Assert.IsTrue(DevBuild.IsLevelSelectEnabled(Debug.isDebugBuild));
        }

        // ---- Layout ----

        private static DevLevelSelectMetrics For(float widthDp, float heightDp, int count = 5)
        {
            var settings = BoardLayoutSettings.Default;
            return DevLevelSelectLayout.Compute(new Vector2(widthDp, heightDp), settings.TopReserveFraction,
                settings.BottomReserveFraction, count);
        }

        [TestCase(360f, 640f)]  // 16:9, smallest common phone
        [TestCase(360f, 743f)]  // 1080x2400 at 3x with a 110 px cutout and 60 px gesture bar
        [TestCase(412f, 915f)]  // 20:9, 412 dp wide
        [TestCase(320f, 568f)]  // very small screen
        public void Buttons_AreAtLeastTouchFloor_AndInsideTheTopReserve(float w, float h)
        {
            var m = For(w, h);
            Assert.GreaterOrEqual(m.Button.x, 48f);
            Assert.GreaterOrEqual(m.Button.y, 48f);
            Assert.IsTrue(m.FitsInReserve, $"Row bottom {m.RowBottomFromTop} dp is inside the {m.TopReserve} dp reserve.");
            Assert.GreaterOrEqual(m.RowCentreFromTop - m.Button.y * 0.5f, 0f, "Row starts inside the safe area.");
        }

        [TestCase(360f, 640f)]
        [TestCase(320f, 568f)]
        public void Row_FitsTheSafeWidth_WithoutOverlap(float w, float h)
        {
            var m = For(w, h);
            Assert.LessOrEqual(m.RowWidth, w - 2f * DevLevelSelectLayout.SideMarginDp + 0.01f);
            for (int i = 1; i < m.Count; i++)
            {
                float gap = m.ButtonCentre(i).x - m.ButtonCentre(i - 1).x - m.Button.x;
                Assert.AreEqual(DevLevelSelectLayout.GapDp, gap, 0.01f);
            }
            Assert.AreEqual(0f, m.ButtonCentre(0).x + m.ButtonCentre(m.Count - 1).x, 0.01f, "Row is centred.");
        }

        [Test]
        public void ShortReserve_KeepsTouchFloor_AndReportsNoFit()
        {
            var m = For(360f, 300f); // landscape-like: 30 dp reserve
            Assert.AreEqual(48f, m.Button.y);
            Assert.IsFalse(m.FitsInReserve);
        }

        // ---- Fps readout ----

        [Test]
        public void FpsSampler_ReportsAverageAndWorstFrame_PerWindow()
        {
            var sampler = new FpsSampler(0.5f);
            bool done = false;
            for (int i = 0; i < 29; i++) done |= sampler.AddFrame(1f / 60f);
            Assert.IsFalse(done, "Window not finished yet.");
            Assert.AreEqual(0, sampler.Fps);
            Assert.IsTrue(sampler.AddFrame(0.025f)); // one slow frame closes the window
            Assert.AreEqual(59, sampler.Fps);         // 30 frames / 0.5083 s
            Assert.AreEqual(25, sampler.WorstFrameMs);
        }

        [Test]
        public void FpsSampler_ResetsWorstFrame_EachWindow()
        {
            var sampler = new FpsSampler(0.5f);
            sampler.AddFrame(0.1f);
            while (!sampler.AddFrame(1f / 60f)) { }
            Assert.AreEqual(100, sampler.WorstFrameMs);
            while (!sampler.AddFrame(1f / 60f)) { }
            Assert.AreEqual(17, sampler.WorstFrameMs);
            Assert.AreEqual(60, sampler.Fps);
        }

        [Test]
        public void FpsSampler_IgnoresBadDeltas()
        {
            var sampler = new FpsSampler(0.5f);
            Assert.IsFalse(sampler.AddFrame(0f));
            Assert.IsFalse(sampler.AddFrame(-1f));
            Assert.IsFalse(sampler.AddFrame(float.NaN));
            Assert.IsFalse(sampler.AddFrame(float.PositiveInfinity));
        }

        [Test]
        public void FpsLabels_AreCached_AndClamp()
        {
            Assert.AreEqual("60 fps", DevFpsLabels.Fps(60));
            Assert.AreSame(DevFpsLabels.Fps(60), DevFpsLabels.Fps(60), "Same string instance: no allocation per update.");
            Assert.AreEqual("0 fps", DevFpsLabels.Fps(-5));
            Assert.AreEqual("240+ fps", DevFpsLabels.Fps(1000));
            Assert.AreEqual("max 17 ms", DevFpsLabels.WorstMs(17));
            Assert.AreSame(DevFpsLabels.WorstMs(17), DevFpsLabels.WorstMs(17));
            Assert.AreEqual("max 250+ ms", DevFpsLabels.WorstMs(9999));
        }

        [Test]
        public void FrameRate_TargetsSixty()
        {
            Assert.AreEqual(60, FrameRate.Target);
        }
    }
}
