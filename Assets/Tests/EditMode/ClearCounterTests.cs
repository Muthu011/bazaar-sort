using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="ClearCounter"/> (issue #21): the display-only HUD clear count.</summary>
    public class ClearCounterTests
    {
        [Test]
        public void StartsAtZero()
        {
            var c = new ClearCounter();
            Assert.AreEqual(0, c.Shown);
            Assert.AreEqual("0", c.Text);
        }

        [Test]
        public void ClearWithACoin_TicksUpWhenTheCoinLands()
        {
            var c = new ClearCounter();
            c.AddClear(true);
            Assert.AreEqual(1, c.Cleared);
            Assert.AreEqual(0, c.Shown, "Waits for the coin.");
            Assert.IsTrue(c.CoinLanded());
            Assert.AreEqual(1, c.Shown);
            Assert.AreEqual("1", c.Text);
        }

        [Test]
        public void ClearWithoutACoin_TicksUpAtOnce()
        {
            var c = new ClearCounter();
            c.AddClear(false);
            Assert.AreEqual(1, c.Shown);
        }

        [Test]
        public void EachLandingAddsExactlyOne_AndNeverPassesTheClears()
        {
            var c = new ClearCounter();
            c.AddClear(true);
            c.AddClear(true);
            c.AddClear(false);
            Assert.AreEqual(1, c.Shown);
            c.CoinLanded();
            Assert.AreEqual(2, c.Shown);
            c.CoinLanded();
            Assert.AreEqual(3, c.Shown);
            Assert.IsFalse(c.CoinLanded(), "No coin left in flight.");
            Assert.AreEqual(3, c.Shown);
        }

        [Test]
        public void Reset_OnLoad_GoesBackToZero_AndDropsFlyingCoins()
        {
            var c = new ClearCounter();
            c.AddClear(false);
            c.AddClear(true);
            c.Reset();
            Assert.AreEqual(0, c.Shown);
            Assert.AreEqual(0, c.Cleared);
            Assert.AreEqual("0", c.Text);
            Assert.IsFalse(c.CoinLanded(), "A coin from the old level does not count.");
            Assert.AreEqual(0, c.Shown);
        }

        [Test]
        public void Text_IsCachedAndCapped()
        {
            var c = new ClearCounter();
            for (int i = 0; i < 99; i++) c.AddClear(false);
            Assert.AreEqual("99", c.Text);
            Assert.AreSame(c.Text, c.Text, "Same string each time: no allocation.");
            c.AddClear(false);
            Assert.AreEqual("99+", c.Text);
        }
    }
}
