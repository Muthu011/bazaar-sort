using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="ClearCounter"/> (issue #21): the HUD clear tally. Since PR #63 a clear flies no coin, so the tally counts
    /// every clear at once; the coin-landing checks went with the flight.
    /// </summary>
    public class ClearCounterTests
    {
        [Test]
        public void StartsAtZero()
        {
            var c = new ClearCounter();
            Assert.AreEqual(0, c.Cleared);
        }

        [Test]
        public void AClear_CountsAtOnce()
        {
            var c = new ClearCounter();
            c.AddClear();
            Assert.AreEqual(1, c.Cleared);
        }

        [Test]
        public void EachClearCountsExactlyOnce()
        {
            var c = new ClearCounter();
            c.AddClear();
            c.AddClear();
            c.AddClear();
            Assert.AreEqual(3, c.Cleared);
        }

        [Test]
        public void Reset_OnLoad_GoesBackToZero()
        {
            var c = new ClearCounter();
            c.AddClear();
            c.AddClear();
            c.Reset();
            Assert.AreEqual(0, c.Cleared);
            c.AddClear();
            Assert.AreEqual(1, c.Cleared, "Counts again from zero after a load.");
        }

        [Test]
        public void ManyClears_AreAllCounted()
        {
            var c = new ClearCounter();
            for (int i = 0; i < 100; i++) c.AddClear();
            Assert.AreEqual(100, c.Cleared);
        }
    }
}
