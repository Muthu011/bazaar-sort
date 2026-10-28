using Game.UI;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="HudLayout"/> (issue #21): counter and haptics toggle in the bottom reserve.</summary>
    public class HudLayoutTests
    {
        private static readonly float Reserve = BoardLayoutSettings.Default.BottomReserveFraction;

        [TestCase(360f, 640f)]
        [TestCase(360f, 800f)]
        [TestCase(412f, 915f)]
        public void Toggle_IsAtLeast48dp_AndInsideTheBottomReserve(float w, float h)
        {
            var m = HudLayout.Compute(new Vector2(w, h), Reserve);
            Assert.GreaterOrEqual(m.Toggle.x, 48f);
            Assert.GreaterOrEqual(m.Toggle.y, 48f);
            Assert.IsTrue(m.FitsInReserve, "Clear of the shelves.");
            Assert.GreaterOrEqual(m.ToggleCentre.y - m.Toggle.y * 0.5f, 0f, "Above the safe-area bottom.");
            Assert.LessOrEqual(m.ToggleCentre.x + m.Toggle.x * 0.5f, -HudLayout.SideMarginDp + 0.001f, "Margin from the right edge.");
        }

        [Test]
        public void Counter_SitsLeftOfTheToggle_WithoutOverlap()
        {
            var m = HudLayout.Compute(new Vector2(360f, 640f), Reserve);
            float counterRight = m.CounterCentre.x + m.Counter.x * 0.5f;
            float toggleLeft = m.ToggleCentre.x - m.Toggle.x * 0.5f;
            Assert.LessOrEqual(counterRight, toggleLeft - HudLayout.GapDp + 0.001f);
            Assert.GreaterOrEqual(m.CounterCentre.x - m.Counter.x * 0.5f, -360f * 0.5f, "Stays in the right half, clear of the dev fps readout.");
            Assert.AreEqual(m.ToggleCentre.y, m.CounterCentre.y, 0.001f, "One row.");
        }

        [Test]
        public void TinyReserve_KeepsTheTouchFloor()
        {
            var m = HudLayout.Compute(new Vector2(360f, 200f), Reserve);
            Assert.AreEqual(48f, m.Toggle.y);
            Assert.AreEqual(24f, m.ToggleCentre.y, 0.001f);
        }
    }
}
