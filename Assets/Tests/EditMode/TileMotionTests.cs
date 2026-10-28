using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="TileMotion"/>: glide back and shake, sampled over time.</summary>
    public class TileMotionTests
    {
        private static readonly Vector2 From = new Vector2(500f, 900f);
        private static readonly Vector2 Home = new Vector2(200f, 400f);
        private const float Amplitude = 10f;

        [Test]
        public void Glide_StartsAtDropPoint_AndEndsHome()
        {
            Assert.AreEqual(From, TileMotion.Position(From, Home, 0f, true, false, Amplitude));
            Assert.AreEqual(Home, TileMotion.Position(From, Home, TileMotion.ReturnSeconds, true, false, Amplitude));
            Assert.AreEqual(Home, TileMotion.Position(From, Home, 5f, true, true, Amplitude));
        }

        [Test]
        public void Glide_MovesTowardsHome_Monotonically()
        {
            float previous = float.MaxValue;
            for (int i = 0; i <= 10; i++)
            {
                float d = Vector2.Distance(Home, TileMotion.Position(From, Home, TileMotion.ReturnSeconds * i / 10f, true, false, Amplitude));
                Assert.LessOrEqual(d, previous + 0.001f);
                previous = d;
            }
        }

        [Test]
        public void Shake_StaysSideways_WithinAmplitude_AndMoves()
        {
            float maxOffset = 0f;
            for (int i = 0; i <= 100; i++)
            {
                var p = TileMotion.Position(From, Home, TileMotion.ShakeSeconds * i / 100f, false, true, Amplitude);
                Assert.AreEqual(Home.y, p.y, 0.0001f, "Shake is horizontal only.");
                Assert.LessOrEqual(Mathf.Abs(p.x - Home.x), Amplitude + 0.0001f);
                maxOffset = Mathf.Max(maxOffset, Mathf.Abs(p.x - Home.x));
            }
            Assert.Greater(maxOffset, Amplitude * 0.5f, "The shake is visible.");
        }

        [Test]
        public void ShakeAfterGlide_BeginsAtHome()
        {
            var p = TileMotion.Position(From, Home, TileMotion.ReturnSeconds, true, true, Amplitude);
            Assert.AreEqual(Home.x, p.x, 0.0001f);
            Assert.AreEqual(Home.y, p.y, 0.0001f);
        }

        [Test]
        public void Duration_AddsPhases()
        {
            Assert.AreEqual(0f, TileMotion.Duration(false, false));
            Assert.AreEqual(TileMotion.ReturnSeconds, TileMotion.Duration(true, false));
            Assert.AreEqual(TileMotion.ShakeSeconds, TileMotion.Duration(false, true));
            Assert.AreEqual(TileMotion.ReturnSeconds + TileMotion.ShakeSeconds, TileMotion.Duration(true, true), 0.0001f);
            Assert.Less(TileMotion.Duration(true, true), 0.5f, "Feedback stays snappy.");
        }
    }
}
