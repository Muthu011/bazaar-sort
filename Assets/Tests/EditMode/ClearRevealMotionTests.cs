using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="ClearRevealMotion"/>: triple-clear pop and fade, layer-reveal slide.</summary>
    public class ClearRevealMotionTests
    {
        private static readonly Vector2 Hidden = new Vector2(300f, 820f);
        private static readonly Vector2 Front = new Vector2(300f, 700f);

        [Test]
        public void Clear_FinishesUnderHalfASecond()
        {
            Assert.Less(ClearRevealMotion.ClearSeconds, 0.5f);
            Assert.AreEqual(0f, ClearRevealMotion.ClearScale(ClearRevealMotion.ClearSeconds));
            Assert.AreEqual(0f, ClearRevealMotion.ClearAlpha(ClearRevealMotion.ClearSeconds));
        }

        [Test]
        public void Clear_StartsAtRest()
        {
            Assert.AreEqual(1f, ClearRevealMotion.ClearScale(0f));
            Assert.AreEqual(1f, ClearRevealMotion.ClearAlpha(0f));
        }

        [Test]
        public void Clear_ScalePeaksAboveOne_ThenFallsToZero()
        {
            const int steps = 200;
            float peak = 0f;
            float peakTime = -1f;
            for (int i = 0; i <= steps; i++)
            {
                float t = ClearRevealMotion.ClearSeconds * i / steps;
                float s = ClearRevealMotion.ClearScale(t);
                if (s > peak)
                {
                    peak = s;
                    peakTime = t;
                }
            }

            Assert.Greater(peak, 1.1f, "Visible pop.");
            Assert.LessOrEqual(peak, ClearRevealMotion.ClearPeakScale + 0.0001f);
            Assert.Greater(peakTime, 0f);
            Assert.Less(peakTime, ClearRevealMotion.ClearSeconds * 0.5f, "Peak comes early, then the fade.");

            // Rises up to the peak, falls after it.
            float previous = ClearRevealMotion.ClearScale(0f);
            for (int i = 1; i <= steps; i++)
            {
                float t = ClearRevealMotion.ClearSeconds * i / steps;
                float s = ClearRevealMotion.ClearScale(t);
                if (t <= peakTime) Assert.GreaterOrEqual(s, previous - 0.0001f, $"Rising at {t}");
                else Assert.LessOrEqual(s, previous + 0.0001f, $"Falling at {t}");
                previous = s;
            }
        }

        [Test]
        public void Clear_FadesMonotonicallyToZero()
        {
            float previous = 1f;
            for (int i = 0; i <= 100; i++)
            {
                float a = ClearRevealMotion.ClearAlpha(ClearRevealMotion.ClearSeconds * i / 100f);
                Assert.LessOrEqual(a, previous + 0.0001f);
                Assert.GreaterOrEqual(a, 0f);
                previous = a;
            }
            Assert.AreEqual(0f, previous);
        }

        [Test]
        public void Clear_StaysGoneAfterTheEnd()
        {
            Assert.AreEqual(0f, ClearRevealMotion.ClearScale(5f));
            Assert.AreEqual(0f, ClearRevealMotion.ClearAlpha(5f));
        }

        [Test]
        public void Reveal_GoesFromHiddenSlotToFrontSlot()
        {
            Assert.AreEqual(Hidden, ClearRevealMotion.RevealPosition(Hidden, Front, 0f));
            Assert.AreEqual(Front, ClearRevealMotion.RevealPosition(Hidden, Front, ClearRevealMotion.RevealSeconds));
            Assert.AreEqual(Front, ClearRevealMotion.RevealPosition(Hidden, Front, 3f));
        }

        [Test]
        public void Reveal_MovesTowardsFront_Monotonically_OnTheLine()
        {
            float previous = float.MaxValue;
            for (int i = 0; i <= 50; i++)
            {
                var p = ClearRevealMotion.RevealPosition(Hidden, Front, ClearRevealMotion.RevealSeconds * i / 50f);
                float d = Vector2.Distance(Front, p);
                Assert.LessOrEqual(d, previous + 0.001f);
                Assert.AreEqual(Hidden.x, p.x, 0.001f, "Straight slide forward.");
                previous = d;
            }
        }

        [Test]
        public void Reveal_GrowsAndBrightensToRest()
        {
            Assert.AreEqual(0.5f, ClearRevealMotion.RevealScale(0.5f, 0f));
            Assert.AreEqual(1f, ClearRevealMotion.RevealScale(0.5f, ClearRevealMotion.RevealSeconds));
            Assert.AreEqual(0.55f, ClearRevealMotion.RevealBrightness(0.55f, 0f), 0.0001f);
            Assert.AreEqual(1f, ClearRevealMotion.RevealBrightness(0.55f, ClearRevealMotion.RevealSeconds));
        }

        [Test]
        public void Reveal_IsShort()
        {
            Assert.Less(ClearRevealMotion.RevealSeconds, 0.5f);
        }
    }
}
