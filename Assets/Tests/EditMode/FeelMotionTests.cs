using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="FeelMotion"/> (issue #21): pick-up squash and the move arc.</summary>
    public class FeelMotionTests
    {
        private static readonly Vector2 From = new Vector2(200f, 500f);
        private static readonly Vector2 To = new Vector2(800f, 900f);

        [Test]
        public void Arc_LastsAboutAQuarterSecond()
        {
            Assert.AreEqual(0.25f, FeelMotion.ArcSeconds);
            Assert.AreEqual(0f, FeelMotion.ArcProgress(0f));
            Assert.AreEqual(1f, FeelMotion.ArcProgress(FeelMotion.ArcSeconds));
            Assert.AreEqual(1f, FeelMotion.ArcProgress(3f));
        }

        [Test]
        public void Arc_EasesOut_FastStartSoftLanding()
        {
            Assert.AreEqual(0.75f, FeelMotion.ArcProgress(FeelMotion.ArcSeconds * 0.5f), 0.0001f, "Quadratic ease-out: 3/4 of the way at half time.");
            float previousStep = float.MaxValue;
            float previous = 0f;
            for (int i = 1; i <= 50; i++)
            {
                float p = FeelMotion.ArcProgress(FeelMotion.ArcSeconds * i / 50f);
                float step = p - previous;
                Assert.Greater(step, 0f, "Always moving forward.");
                Assert.LessOrEqual(step, previousStep + 0.0001f, "Slowing down (ease-out).");
                previousStep = step;
                previous = p;
            }
        }

        [Test]
        public void Arc_StartsAtTheItem_LandsExactlyInTheSlot()
        {
            float h = FeelMotion.ArcHeight(Vector2.Distance(From, To), 250f);
            Assert.AreEqual(From, FeelMotion.ArcPosition(From, To, h, 0f));
            Assert.AreEqual(To, FeelMotion.ArcPosition(From, To, h, FeelMotion.ArcSeconds));
            Assert.AreEqual(To, FeelMotion.ArcPosition(From, To, h, 1f));
            Assert.AreEqual(To, FeelMotion.ArcGround(From, To, FeelMotion.ArcSeconds));
        }

        [Test]
        public void Arc_BowsUpAboveTheStraightLine_ByItsHeight()
        {
            float h = 120f;
            float half = 0f;
            // Find the time where progress is 1/2: the top of the arc.
            for (int i = 0; i <= 1000; i++)
            {
                float t = FeelMotion.ArcSeconds * i / 1000f;
                if (FeelMotion.ArcProgress(t) >= 0.5f)
                {
                    half = t;
                    break;
                }
            }
            var top = FeelMotion.ArcPosition(From, To, h, half);
            var ground = FeelMotion.ArcGround(From, To, half);
            Assert.AreEqual(h, top.y - ground.y, 1.5f, "Peak lift equals the arc height.");
            Assert.AreEqual(ground.x, top.x, 0.001f, "Lift is straight up.");
            for (int i = 1; i < 50; i++)
            {
                float t = FeelMotion.ArcSeconds * i / 50f;
                Assert.Greater(FeelMotion.ArcPosition(From, To, h, t).y, FeelMotion.ArcGround(From, To, t).y, "Above the line in flight.");
            }
        }

        [Test]
        public void ArcHeight_GrowsWithDistance_AndIsCapped()
        {
            const float tile = 200f;
            Assert.AreEqual(FeelMotion.ArcHeightTiles * tile, FeelMotion.ArcHeight(0f, tile), 0.001f);
            Assert.Greater(FeelMotion.ArcHeight(600f, tile), FeelMotion.ArcHeight(200f, tile));
            Assert.AreEqual(FeelMotion.MaxArcHeightTiles * tile, FeelMotion.ArcHeight(100000f, tile), 0.001f);
        }

        [Test]
        public void ArcScale_GrowsMidFlight_AndLandsAtOne()
        {
            Assert.AreEqual(1f, FeelMotion.ArcScale(0f));
            Assert.AreEqual(1f, FeelMotion.ArcScale(FeelMotion.ArcSeconds));
            float peak = 0f;
            for (int i = 0; i <= 100; i++) peak = Mathf.Max(peak, FeelMotion.ArcScale(FeelMotion.ArcSeconds * i / 100f));
            Assert.AreEqual(FeelMotion.ArcPeakScale, peak, 0.001f);
        }

        [Test]
        public void PickUp_StartsSquashed_1_1_By_0_9()
        {
            var s = FeelMotion.PickUpScale(0f);
            Assert.AreEqual(1.1f, s.x, 0.0001f);
            Assert.AreEqual(0.9f, s.y, 0.0001f);
        }

        [Test]
        public void PickUp_SpringsToAStretch_ThenSettlesAtRest()
        {
            var mid = FeelMotion.PickUpScale(FeelMotion.PickUpSeconds * 0.5f);
            Assert.Less(mid.x, 1f, "Springs the other way: narrower...");
            Assert.Greater(mid.y, 1f, "...and taller.");
            Assert.AreEqual(Vector2.one, FeelMotion.PickUpScale(FeelMotion.PickUpSeconds));
            Assert.AreEqual(Vector2.one, FeelMotion.PickUpScale(2f));
            for (int i = 0; i <= 100; i++)
            {
                var s = FeelMotion.PickUpScale(FeelMotion.PickUpSeconds * i / 100f);
                Assert.AreEqual(2f, s.x + s.y, 0.0001f, "Keeps its area roughly: what grows one way shrinks the other.");
                Assert.LessOrEqual(Mathf.Abs(s.x - 1f), FeelMotion.PickUpSquash + 0.0001f);
            }
        }
    }
}
