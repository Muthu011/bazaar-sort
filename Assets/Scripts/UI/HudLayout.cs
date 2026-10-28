using UnityEngine;

namespace Game.UI
{
    /// <summary>Where the HUD pieces go, in dp, relative to the bottom-right corner of the safe area (x left is negative).</summary>
    public readonly struct HudMetrics
    {
        public HudMetrics(Vector2 toggle, Vector2 toggleCentre, Vector2 counter, Vector2 counterCentre, float bottomReserve)
        {
            Toggle = toggle;
            ToggleCentre = toggleCentre;
            Counter = counter;
            CounterCentre = counterCentre;
            BottomReserve = bottomReserve;
        }

        /// <summary>Haptics toggle size.</summary>
        public Vector2 Toggle { get; }

        /// <summary>Haptics toggle centre from the safe area's bottom-right corner.</summary>
        public Vector2 ToggleCentre { get; }

        /// <summary>Clear counter pill size.</summary>
        public Vector2 Counter { get; }

        /// <summary>Clear counter centre from the safe area's bottom-right corner.</summary>
        public Vector2 CounterCentre { get; }

        /// <summary>Height of the board's bottom reserve (the HUD row lives in it).</summary>
        public float BottomReserve { get; }

        /// <summary>True when the toggle sits inside the bottom reserve, clear of the shelves.</summary>
        public bool FitsInReserve => ToggleCentre.y + Toggle.y * 0.5f <= BottomReserve + 0.001f;
    }

    /// <summary>
    /// The in-game HUD row (issue #21): the clear counter and the haptics toggle, right-aligned in the board's bottom
    /// reserve (<c>BoardLayoutSettings.BottomReserveFraction</c>, kept for the booster bar, where coins will belong).
    /// The top reserve holds the status line and, in development builds, the level select, and the bottom-left corner
    /// the dev fps readout, so the right side of the bottom row is free on every build. The toggle is never below the
    /// 48 dp touch floor. Pure math, Edit Mode tested.
    /// </summary>
    public static class HudLayout
    {
        public const float MinTouchDp = ResultPanelLayout.MinTouchDp;
        public const float ToggleWidthDp = 72f;
        public const float CounterWidthDp = 84f;
        public const float CounterHeightDp = 40f;
        public const float SideMarginDp = 16f;
        public const float GapDp = 8f;
        public const int CounterFontDp = 20;
        public const int ToggleFontDp = 13;

        public static HudMetrics Compute(Vector2 safeSizeDp, float bottomReserveFraction)
        {
            float reserve = Mathf.Max(0f, safeSizeDp.y * bottomReserveFraction);
            var toggle = new Vector2(ToggleWidthDp, MinTouchDp);
            var counter = new Vector2(CounterWidthDp, CounterHeightDp);
            // Centred in the reserve; on a reserve shorter than the toggle, keep the touch floor and sit on the bottom.
            float y = Mathf.Max(reserve * 0.5f, toggle.y * 0.5f);
            var toggleCentre = new Vector2(-SideMarginDp - toggle.x * 0.5f, y);
            var counterCentre = new Vector2(toggleCentre.x - toggle.x * 0.5f - GapDp - counter.x * 0.5f, y);
            return new HudMetrics(toggle, toggleCentre, counter, counterCentre, reserve);
        }
    }
}
