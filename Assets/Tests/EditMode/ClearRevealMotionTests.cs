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

        /// <summary>
        /// Issue #21 replaced the plain ease-out slide (which never passed the front slot) with a small bounce: the tile
        /// still moves only forward on the straight line until it overshoots the front slot by a small, bounded amount,
        /// then only back onto it.
        /// </summary>
        [Test]
        public void Reveal_MovesForwardOnTheLine_OvershootsALittle_ThenSettlesBack()
        {
            var axis = (Front - Hidden).normalized;
            float total = Vector2.Distance(Hidden, Front);
            const int steps = 200;
            float previous = 0f;
            float peak = 0f;
            bool pastPeak = false;
            for (int i = 0; i <= steps; i++)
            {
                var p = ClearRevealMotion.RevealPosition(Hidden, Front, ClearRevealMotion.RevealSeconds * i / steps);
                Assert.AreEqual(Hidden.x, p.x, 0.001f, "Straight slide forward.");
                float along = Vector2.Dot(p - Hidden, axis) / total; // 0 at the hidden spot, 1 at the front slot
                if (!pastPeak && along < previous - 0.0001f) pastPeak = true;
                if (pastPeak) Assert.LessOrEqual(along, previous + 0.0001f, $"Only settles back after the bounce ({i}).");
                else Assert.GreaterOrEqual(along, previous - 0.0001f, $"Only moves forward before the bounce ({i}).");
                peak = Mathf.Max(peak, along);
                previous = along;
            }

            Assert.IsTrue(pastPeak, "It bounces.");
            Assert.Greater(peak, 1.02f, "Overshoot is visible.");
            Assert.LessOrEqual(peak, ClearRevealMotion.RevealPeakProgress + 0.001f);
            Assert.AreEqual(1f, previous, 0.0001f, "Ends exactly on the front slot.");
        }

        [Test]
        public void Reveal_BounceOvershoot_IsSmall()
        {
            // Back-ease constant 1.2: peak progress 1 + about 5 %.
            Assert.AreEqual(1.2f, ClearRevealMotion.RevealOvershoot);
            Assert.AreEqual(1.0529f, ClearRevealMotion.RevealPeakProgress, 0.001f);
            Assert.Less(ClearRevealMotion.RevealPeakProgress, 1.1f, "Small bounce, not a wobble.");
            Assert.AreEqual(0f, ClearRevealMotion.RevealProgress(0f));
            Assert.AreEqual(1f, ClearRevealMotion.RevealProgress(ClearRevealMotion.RevealSeconds));
        }

        [Test]
        public void Reveal_BrightnessNeverPassesFull_ScaleBouncesBackToOne()
        {
            float peakScale = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float t = ClearRevealMotion.RevealSeconds * i / 100f;
                Assert.LessOrEqual(ClearRevealMotion.RevealBrightness(0.55f, t), 1f);
                peakScale = Mathf.Max(peakScale, ClearRevealMotion.RevealScale(0.35f, t));
            }
            Assert.Greater(peakScale, 1f, "Scale bounces past rest with the slide.");
            Assert.Less(peakScale, 1.05f);
            Assert.AreEqual(1f, ClearRevealMotion.RevealScale(0.35f, ClearRevealMotion.RevealSeconds));
        }

        [Test]
        public void Clear_SquashesWideThenTall_AndIsRoundAtThePeak()
        {
            Assert.AreEqual(0f, ClearRevealMotion.ClearSquashAspect(0f));
            Assert.AreEqual(0f, ClearRevealMotion.ClearSquashAspect(ClearRevealMotion.ClearPopSeconds), "Uniform at the top of the pop.");
            Assert.AreEqual(0f, ClearRevealMotion.ClearSquashAspect(ClearRevealMotion.ClearSeconds));
            Assert.Greater(ClearRevealMotion.ClearSquashAspect(ClearRevealMotion.ClearPopSeconds * 0.25f), 0.08f, "Squash: wider than tall.");
            Assert.Less(ClearRevealMotion.ClearSquashAspect(ClearRevealMotion.ClearPopSeconds * 0.75f), -0.04f, "Then stretch: taller than wide.");
            for (int i = 0; i <= 100; i++)
            {
                Assert.LessOrEqual(Mathf.Abs(ClearRevealMotion.ClearSquashAspect(ClearRevealMotion.ClearPopSeconds * i / 100f)),
                    ClearRevealMotion.ClearSquash + 0.0001f);
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
