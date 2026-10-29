using UnityEngine;

namespace Game.UI
{
    /// <summary>Sizes for the settings panel, in dp. Rectangles are relative to the card centre (x right, y up).</summary>
    public readonly struct SettingsPanelMetrics
    {
        public SettingsPanelMetrics(Vector2 safeSize, Vector2 card, Rect title, Rect hapticsLabel, Rect hapticsToggle, Rect close)
        {
            SafeSize = safeSize;
            Card = card;
            Title = title;
            HapticsLabel = hapticsLabel;
            HapticsToggle = hapticsToggle;
            Close = close;
        }

        public Vector2 SafeSize { get; }

        /// <summary>Card size; the card is centred in the safe area.</summary>
        public Vector2 Card { get; }

        public Rect Title { get; }

        /// <summary>"Haptics" text on the left of the row.</summary>
        public Rect HapticsLabel { get; }

        /// <summary>On / Off button on the right of the row.</summary>
        public Rect HapticsToggle { get; }

        public Rect Close { get; }

        public bool CardFitsSafeArea => Card.x <= SafeSize.x + 0.001f && Card.y <= SafeSize.y + 0.001f;
    }

    /// <summary>
    /// The small settings panel (issue #40) opened by the bottom bar's settings button: a card centred in the safe area
    /// with a title, one row for Haptics (moved here from the HUD row) and Close. Card and button sizes follow the
    /// result panels (<see cref="ResultPanelLayout"/>: 320 dp card, 24 dp padding, 56 dp buttons); every button is at
    /// least the 48 dp touch floor. Pure math, Edit Mode tested.
    /// </summary>
    public static class SettingsPanelLayout
    {
        public const float TitleHeightDp = 40f;
        public const float RowHeightDp = 56f;
        public const float ToggleWidthDp = 96f;
        public const float ToggleHeightDp = ResultPanelLayout.MinTouchDp;
        public const float GapDp = 16f;
        public const int TitleFontDp = 24;
        public const int RowFontDp = 20;
        public const int ButtonFontDp = ResultPanelLayout.ButtonFontDp;

        public static SettingsPanelMetrics Compute(Vector2 safeSizeDp)
        {
            float pad = ResultPanelLayout.PaddingDp;
            float width = Mathf.Min(ResultPanelLayout.MaxCardWidthDp, safeSizeDp.x - 2f * ResultPanelLayout.MarginDp);
            width = Mathf.Max(width, ToggleWidthDp + 2f * pad + ResultPanelLayout.MinTouchDp);
            float buttonHeight = ResultPanelLayout.ButtonHeightDp;
            float height = pad + TitleHeightDp + GapDp + RowHeightDp + GapDp + buttonHeight + pad;
            var card = new Vector2(width, height);

            float left = -width * 0.5f + pad;
            float inner = width - 2f * pad;
            float y = height * 0.5f - pad;
            var title = new Rect(left, y - TitleHeightDp, inner, TitleHeightDp);
            y -= TitleHeightDp + GapDp;
            float rowCentre = y - RowHeightDp * 0.5f;
            var toggle = new Rect(left + inner - ToggleWidthDp, rowCentre - ToggleHeightDp * 0.5f, ToggleWidthDp, ToggleHeightDp);
            var label = new Rect(left, y - RowHeightDp, inner - ToggleWidthDp - GapDp, RowHeightDp);
            y -= RowHeightDp + GapDp;
            var close = new Rect(left, y - buttonHeight, inner, buttonHeight);
            return new SettingsPanelMetrics(safeSizeDp, card, title, label, toggle, close);
        }
    }
}
