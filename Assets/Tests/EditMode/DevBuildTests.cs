using Game.UI;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>Dev-only tools: the build guard, the level select layout and cell states, and the allocation-free fps readout.</summary>
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

        // ---- Layout (issue #22: a "Levels" button that opens a grid, so every level fits on a phone) ----

        private static DevLevelSelectMetrics For(float widthDp, float heightDp, int count = 15)
        {
            var settings = BoardLayoutSettings.Default;
            return DevLevelSelectLayout.Compute(new Vector2(widthDp, heightDp), settings.TopReserveFraction,
                settings.BottomReserveFraction, count);
        }

        [TestCase(360f, 640f)]  // 16:9, smallest common phone
        [TestCase(360f, 743f)]  // 1080x2400 at 3x with a 110 px cutout and 60 px gesture bar
        [TestCase(412f, 915f)]  // 20:9, 412 dp wide
        [TestCase(320f, 568f)]  // very small screen
        public void Toggle_IsAtLeastTouchFloor_AndInsideTheTopReserve(float w, float h)
        {
            var m = For(w, h);
            Assert.GreaterOrEqual(m.Toggle.x, 48f);
            Assert.GreaterOrEqual(m.Toggle.y, 48f);
            Assert.IsTrue(m.ToggleFitsInReserve, $"Toggle bottom is inside the {m.TopReserve} dp reserve.");
            Assert.LessOrEqual(-m.ToggleCentre.y - m.Toggle.y * 0.5f + 0.001f, m.TopReserve);
            Assert.GreaterOrEqual(-m.ToggleCentre.y - m.Toggle.y * 0.5f, 0f, "Toggle starts inside the safe area.");
            Assert.GreaterOrEqual(m.ToggleCentre.x - m.Toggle.x * 0.5f, 0f, "Toggle starts inside the safe area.");
        }

        [TestCase(360f, 640f, 15)]
        [TestCase(360f, 743f, 15)]
        [TestCase(412f, 915f, 15)]
        [TestCase(320f, 568f, 15)]
        [TestCase(360f, 640f, 20)] // all of World 1
        [TestCase(360f, 640f, 1)]
        public void Grid_CellsAreAtLeastTouchFloor_AndTheCardFitsTheSafeArea(float w, float h, int count)
        {
            var m = For(w, h, count);
            Assert.GreaterOrEqual(m.Cell, 48f);
            Assert.GreaterOrEqual(m.Action.y, 48f);
            Assert.GreaterOrEqual(m.Action.x, 48f);
            Assert.GreaterOrEqual(m.Columns * m.Rows, count, "A cell for every level.");
            Assert.IsTrue(m.CardFitsSafeArea, $"Card {m.Card} fits {w}x{h} dp.");
            Assert.LessOrEqual(m.Card.x, w - 2f * DevLevelSelectLayout.SideMarginDp + 0.01f, "Side margins kept.");
        }

        [Test]
        public void Grid_HasFiveColumnsOnA360DpPhone_SoFifteenLevelsAreThreeRows()
        {
            var m = For(360f, 640f, 15);
            Assert.AreEqual(5, m.Columns);
            Assert.AreEqual(3, m.Rows);
        }

        [TestCase(360f, 640f, 15)]
        [TestCase(320f, 568f, 15)]
        public void Grid_CellsAndButtonsDoNotOverlap_AndStayInsideTheCard(float w, float h, int count)
        {
            var m = For(w, h, count);
            var rects = new System.Collections.Generic.List<Rect>();
            for (int i = 0; i < count; i++)
            {
                var c = m.CellCentre(i);
                rects.Add(new Rect(c.x - m.Cell * 0.5f, c.y - m.Cell * 0.5f, m.Cell, m.Cell));
            }
            for (int i = 0; i < 2; i++)
            {
                var c = m.ActionCentre(i);
                rects.Add(new Rect(c.x - m.Action.x * 0.5f, c.y - m.Action.y * 0.5f, m.Action.x, m.Action.y));
            }
            var title = m.TitleCentre;
            rects.Add(new Rect(-m.Card.x * 0.5f + m.Padding, title.y - DevLevelSelectLayout.TitleHeightDp * 0.5f,
                m.Card.x - 2f * m.Padding, DevLevelSelectLayout.TitleHeightDp));

            var card = new Rect(-m.Card.x * 0.5f, -m.Card.y * 0.5f, m.Card.x, m.Card.y);
            for (int i = 0; i < rects.Count; i++)
            {
                Assert.IsTrue(card.xMin <= rects[i].xMin + 0.01f && rects[i].xMax <= card.xMax + 0.01f
                    && card.yMin <= rects[i].yMin + 0.01f && rects[i].yMax <= card.yMax + 0.01f, $"Piece {i} inside the card.");
                for (int j = i + 1; j < rects.Count; j++)
                {
                    var a = rects[i];
                    var b = rects[j];
                    bool overlap = a.xMin < b.xMax - 0.01f && b.xMin < a.xMax - 0.01f && a.yMin < b.yMax - 0.01f && b.yMin < a.yMax - 0.01f;
                    Assert.IsFalse(overlap, $"Pieces {i} and {j} overlap.");
                }
            }
        }

        [Test]
        public void ShortReserve_KeepsTouchFloor_AndReportsNoFit()
        {
            var m = For(360f, 300f); // landscape-like: 30 dp reserve
            Assert.AreEqual(48f, m.Toggle.y);
            Assert.IsFalse(m.ToggleFitsInReserve);
        }

        // ---- Cell state ----

        [Test]
        public void CellState_CurrentWins_ThenClearedThenOpenThenLocked()
        {
            Assert.AreEqual(DevLevelCellState.Current, DevLevelCell.StateFor(true, 3, true));
            Assert.AreEqual(DevLevelCellState.Current, DevLevelCell.StateFor(true, 0, false));
            Assert.AreEqual(DevLevelCellState.Cleared, DevLevelCell.StateFor(false, 3, true));
            Assert.AreEqual(DevLevelCellState.Cleared, DevLevelCell.StateFor(false, 1, false));
            Assert.AreEqual(DevLevelCellState.Open, DevLevelCell.StateFor(false, 0, true));
            Assert.AreEqual(DevLevelCellState.Locked, DevLevelCell.StateFor(false, 0, false));
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
