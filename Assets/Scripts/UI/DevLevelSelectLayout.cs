using UnityEngine;

namespace Game.UI
{
    /// <summary>Sizes for the dev level select, in dp. See <see cref="DevLevelSelectLayout"/>.</summary>
    public readonly struct DevLevelSelectMetrics
    {
        public DevLevelSelectMetrics(Vector2 toggle, Vector2 toggleCentre, float topReserve, int count, int columns,
            float cell, Vector2 card, Vector2 safeSize, Vector2 action, Vector2 fpsCentre, Vector2 worstCentre)
        {
            Toggle = toggle;
            ToggleCentre = toggleCentre;
            TopReserve = topReserve;
            Count = count;
            Columns = columns;
            Cell = cell;
            Card = card;
            SafeSize = safeSize;
            Action = action;
            FpsCentre = fpsCentre;
            WorstCentre = worstCentre;
        }

        /// <summary>The "Levels" button that opens the panel.</summary>
        public Vector2 Toggle { get; }

        /// <summary>Toggle centre from the top-left corner of the safe area (x right, y up: y is negative).</summary>
        public Vector2 ToggleCentre { get; }

        /// <summary>Height of the board's HUD reserve at the top of the safe area (the toggle lives in it).</summary>
        public float TopReserve { get; }

        public int Count { get; }
        public int Columns { get; }
        public int Rows => (Count + Columns - 1) / Columns;

        /// <summary>Side of one square level cell.</summary>
        public float Cell { get; }

        public float Gap => DevLevelSelectLayout.GapDp;
        public float Padding => DevLevelSelectLayout.CardPaddingDp;

        /// <summary>Panel card size; the card is centred in the safe area.</summary>
        public Vector2 Card { get; }

        public Vector2 SafeSize { get; }

        /// <summary>Size of each of the two bottom buttons (Reset progress, Close).</summary>
        public Vector2 Action { get; }

        /// <summary>
        /// Centre of the fps line ("60 fps") from the top-right corner of the safe area (x left and y down are
        /// negative). The readout sits at the right of the top bar (issue #40), right-aligned, the frame-time line under it.
        /// </summary>
        public Vector2 FpsCentre { get; }

        /// <summary>Centre of the worst-frame line ("max 17 ms"), same frame as <see cref="FpsCentre"/>.</summary>
        public Vector2 WorstCentre { get; }

        /// <summary>Size of each fps readout line.</summary>
        public Vector2 FpsLine => new Vector2(DevLevelSelectLayout.FpsWidthDp, DevLevelSelectLayout.FpsHeightDp);

        /// <summary>True when both fps lines sit inside the top reserve.</summary>
        public bool FpsFitsInReserve => -WorstCentre.y + DevLevelSelectLayout.FpsHeightDp * 0.5f <= TopReserve + 0.001f
            && -FpsCentre.y - DevLevelSelectLayout.FpsHeightDp * 0.5f >= -0.001f;

        public float GridWidth => Columns * Cell + (Columns - 1) * Gap;
        public float GridHeight => Rows * Cell + (Rows - 1) * Gap;

        /// <summary>True when the toggle sits entirely inside the top HUD reserve, clear of the shelves.</summary>
        public bool ToggleFitsInReserve => -ToggleCentre.y + Toggle.y * 0.5f <= TopReserve + 0.001f;

        /// <summary>True when the whole card fits inside the safe area.</summary>
        public bool CardFitsSafeArea => Card.x <= SafeSize.x + 0.001f && Card.y <= SafeSize.y + 0.001f;

        /// <summary>Title centre relative to the card centre.</summary>
        public Vector2 TitleCentre => new Vector2(0f, Card.y * 0.5f - Padding - DevLevelSelectLayout.TitleHeightDp * 0.5f);

        /// <summary>Centre of level cell <paramref name="index"/> relative to the card centre (row by row, left to right).</summary>
        public Vector2 CellCentre(int index)
        {
            int row = index / Columns;
            int column = index % Columns;
            float left = -GridWidth * 0.5f + Cell * 0.5f;
            float top = Card.y * 0.5f - Padding - DevLevelSelectLayout.TitleHeightDp - Gap - Cell * 0.5f;
            return new Vector2(left + column * (Cell + Gap), top - row * (Cell + Gap));
        }

        /// <summary>Centre of bottom button <paramref name="index"/> (0 Reset progress, 1 Close) relative to the card centre.</summary>
        public Vector2 ActionCentre(int index)
        {
            float y = -Card.y * 0.5f + Padding + Action.y * 0.5f;
            float x = (index == 0 ? -1f : 1f) * (Action.x + Gap) * 0.5f;
            return new Vector2(x, y);
        }
    }

