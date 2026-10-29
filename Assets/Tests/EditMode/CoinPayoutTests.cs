using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="CoinPayout"/> and the Win coin flight maths in <see cref="UiFeel"/> (issue #44, PR #63): the pill keeps
    /// the old balance until a win's coins fly in, then climbs as each lands and ends exactly on the wallet.
    /// </summary>
    public class CoinPayoutTests
    {
        [TestCase(0, 0)]
        [TestCase(5, 1)]
        [TestCase(10, 2)]
        [TestCase(15, 3)]
        [TestCase(20, 4)]
        [TestCase(11, 3)]
        [TestCase(1000, UiFeel.CoinPool)]
        public void CoinsFlown_OnePerFiveCoins_UpToThePool(int coins, int flown)
        {
            Assert.AreEqual(flown, UiFeel.CoinsFlownFor(coins));
        }

        [Test]
        public void CoinsLanded_ClimbsInSteps_AndEndsOnTheTotal()
        {
            Assert.AreEqual(0, UiFeel.CoinsLanded(20, 4, 0));
            Assert.AreEqual(5, UiFeel.CoinsLanded(20, 4, 1));
            Assert.AreEqual(10, UiFeel.CoinsLanded(20, 4, 2));
            Assert.AreEqual(20, UiFeel.CoinsLanded(20, 4, 4));
            Assert.AreEqual(11, UiFeel.CoinsLanded(11, 3, 3), "Uneven amounts still end on the total.");
            int last = 0;
            for (int i = 0; i <= 6; i++)
            {
                int landed = UiFeel.CoinsLanded(1000, 6, i);
                Assert.GreaterOrEqual(landed, last);
                last = landed;
            }
            Assert.AreEqual(1000, last);
        }

        [Test]
        public void CoinsSeconds_CoverEveryStaggeredCoin()
        {
            Assert.AreEqual(0f, UiFeel.CoinsSecondsFor(0));
            Assert.AreEqual(UiFeel.CoinSeconds, UiFeel.CoinsSecondsFor(1), 1e-5f);
            Assert.AreEqual(3 * UiFeel.CoinStaggerSeconds + UiFeel.CoinSeconds, UiFeel.CoinsSecondsFor(4), 1e-5f);
        }

        [Test]
        public void Hold_KeepsTheOldBalance_UntilTheCoinsLand()
        {
            var payout = new CoinPayout();
            payout.Hold(20);
            Assert.AreEqual(100, payout.Shown(120), "Wallet 120 after the save; the pill still shows 100.");
            Assert.AreEqual(4, payout.Launch());
            Assert.IsTrue(payout.Flying);
            Assert.IsTrue(payout.Land());
            Assert.AreEqual(105, payout.Shown(120));
            payout.Land();
            payout.Land();
            Assert.AreEqual(115, payout.Shown(120));
            Assert.IsTrue(payout.Land());
            Assert.AreEqual(120, payout.Shown(120), "The last coin lands on the wallet.");
            Assert.AreEqual(0, payout.Held);
            Assert.IsFalse(payout.Flying);
            Assert.IsFalse(payout.Land(), "No coin left.");
        }

        [Test]
        public void Launch_OnlyOnce_AndNothingWhenNothingIsHeld()
        {
            var payout = new CoinPayout();
            Assert.AreEqual(0, payout.Launch());
            payout.Hold(0);
            Assert.AreEqual(0, payout.Launch(), "A 0-coin replay flies nothing.");
            Assert.AreEqual(50, payout.Shown(50));
            payout.Hold(10);
            Assert.AreEqual(2, payout.Launch());
            Assert.AreEqual(0, payout.Launch(), "Already on their way.");
        }

        [Test]
        public void Release_ShowsEverythingAtOnce()
        {
            var payout = new CoinPayout();
            payout.Hold(15);
            payout.Launch();
            payout.Land();
            payout.Release();
            Assert.AreEqual(0, payout.Held);
            Assert.AreEqual(80, payout.Shown(80));
            Assert.IsFalse(payout.Land());
        }

        [Test]
        public void HeldAfter_MatchesTheLandings()
        {
            var payout = new CoinPayout();
            payout.Hold(15);
            Assert.AreEqual(15, payout.HeldAfter(0));
            Assert.AreEqual(10, payout.HeldAfter(1));
            Assert.AreEqual(0, payout.HeldAfter(3));
            payout.Launch();
            payout.Land();
            Assert.AreEqual(payout.HeldAfter(1), payout.Held);
        }

        [Test]
        public void Shown_NeverGoesNegative()
        {
            var payout = new CoinPayout();
            payout.Hold(20);
            Assert.AreEqual(0, payout.Shown(5));
        }
    }
}
