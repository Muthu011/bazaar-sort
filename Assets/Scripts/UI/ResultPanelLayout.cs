using UnityEngine;

namespace Game.UI
{
    /// <summary>Sizes of a result panel card, in dp (the panel canvas works in dp: 1 canvas unit = 1 dp).</summary>
    public readonly struct ResultPanelMetrics
    {
        public ResultPanelMetrics(Vector2 card, float titleHeight, Vector2 button, float buttonGap, float padding,
            float starRow = 0f, float leadButtonHeight = 0f)
        {
            Card = card;
            TitleHeight = titleHeight;
            Button = button;
            LeadButton = new Vector2(button.x, leadButtonHeight > 0f ? leadButtonHeight : button.y);
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

        /// <summary>Size of every button after the first; buttons are stacked under the title, centred.</summary>
        public Vector2 Button { get; }

        /// <summary>
        /// Size of button 0. Taller than <see cref="Button"/> when the card leads with a main button (the Fail card's
        /// rewarded Continue); otherwise the same.
        /// </summary>
        public Vector2 LeadButton { get; }

        /// <summary>Size of button <paramref name="index"/>: <see cref="LeadButton"/> for 0, <see cref="Button"/> after.</summary>
        public Vector2 ButtonSize(int index) => index == 0 ? LeadButton : Button;

        public float ButtonGap { get; }
        public float Padding { get; }

        /// <summary>
        /// Centre of button <paramref name="index"/> relative to the card centre (y up). Button 0 sits just under the
        /// title; the rest follow downwards.
        /// </summary>
        public Vector2 ButtonCentre(int index)
        {
            float top = Card.y * 0.5f - Padding - TitleHeight - StarRow - ButtonGap;
            if (index == 0) return new Vector2(0f, top - LeadButton.y * 0.5f);
            float y = top - LeadButton.y - ButtonGap - Button.y * 0.5f - (index - 1) * (Button.y + ButtonGap);
            return new Vector2(0f, y);
        }

        /// <summary>Centre of the title relative to the card centre.</summary>
        public Vector2 TitleCentre => new Vector2(0f, Card.y * 0.5f - Padding - TitleHeight * 0.5f);
    }

    /// <summary>
    /// Layout maths for the Win, Fail and More-levels-soon panels, pure so it is Edit Mode tested: a card centred in the safe area,
    /// never wider than <see cref="MaxCardWidthDp"/>, kept <see cref="MarginDp"/> clear of the safe-area edges, with
    /// buttons at least <see cref="MinTouchDp"/> tall (style guide: 48 dp tap targets).
    /// </summary>
    public static class ResultPanelLayout
    {
        /// <summary>Style guide floor for a tap target.</summary>
        public const float MinTouchDp = 48f;

        /// <summary>Preferred button height.</summary>
        public const float ButtonHeightDp = 56f;

        /// <summary>Preferred height of a lead (main) button above smaller ones: the Fail card's rewarded Continue.</summary>
        public const float LeadButtonHeightDp = 72f;

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
        /// The Win card's coin line under the stars (issue #44, PR #63): "+N" and a coin, in this much height. Only there
        /// when the win earned coins; without it the card is as before.
        /// </summary>
        public const float CoinLineDp = 40f;
        public const int CoinLineFontDp = 28;
        public const float CoinLineIconDp = 30f;
        public const float CoinLineGapDp = 6f;

        /// <summary>Height of the Win card's band under the title: the star row, plus the coin line when shown.</summary>
        public static float WinRowDp(bool coinLine) => StarRowDp + (coinLine ? CoinLineDp : 0f);

        /// <summary>Centre of the Win card's stars (the top <see cref="StarRowDp"/> of the band), relative to the card centre.</summary>
        public static Vector2 WinStarsCentre(ResultPanelMetrics m) =>
            new Vector2(0f, m.StarRowCentre.y + m.StarRow * 0.5f - StarRowDp * 0.5f);

        /// <summary>Centre of the coin line (the bottom <see cref="CoinLineDp"/> of the band), relative to the card centre.</summary>
        public static Vector2 WinCoinLineCentre(ResultPanelMetrics m) =>
            new Vector2(0f, m.StarRowCentre.y - m.StarRow * 0.5f + CoinLineDp * 0.5f);

        /// <summary>
        /// Card and button sizes for a safe area of <paramref name="safeSizeDp"/> with <paramref name="buttonCount"/>
        /// buttons and a title of <paramref name="titleLines"/> lines, with a star row of <paramref name="starRowDp"/>
        /// (0 for none) under the title. With <paramref name="leadButton"/> (and 2 or more buttons) button 0 is the
        /// taller main button (<see cref="LeadButtonHeightDp"/>).
        /// </summary>
        public static ResultPanelMetrics Compute(Vector2 safeSizeDp, int buttonCount, int titleLines = 1, float starRowDp = 0f,
            bool leadButton = false)
        {
            if (buttonCount < 1) buttonCount = 1;
            if (titleLines < 1) titleLines = 1;
            float extraLines = (titleLines - 1) * TitleLineDp;

            float maxWidth = Mathf.Max(0f, safeSizeDp.x - 2f * MarginDp);
            float maxHeight = Mathf.Max(0f, safeSizeDp.y - 2f * MarginDp);
            float cardWidth = Mathf.Min(MaxCardWidthDp, maxWidth);

            float buttonHeight = ButtonHeightDp;
            float leadHeight = leadButton && buttonCount > 1 ? LeadButtonHeightDp : ButtonHeightDp;
            float title = TitleHeightDp + extraLines;
            float stars = Mathf.Max(0f, starRowDp);
            float height = Height(title, leadHeight, buttonHeight, buttonCount) + stars;

            // Very short safe areas (landscape, split screen): give up title room first, then the lead button's extra
            // height, then button room down to the touch floor. Below that the card may overflow; portrait phones
            // never get there.
            if (height > maxHeight)
            {
                title = Mathf.Max(MinTitleHeightDp + extraLines, title - (height - maxHeight));
                height = Height(title, leadHeight, buttonHeight, buttonCount) + stars;
            }
            if (height > maxHeight && leadHeight > buttonHeight)
            {
                leadHeight = Mathf.Max(buttonHeight, leadHeight - (height - maxHeight));
                height = Height(title, leadHeight, buttonHeight, buttonCount) + stars;
            }
            if (height > maxHeight)
            {
                float perButton = (height - maxHeight) / buttonCount;
                buttonHeight = Mathf.Max(MinTouchDp, buttonHeight - perButton);
                leadHeight = Mathf.Max(buttonHeight, leadHeight - perButton);
                height = Height(title, leadHeight, buttonHeight, buttonCount) + stars;
            }

            float buttonWidth = Mathf.Max(MinTouchDp, cardWidth - 2f * PaddingDp);
            return new ResultPanelMetrics(new Vector2(cardWidth, height), title, new Vector2(buttonWidth, buttonHeight),
                ButtonGapDp, PaddingDp, stars, leadHeight);
        }

        private static float Height(float title, float lead, float button, int count)
        {
            return PaddingDp + title + ButtonGapDp + lead + (count - 1) * (button + ButtonGapDp) + PaddingDp;
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
