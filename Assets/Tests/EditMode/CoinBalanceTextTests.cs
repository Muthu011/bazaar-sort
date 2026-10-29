using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The HUD coin pill's number and width (issue #44, PR #63): whole digits up to 5, "99K+" above; the pill widens to
    /// fit (84 dp minimum) and the text is never shrunk.
    /// </summary>
    public class CoinBalanceTextTests
    {
        [TestCase(0, "0")]
        [TestCase(-3, "0")]
        [TestCase(7, "7")]
        [TestCase(20, "20")]
        [TestCase(999, "999")]
        [TestCase(1250, "1250")]
        [TestCase(9999, "9999")]
        [TestCase(10000, "10000")]
        [TestCase(99999, "99999")]
        [TestCase(100000, "99K+")]
        [TestCase(int.MaxValue, "99K+")]
        public void For_ShowsUpTo5Digits_ThenTheOverflowText(int balance, string expected)
        {
            Assert.AreEqual(expected, CoinBalanceText.For(balance));
        }

        [Test]
        public void MaxShown_IsFiveDigits()
        {
            Assert.AreEqual(99999, CoinBalanceText.MaxShown);
            Assert.AreEqual(HudLayout.CounterMaxDigits, CoinBalanceText.MaxShown.ToString().Length);
        }

        // Width = 38 dp (icon) + 11.5 dp per digit + 8 dp, never under 84 dp.
        [TestCase(7, 84f, TestName = "PillWidth_1Digit_IsTheMinimum")]
        [TestCase(20, 84f, TestName = "PillWidth_2Digits_IsTheMinimum")]
        [TestCase(999, 84f, TestName = "PillWidth_3Digits_IsTheMinimum")]
        [TestCase(1250, 92f, TestName = "PillWidth_4Digits_Widens")]
        [TestCase(99999, 103.5f, TestName = "PillWidth_5Digits_IsTheMaximum")]
        [TestCase(100000, 97f, TestName = "PillWidth_Overflow_FitsInsideTheMaximum")]
        public void PillWidth_GrowsWithTheText(int balance, float expected)
        {
            Assert.AreEqual(expected, HudLayout.CounterWidthFor(CoinBalanceText.For(balance)), 0.001f);
        }

        [Test]
        public void PillWidth_NeverShrinksBelow84_NorGrowsPastFiveDigits()
        {
            Assert.AreEqual(84f, HudLayout.CounterMinWidthDp);
            Assert.AreEqual(84f, HudLayout.CounterWidthFor(string.Empty));
            Assert.AreEqual(84f, HudLayout.CounterWidthFor(null));
            Assert.AreEqual(HudLayout.CounterMaxWidthDp, HudLayout.CounterWidthFor("1234567890"));
            for (int balance = 0; balance <= 200000; balance = balance * 3 + 1)
            {
                float width = HudLayout.CounterWidthFor(CoinBalanceText.For(balance));
                Assert.GreaterOrEqual(width, HudLayout.CounterMinWidthDp, $"balance {balance}");
                Assert.LessOrEqual(width, HudLayout.CounterMaxWidthDp, $"balance {balance}");
            }
        }

        [Test]
        public void PillWidth_EveryShownText_FitsAtTheFullFontSize()
        {
            // The text box is the pill less the icon and the right inset; the glyphs at full size must fit in it.
            foreach (var text in new[] { "0", "88", "888", "8888", "88888", CoinBalanceText.OverflowText })
            {
                float box = HudLayout.CounterWidthFor(text) - HudLayout.CounterTextLeftDp - HudLayout.CounterTextRightDp;
                float glyphs = 0f;
                foreach (char c in text)
                {
                    glyphs += char.IsDigit(c) ? HudLayout.CounterDigitDp : c == '+' ? HudLayout.CounterPlusDp : HudLayout.CounterWideGlyphDp;
                }
                Assert.GreaterOrEqual(box + 0.001f, glyphs, text);
            }
        }
    }
}
