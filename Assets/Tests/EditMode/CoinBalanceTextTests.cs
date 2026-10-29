using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The HUD coin pill's number (issue #44).</summary>
    public class CoinBalanceTextTests
    {
        [TestCase(0, "0")]
        [TestCase(-3, "0")]
        [TestCase(20, "20")]
        [TestCase(1250, "1250")]
        [TestCase(9999, "9999")]
        [TestCase(10000, "9999+")]
        [TestCase(int.MaxValue, "9999+")]
        public void For_ShowsTheBalance_CappedAtTheMax(int balance, string expected)
        {
            Assert.AreEqual(expected, CoinBalanceText.For(balance));
        }
    }
}
