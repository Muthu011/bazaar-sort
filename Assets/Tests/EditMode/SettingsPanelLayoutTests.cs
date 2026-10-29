using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="SettingsPanelLayout"/> (issue #40): a small card with the Haptics row and Close.</summary>
    public class SettingsPanelLayoutTests
    {
        private const float E = 0.01f;

        [TestCase(360f, 742.33f)] // 1080x2400 @3 with notch and gesture bar
        [TestCase(360f, 616f)]    // shorter than 1080x1920 @3 (640 dp): a stricter case
        [TestCase(360f, 742f)]    // 720x1600 @2 with notch and gesture bar
        [TestCase(320f, 568f)]    // very small screen
        [TestCase(412f, 915f)]
        public void Card_FitsTheSafeArea_ButtonsAreTouchSized_AndNothingOverlaps(float w, float h)
        {
            var m = SettingsPanelLayout.Compute(new Vector2(w, h));
            Assert.IsTrue(m.CardFitsSafeArea, $"Card {m.Card} fits {w}x{h} dp.");
            Assert.LessOrEqual(m.Card.x, ResultPanelLayout.MaxCardWidthDp + E, "Small: no wider than the result cards.");
            Assert.GreaterOrEqual(m.HapticsToggle.width, 48f);
            Assert.GreaterOrEqual(m.HapticsToggle.height, 48f);
            Assert.GreaterOrEqual(m.Close.width, 48f);
            Assert.GreaterOrEqual(m.Close.height, 48f);

            var card = new Rect(-m.Card.x * 0.5f, -m.Card.y * 0.5f, m.Card.x, m.Card.y);
            var pieces = new[] { m.Title, m.HapticsLabel, m.HapticsToggle, m.Close };
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                Assert.IsTrue(p.xMin >= card.xMin - E && p.xMax <= card.xMax + E && p.yMin >= card.yMin - E && p.yMax <= card.yMax + E,
                    $"Piece {i} {p} inside the card {card}.");
                for (int j = i + 1; j < pieces.Length; j++)
                {
                    var q = pieces[j];
                    bool overlap = p.xMin < q.xMax - E && q.xMin < p.xMax - E && p.yMin < q.yMax - E && q.yMin < p.yMax - E;
                    Assert.IsFalse(overlap, $"Pieces {i} and {j} overlap.");
                }
            }
            Assert.Greater(m.HapticsToggle.xMin, m.HapticsLabel.xMax - E, "Toggle right of its label.");
            Assert.Less(m.Close.yMax, m.HapticsToggle.yMin, "Close under the Haptics row.");
        }
    }
}
