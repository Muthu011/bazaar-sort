using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="UiFeel"/> (issue #21): button press, coin path, star timings.</summary>
    public class UiFeelTests
    {
        [Test]
        public void ButtonPress_GoesTo0_92_Quickly()
        {
            Assert.AreEqual(0.92f, UiFeel.PressedScale);
            Assert.Less(UiFeel.PressSeconds, 0.1f);
        }

        [Test]
        public void Coin_FliesFromTheTripleToTheCounter_OverACurve()
        {
            var from = new Vector2(-100f, 200f);
            var to = new Vector2(120f, -300f);
            Assert.AreEqual(from, UiFeel.CoinPosition(from, to, 0f));
            Assert.AreEqual(to, UiFeel.CoinPosition(from, to, 1f));
            var mid = UiFeel.CoinPosition(from, to, 0.5f);
            var line = (from + to) * 0.5f;
            Assert.AreEqual(UiFeel.CoinArcDp * 0.5f, mid.y - line.y, 0.01f, "Bows upwards.");
            Assert.AreEqual(0.8f, UiFeel.CoinScale(1f), 0.0001f, "Lands small, into the icon.");
            Assert.Greater(UiFeel.CoinScale(0.5f), 1f);
            Assert.Less(UiFeel.CoinSeconds, 0.6f);
        }

        [Test]
        public void Stars_AreThree_DroppingOneAtATime()
        {
            Assert.AreEqual(3, UiFeel.WinStars, "Muthu: always 3 stars for now.");
            for (int i = 1; i < UiFeel.WinStars; i++)
            {
                Assert.Greater(UiFeel.StarStart(i), UiFeel.StarStart(i - 1), "One at a time.");
            }
            Assert.AreEqual(UiFeel.StarStart(UiFeel.WinStars - 1) + UiFeel.StarDropSeconds + UiFeel.StarPunchSeconds,
                UiFeel.StarsSeconds, 0.0001f, "Confetti follows the last star's punch.");
            Assert.Less(UiFeel.StarsSeconds, 1.2f, "The player is not kept waiting.");
        }
    }
}
