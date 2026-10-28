using UnityEngine;

namespace Game.UI
{
    /// <summary>Sizes for the dev level select row and fps readout, in dp. See <see cref="DevLevelSelectLayout"/>.</summary>
    public readonly struct DevLevelSelectMetrics
    {
        public DevLevelSelectMetrics(Vector2 button, float gap, int count, float rowCentreFromTop, float topReserve,
            float fpsCentreFromBottom)
        {
            Button = button;
            Gap = gap;
            Count = count;
            RowCentreFromTop = rowCentreFromTop;
            TopReserve = topReserve;
            FpsCentreFromBottom = fpsCentreFromBottom;
        }

        /// <summary>Size of one level button.</summary>
        public Vector2 Button { get; }

        public float Gap { get; }
        public int Count { get; }

        /// <summary>Distance from the top of the safe area to the centre of the button row.</summary>
        public float RowCentreFromTop { get; }

        /// <summary>Height of the board's HUD reserve at the top of the safe area (the row lives in it).</summary>
        public float TopReserve { get; }

        /// <summary>Distance from the bottom of the safe area to the centre of the fps readout.</summary>
        public float FpsCentreFromBottom { get; }

        public float RowWidth => Count * Button.x + (Count - 1) * Gap;

        /// <summary>Bottom edge of the button row, measured down from the top of the safe area.</summary>
        public float RowBottomFromTop => RowCentreFromTop + Button.y * 0.5f;

        /// <summary>True when the row sits entirely inside the top HUD reserve, clear of the board's playfield.</summary>
        public bool FitsInReserve => RowBottomFromTop <= TopReserve + 0.001f;

        /// <summary>
        /// Centre of button <paramref name="index"/> relative to the top-centre of the safe area (x right, y up).
        /// </summary>
        public Vector2 ButtonCentre(int index)
        {
            float left = -RowWidth * 0.5f + Button.x * 0.5f;
            return new Vector2(left + index * (Button.x + Gap), -RowCentreFromTop);
        }
    }

    /// <summary>
    /// Where the dev level select goes: one row of buttons centred in the board's top HUD reserve
    /// (<c>BoardLayoutSettings.TopReserveFraction</c> of the safe area), so it never covers the shelves. Buttons
    /// never go below the 48 dp touch floor. The fps readout sits in the bottom reserve. Pure math, Edit Mode tested.
    /// </summary>
    public static class DevLevelSelectLayout
    {
        public const float MinTouchDp = ResultPanelLayout.MinTouchDp;
        public const float PreferredButtonWidthDp = 56f;
        public const float GapDp = 8f;
        public const float SideMarginDp = 16f;
        public const int LabelFontDp = 18;
        public const int FpsFontDp = 14;
        public const float FpsHeightDp = 20f;

        public static DevLevelSelectMetrics Compute(Vector2 safeSizeDp, float topReserveFraction,
            float bottomReserveFraction, int count)
        {
            if (count < 1) count = 1;
            float available = Mathf.Max(0f, safeSizeDp.x - 2f * SideMarginDp - (count - 1) * GapDp);
            float width = Mathf.Clamp(available / count, MinTouchDp, PreferredButtonWidthDp);
            float height = MinTouchDp;

            float reserve = Mathf.Max(0f, safeSizeDp.y * topReserveFraction);
            // Centre the row in the reserve; if the reserve is shorter than a button, keep the touch floor and pin
            // the row to the top (FitsInReserve is then false; portrait phones have 64 dp or more).
            float rowCentre = Mathf.Max(reserve * 0.5f, height * 0.5f);

            float bottomReserve = Mathf.Max(0f, safeSizeDp.y * bottomReserveFraction);
            float fpsCentre = Mathf.Max(bottomReserve * 0.5f, FpsHeightDp * 0.5f);

            return new DevLevelSelectMetrics(new Vector2(width, height), GapDp, count, rowCentre, reserve, fpsCentre);
        }
    }
}
