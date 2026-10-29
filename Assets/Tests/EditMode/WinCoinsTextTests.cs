using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>The Win card's "+N" coin line (issue #44, PR #63): keyed string, hidden at 0, and its place on the card.</summary>
    public class WinCoinsTextTests
    {
        [Test]
        public void Key_AndEnglishTemplate()
        {
            Assert.AreEqual("win.coins_earned", WinCoinsText.Key);
            Assert.AreEqual("+{0}", WinCoinsText.English);
        }

        [TestCase(10, "+10")]
        [TestCase(15, "+15")]
        [TestCase(20, "+20")]
        [TestCase(5, "+5")]
        [TestCase(12345, "+12345")]
        public void For_IsPlusTheCoins(int coins, string expected)
        {
            Assert.AreEqual(expected, WinCoinsText.For(coins));
            Assert.IsTrue(WinCoinsText.IsShown(coins));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void ZeroCoins_HidesTheLine(int coins)
        {
            Assert.AreEqual(string.Empty, WinCoinsText.For(coins));
            Assert.IsFalse(WinCoinsText.IsShown(coins));
        }

        [TestCase(360f, 640f)]
        [TestCase(360f, 742.33f)]
        [TestCase(412f, 915f)]
        public void CoinLine_SitsUnderTheStars_AboveTheButtons(float w, float h)
        {
            var safe = new Vector2(w, h);
            var without = ResultPanelLayout.Compute(safe, 2, 1, ResultPanelLayout.WinRowDp(false));
            var with = ResultPanelLayout.Compute(safe, 2, 1, ResultPanelLayout.WinRowDp(true));
            Assert.AreEqual(without.Card.y + ResultPanelLayout.CoinLineDp, with.Card.y, 0.001f, "The card grows by the line only.");
            Assert.AreEqual(without.StarRowCentre.y, ResultPanelLayout.WinStarsCentre(without).y, 0.001f,
                "Without the line the stars are where they always were.");

            var stars = ResultPanelLayout.WinStarsCentre(with);
            var line = ResultPanelLayout.WinCoinLineCentre(with);
            float titleBottom = with.TitleCentre.y - with.TitleHeight * 0.5f;
            float buttonTop = with.ButtonCentre(0).y + with.ButtonSize(0).y * 0.5f;
            Assert.LessOrEqual(stars.y + ResultPanelLayout.StarRowDp * 0.5f, titleBottom + 0.001f, "Stars under the title.");
            Assert.LessOrEqual(line.y + ResultPanelLayout.CoinLineDp * 0.5f, stars.y - ResultPanelLayout.StarRowDp * 0.5f + 0.001f, "Line under the stars.");
            Assert.GreaterOrEqual(line.y - ResultPanelLayout.CoinLineDp * 0.5f, buttonTop - 0.001f, "Line above the buttons.");
            Assert.GreaterOrEqual(ResultPanelLayout.CoinLineDp, ResultPanelLayout.CoinLineIconDp, "The coin fits the line.");
        }
    }
}
