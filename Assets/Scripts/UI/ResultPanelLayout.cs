using UnityEngine;

namespace Game.UI
{
    /// <summary>Sizes of a result panel card, in dp (the panel canvas works in dp: 1 canvas unit = 1 dp).</summary>
    public readonly struct ResultPanelMetrics
    {
        public ResultPanelMetrics(Vector2 card, float titleHeight, Vector2 button, float buttonGap, float padding,
            float starRow = 0f)
        {
            Card = card;
            TitleHeight = titleHeight;
            Button = button;
            ButtonGap = buttonGap;
            Padding = padding;
            StarRow = starRow;
        }

        /// <summary>Height of the star row between the title and the buttons (Win panel), or 0 when there is none.</summary>
        public float StarRow { get; }

        /// <summary>Centre of the star row relative to the card centre.</summary>
        public Vector2 StarRowCentre => new Vector2(0f, Card.y * 0.5f - Padding - TitleHeight - StarRow * 0.5f);

        /// <summary>Card size; the card is centred in the safe area.</summary>
        public Vector2 Card { get; }

        /// <summary>Height of the title line at the top of the card.</summary>
        public float TitleHeight { get; }

        /// <summary>Size of every button; buttons are stacked under the title, centred.</summary>
        public Vector2 Button { get; }

        public float ButtonGap { get; }
        public float Padding { get; }

        /// <summary>
        /// Centre of button <paramref name="index"/> relative to the card centre (y up). Button 0 sits just under the
        /// title; the rest follow downwards.
        /// </summary>
        public Vector2 ButtonCentre(int index)
        {
            float top = Card.y * 0.5f - Padding - TitleHeight - StarRow - ButtonGap;
            float y = top - Button.y * 0.5f - index * (Button.y + ButtonGap);
            return new Vector2(0f, y);
        }

        /// <summary>Centre of the title relative to the card centre.</summary>
        public Vector2 TitleCentre => new Vector2(0f, Card.y * 0.5f - Padding - TitleHeight * 0.5f);
    }

    /// <summary>
    /// Layout maths for the Win, Stuck and More-levels-soon panels, pure so it is Edit Mode tested: a card centred in the safe area,
    /// never wider than <see cref="MaxCardWidthDp"/>, kept <see cref="MarginDp"/> clear of the safe-area edges, with
    /// buttons at least <see cref="MinTouchDp"/> tall (style guide: 48 dp tap targets).
    /// </summary>
    public static class ResultPanelLayout
    {
        /// <summary>Style guide floor for a tap target.</summary>
        public const float MinTouchDp = 48f;

        /// <summary>Preferred button height.</summary>
        public const float ButtonHeightDp = 56f;

        public const float MaxCardWidthDp = 320f;
        public const float MarginDp = 16f;
        public const float PaddingDp = 24f;
        public const float TitleHeightDp = 56f;
        public const float MinTitleHeightDp = 32f;

        /// <summary>Extra title height for each line after the first (title font plus line spacing).</summary>
        public const float TitleLineDp = 36f;
        public const float ButtonGapDp = 12f;

        /// <summary>Title font size in dp.</summary>
        public const int TitleFontDp = 30;

        /// <summary>Button label font size in dp.</summary>
        public const int ButtonFontDp = 22;

        /// <summary>Height of the Win panel's star row (issue #21), and the star size inside it.</summary>
        public const float StarRowDp = 64f;
        public const float StarDp = 52f;
        public const float StarGapDp = 12f;

        /// <summary>
        /// Card and button sizes for a safe area of <paramref name="safeSizeDp"/> with <paramref name="buttonCount"/>
        /// buttons and a title of <paramref name="titleLines"/> lines, with a star row of <paramref name="starRowDp"/>
        /// (0 for none) under the title.
        /// </summary>
        public static ResultPanelMetrics Compute(Vector2 safeSizeDp, int buttonCount, int titleLines = 1, float starRowDp = 0f)
        {
            if (buttonCount < 1) buttonCount = 1;
            if (titleLines < 1) titleLines = 1;
            float extraLines = (titleLines - 1) * TitleLineDp;

            float maxWidth = Mathf.Max(0f, safeSizeDp.x - 2f * MarginDp);
            float maxHeight = Mathf.Max(0f, safeSizeDp.y - 2f * MarginDp);
            float cardWidth = Mathf.Min(MaxCardWidthDp, maxWidth);

            float buttonHeight = ButtonHeightDp;
            float title = TitleHeightDp + extraLines;
            float stars = Mathf.Max(0f, starRowDp);
            float height = Height(title, buttonHeight, buttonCount) + stars;

            // Very short safe areas (landscape, split screen): give up title room first, then button room down to
            // the touch floor. Below that the card may overflow; portrait phones never get there.
            if (height > maxHeight)
            {
                title = Mathf.Max(MinTitleHeightDp + extraLines, title - (height - maxHeight));
                height = Height(title, buttonHeight, buttonCount) + stars;
            }
            if (height > maxHeight)
            {
                float perButton = (height - maxHeight) / buttonCount;
                buttonHeight = Mathf.Max(MinTouchDp, buttonHeight - perButton);
                height = Height(title, buttonHeight, buttonCount) + stars;
            }

            float buttonWidth = Mathf.Max(MinTouchDp, cardWidth - 2f * PaddingDp);
            return new ResultPanelMetrics(new Vector2(cardWidth, height), title, new Vector2(buttonWidth, buttonHeight),
                ButtonGapDp, PaddingDp, stars);
        }

        private static float Height(float title, float button, int count)
        {
            return PaddingDp + title + ButtonGapDp + count * button + (count - 1) * ButtonGapDp + PaddingDp;
        }

        /// <summary>
        /// The safe area as anchors (0..1) of a screen <paramref name="screenWidth"/> x <paramref name="screenHeight"/>
        /// pixels, clamped to the screen.
        /// </summary>
        public static void SafeAreaAnchors(Rect safeAreaPx, float screenWidth, float screenHeight, out Vector2 min, out Vector2 max)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
            {
                min = Vector2.zero;
                max = Vector2.one;
                return;
            }
            min = new Vector2(Mathf.Clamp01(safeAreaPx.xMin / screenWidth), Mathf.Clamp01(safeAreaPx.yMin / screenHeight));
            max = new Vector2(Mathf.Clamp01(safeAreaPx.xMax / screenWidth), Mathf.Clamp01(safeAreaPx.yMax / screenHeight));
        }
    }
}
