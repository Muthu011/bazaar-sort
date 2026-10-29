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
        public void TwoLineTitle_IsTaller_AndStillFitsWith48dpButtons()
        {
            foreach (var safe in SafeAreas)
            {
                var one = ResultPanelLayout.Compute(safe, 1);
                var two = ResultPanelLayout.Compute(safe, 1, 2);
                Assert.Greater(two.TitleHeight, one.TitleHeight, $"{safe}: room for a second line");
                Assert.GreaterOrEqual(two.Button.y, ResultPanelLayout.MinTouchDp, $"{safe}: button height");
                Assert.GreaterOrEqual(two.Button.x, ResultPanelLayout.MinTouchDp, $"{safe}: button width");
                Assert.LessOrEqual(two.Card.y, safe.y - 2f * ResultPanelLayout.MarginDp + 0.001f, $"{safe}: card height");
                Assert.Less(two.ButtonCentre(0).y + two.Button.y * 0.5f, two.TitleCentre.y - two.TitleHeight * 0.5f + 0.001f,
                    $"{safe}: button under the title");
            }
        }

        [Test]
        public void StarRow_SitsBetweenTitleAndButtons_AndTheCardStillFits()
        {
            foreach (var safe in new[] { new Vector2(360f, 640f), new Vector2(360f, 760f), new Vector2(412f, 915f) })
            {
                var plain = ResultPanelLayout.Compute(safe, 2);
                var m = ResultPanelLayout.Compute(safe, 2, 1, ResultPanelLayout.StarRowDp);
                Assert.AreEqual(0f, plain.StarRow, "No star row unless asked.");
                Assert.AreEqual(ResultPanelLayout.StarRowDp, m.StarRow);
                Assert.AreEqual(plain.Card.y + ResultPanelLayout.StarRowDp, m.Card.y, 0.001f, "Card grows by the row.");
                Assert.LessOrEqual(m.Card.y, safe.y - 2f * ResultPanelLayout.MarginDp + 0.001f);

                float titleBottom = m.TitleCentre.y - m.TitleHeight * 0.5f;
                float rowTop = m.StarRowCentre.y + m.StarRow * 0.5f;
                float rowBottom = m.StarRowCentre.y - m.StarRow * 0.5f;
                float firstButtonTop = m.ButtonCentre(0).y + m.Button.y * 0.5f;
                Assert.LessOrEqual(rowTop, titleBottom + 0.001f, "Under the title.");
                Assert.GreaterOrEqual(rowBottom, firstButtonTop - 0.001f, "Above the buttons.");
                Assert.GreaterOrEqual(m.StarRow, ResultPanelLayout.StarDp, "Stars fit the row.");
                Assert.GreaterOrEqual(m.Button.y, ResultPanelLayout.MinTouchDp);
            }
        }

        // ---- Lead button: the Fail card's rewarded Continue above Retry (issue #41) ----

        [Test]
        public void WithoutLeadButton_EveryButtonIsTheSameSize([Values(1, 2)] int buttons)
        {
            var m = ResultPanelLayout.Compute(new Vector2(360f, 640f), buttons);
            Assert.AreEqual(m.Button, m.LeadButton);
            Assert.AreEqual(m.Button, m.ButtonSize(0));
        }

        [Test]
        public void LeadButton_IsTallerThanTheOthers_AndOnTop()
        {
            var m = ResultPanelLayout.Compute(new Vector2(360f, 640f), 2, leadButton: true);
            Assert.AreEqual(ResultPanelLayout.LeadButtonHeightDp, m.LeadButton.y, 0.001f);
            Assert.AreEqual(ResultPanelLayout.ButtonHeightDp, m.Button.y, 0.001f);
            Assert.Greater(m.ButtonSize(0).y, m.ButtonSize(1).y, "The main button is larger.");
            Assert.AreEqual(m.Button.x, m.LeadButton.x, "Same width.");
            Assert.Greater(m.ButtonCentre(0).y, m.ButtonCentre(1).y, "The main button is on top.");

            var plain = ResultPanelLayout.Compute(new Vector2(360f, 640f), 2);
            Assert.AreEqual(plain.Card.y + ResultPanelLayout.LeadButtonHeightDp - ResultPanelLayout.ButtonHeightDp, m.Card.y,
                0.001f, "The card grows by the extra height.");
        }

        [Test]
        public void LeadButton_StacksInsideTheCard_WithoutOverlap_OnEveryPhone()
        {
            foreach (var safe in SafeAreas)
            {
                var m = ResultPanelLayout.Compute(safe, 2, leadButton: true);
                float half = m.Card.y * 0.5f;
                var lead = m.ButtonCentre(0);
                var second = m.ButtonCentre(1);
                Assert.LessOrEqual(lead.y + m.LeadButton.y * 0.5f, m.TitleCentre.y - m.TitleHeight * 0.5f + 0.001f, $"{safe}: under the title");
                Assert.LessOrEqual(second.y + m.Button.y * 0.5f, lead.y - m.LeadButton.y * 0.5f - m.ButtonGap + 0.001f, $"{safe}: gap, no overlap");
                Assert.GreaterOrEqual(second.y - m.Button.y * 0.5f, -half + m.Padding - 0.001f, $"{safe}: inside the card bottom");
                Assert.LessOrEqual(m.Card.y, safe.y - 2f * ResultPanelLayout.MarginDp + 0.001f, $"{safe}: card fits");
                Assert.GreaterOrEqual(m.Button.y, ResultPanelLayout.MinTouchDp, $"{safe}: touch floor");
                Assert.GreaterOrEqual(m.LeadButton.y, m.Button.y, $"{safe}: lead never smaller");
            }
        }

        [Test]
        public void ShortSafeArea_GivesUpTheLeadButtonsExtraHeight_BeforeTheTouchFloor()
        {
            var m = ResultPanelLayout.Compute(new Vector2(640f, 250f), 2, leadButton: true);
            Assert.GreaterOrEqual(m.Button.y, ResultPanelLayout.MinTouchDp);
            Assert.GreaterOrEqual(m.LeadButton.y, m.Button.y);
            Assert.Less(m.LeadButton.y, ResultPanelLayout.LeadButtonHeightDp, "Lead shrinks on a short screen.");
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