    /// <summary>
    /// Where the dev level select goes (development builds only, issue #22): a "Levels" button at the left of the
    /// board's top HUD reserve (<c>BoardLayoutSettings.TopReserveFraction</c>), clear of the shelves and of the centred
    /// status line, opens a card centred in the safe area with one square cell per level, up to
    /// <see cref="MaxColumns"/> per row, and Reset progress / Close below. Every button is at least the 48 dp touch floor.
    /// The fps readout sits at the right of the top reserve (issue #40; the bottom bar holds boosters, coins and
    /// settings). Pure math, Edit Mode tested.
    /// </summary>
    public static class DevLevelSelectLayout
    {
        public const float MinTouchDp = ResultPanelLayout.MinTouchDp;
        public const float MaxCellDp = 60f;
        public const int MaxColumns = 5;
        public const float GapDp = 8f;
        public const float SideMarginDp = 16f;
        public const float CardPaddingDp = 16f;
        public const float TitleHeightDp = 32f;
        public const float ToggleWidthDp = 72f;
        public const float StarDp = 12f;
        public const int TitleFontDp = 20;
        public const int LabelFontDp = 18;
        public const int ActionFontDp = 15;
        public const int FpsFontDp = 14;
        public const float FpsHeightDp = 20f;
        public const float FpsWidthDp = 90f;

        public static DevLevelSelectMetrics Compute(Vector2 safeSizeDp, float topReserveFraction,
            float bottomReserveFraction, int count)
        {
            if (count < 1) count = 1;

            float reserve = Mathf.Max(0f, safeSizeDp.y * topReserveFraction);
            var toggle = new Vector2(ToggleWidthDp, MinTouchDp);
            // Centred in the reserve; on a reserve shorter than the toggle, keep the touch floor and pin it to the top.
            float toggleY = Mathf.Max(reserve * 0.5f, toggle.y * 0.5f);
            var toggleCentre = new Vector2(SideMarginDp + toggle.x * 0.5f, -toggleY);

            // Grid: as many columns as fit at the touch floor (max MaxColumns), cells as big as fit (max MaxCellDp).
            float inner = Mathf.Max(MinTouchDp, safeSizeDp.x - 2f * SideMarginDp - 2f * CardPaddingDp);
            int columns = Mathf.Clamp(Mathf.FloorToInt((inner + GapDp) / (MinTouchDp + GapDp)), 1, MaxColumns);
            columns = Mathf.Min(columns, count);
            float cell = Mathf.Clamp((inner - (columns - 1) * GapDp) / columns, MinTouchDp, MaxCellDp);
            int rows = (count + columns - 1) / columns;

            float gridWidth = columns * cell + (columns - 1) * GapDp;
            float gridHeight = rows * cell + (rows - 1) * GapDp;
            // The bottom buttons need room for their labels even when the grid is narrow (few levels).
            float contentWidth = Mathf.Max(gridWidth, 2f * 120f + GapDp);
            contentWidth = Mathf.Min(contentWidth, inner);
            var action = new Vector2((contentWidth - GapDp) * 0.5f, MinTouchDp);
            var card = new Vector2(contentWidth + 2f * CardPaddingDp,
                CardPaddingDp + TitleHeightDp + GapDp + gridHeight + 2f * GapDp + action.y + CardPaddingDp);

            // Fps readout: two right-aligned lines centred in the top reserve (pinned to the top when it is short).
            float fpsMid = Mathf.Max(reserve * 0.5f, FpsHeightDp);
            float fpsX = -SideMarginDp - FpsWidthDp * 0.5f;
            var fpsCentre = new Vector2(fpsX, -(fpsMid - FpsHeightDp * 0.5f));
            var worstCentre = new Vector2(fpsX, -(fpsMid + FpsHeightDp * 0.5f));

            return new DevLevelSelectMetrics(toggle, toggleCentre, reserve, count, columns, cell, card, safeSizeDp,
                action, fpsCentre, worstCentre);
        }
    }
}
