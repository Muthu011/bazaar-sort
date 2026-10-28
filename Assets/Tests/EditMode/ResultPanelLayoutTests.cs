using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="ResultPanelLayout"/>: card fits the safe area, buttons keep 48 dp tap targets.</summary>
    public class ResultPanelLayoutTests
    {
        // Safe areas in dp: 1080x1920 and 1080x2400 at 3 px/dp, a 2400 one with a notch and gesture bar, a small phone.
        private static readonly Vector2[] SafeAreas =
        {
            new Vector2(360f, 640f),
            new Vector2(360f, 800f),
            new Vector2(360f, 800f - 48f - 16f),
            new Vector2(320f, 480f),
        };

        [Test]
        public void Buttons_AreAtLeast48dp([Values(1, 2)] int buttons)
        {
            foreach (var safe in SafeAreas)
            {
                var m = ResultPanelLayout.Compute(safe, buttons);
                Assert.GreaterOrEqual(m.Button.y, ResultPanelLayout.MinTouchDp, $"{safe}: height");
                Assert.GreaterOrEqual(m.Button.x, ResultPanelLayout.MinTouchDp, $"{safe}: width");
            }
        }

        [Test]
        public void Card_FitsInsideTheSafeAreaWithMargin([Values(1, 2)] int buttons)
        {
            foreach (var safe in SafeAreas)
            {
                var m = ResultPanelLayout.Compute(safe, buttons);
                Assert.LessOrEqual(m.Card.x, safe.x - 2f * ResultPanelLayout.MarginDp + 0.001f, $"{safe}: width");
                Assert.LessOrEqual(m.Card.y, safe.y - 2f * ResultPanelLayout.MarginDp + 0.001f, $"{safe}: height");
                Assert.LessOrEqual(m.Card.x, ResultPanelLayout.MaxCardWidthDp);
            }
        }

        [Test]
        public void Buttons_StackInsideTheCard_WithoutOverlap()
        {
            var m = ResultPanelLayout.Compute(new Vector2(360f, 640f), 2);
            float half = m.Card.y * 0.5f;
            var first = m.ButtonCentre(0);
            var second = m.ButtonCentre(1);

            Assert.Less(first.y + m.Button.y * 0.5f, m.TitleCentre.y - m.TitleHeight * 0.5f + 0.001f, "Under the title.");
            Assert.LessOrEqual(second.y + m.Button.y * 0.5f, first.y - m.Button.y * 0.5f, "No overlap.");
            Assert.GreaterOrEqual(second.y - m.Button.y * 0.5f, -half + m.Padding - 0.001f, "Inside the card bottom.");
            Assert.LessOrEqual(m.Button.x, m.Card.x - 2f * m.Padding + 0.001f, "Inside the card sides.");
        }

        [Test]
        public void ShortSafeArea_ShrinksTitleFirst_ButKeepsTouchFloor()
        {
            var m = ResultPanelLayout.Compute(new Vector2(640f, 220f), 2);
            Assert.GreaterOrEqual(m.Button.y, ResultPanelLayout.MinTouchDp);
            Assert.GreaterOrEqual(m.TitleHeight, ResultPanelLayout.MinTitleHeightDp);
        }

        [Test]
        public void SafeAreaAnchors_AreFractionsOfTheScreen()
        {
            ResultPanelLayout.SafeAreaAnchors(new Rect(0f, 144f, 1080f, 2160f), 1080f, 2400f, out var min, out var max);
            Assert.AreEqual(new Vector2(0f, 0.06f), min);
            Assert.AreEqual(1f, max.x);
            Assert.AreEqual(0.96f, max.y, 0.0001f);
        }
    }
}
