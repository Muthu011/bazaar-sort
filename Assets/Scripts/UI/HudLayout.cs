using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Where the HUD pieces go (issue #40), in dp. Every rectangle is relative to the bottom-left corner of the safe
    /// area (x right, y up), so the bars stay clear of the notch and the gesture bar.
    /// </summary>
    public readonly struct HudMetrics
    {
        public HudMetrics(Vector2 safeSize, float topReserve, float bottomReserve, Rect levelLabel, Rect counter,
            Rect settings, Rect[] boosters)
        {
            SafeSize = safeSize;
            TopReserve = topReserve;
            BottomReserve = bottomReserve;
            LevelLabel = levelLabel;
            Counter = counter;
            Settings = settings;
            Boosters = boosters;
        }

        public Vector2 SafeSize { get; }

        /// <summary>Height of the top bar: the board's top reserve.</summary>
        public float TopReserve { get; }

        /// <summary>Height of the bottom bar: the board's bottom reserve.</summary>
        public float BottomReserve { get; }

        /// <summary>"Level 3", centred in the upper part of the top bar (the status line sits under it).</summary>
        public Rect LevelLabel { get; }

        /// <summary>Coin pill (the wallet), left of the settings button; as wide as its text needs (84 to 103.5 dp).</summary>
        public Rect Counter { get; }

        /// <summary>Settings button, bottom-right.</summary>
        public Rect Settings { get; }

        /// <summary>Placeholder booster slots, left to right from the bottom-left (boosters arrive in issue #42).</summary>
        public Rect[] Boosters { get; }

        /// <summary>True when every bottom-bar piece sits inside the bottom reserve.</summary>
        public bool BottomBarFits
        {
            get
            {
                if (!Inside(Counter, 0f, BottomReserve) || !Inside(Settings, 0f, BottomReserve)) return false;
                foreach (var b in Boosters) if (!Inside(b, 0f, BottomReserve)) return false;
                return true;
            }
        }

        /// <summary>True when the level label sits inside the top reserve.</summary>
        public bool TopBarFits => Inside(LevelLabel, SafeSize.y - TopReserve, SafeSize.y);

        private bool Inside(Rect r, float yMin, float yMax)
        {
            const float e = 0.001f;
            return r.xMin >= -e && r.xMax <= SafeSize.x + e && r.yMin >= yMin - e && r.yMax <= yMax + e;
        }
    }

    /// <summary>
    /// The portrait HUD (issues #21, #40). The board sits in the middle band of the safe area; the bars use the board's
    /// reserves (<c>BoardLayoutSettings.TopReserveFraction</c> and <c>BottomReserveFraction</c>):
    /// - Top bar: the level number in the centre (upper part; the board's status line uses the lower part, see
    ///   <c>BoardView.StatusCentreInReserve</c>), the dev "Levels" button on the left and the dev fps readout on the
    ///   right (development builds only, <see cref="DevLevelSelectLayout"/>).
    /// - Bottom bar: <see cref="BoosterSlots"/> placeholder booster slots from the left, then the coin counter and the
    ///   settings button on the right.
    /// Buttons are at least the 48 dp touch floor. Pure math, Edit Mode tested.
    /// </summary>
    public static class HudLayout
    {
        public const float MinTouchDp = ResultPanelLayout.MinTouchDp;
        public const float SettingsDp = MinTouchDp;
        /// <summary>The coin pill is never narrower than this (up to 3 digits fit in it).</summary>
        public const float CounterMinWidthDp = 84f;
        public const float CounterHeightDp = 40f;

        /// <summary>Coin icon inside the pill: its left inset and size, in dp.</summary>
        public const float CounterIconLeftDp = 6f;
        public const float CounterIconDp = 28f;

        /// <summary>The number's box inside the pill: from this far from the left edge (icon + 4 dp) to this far from the right.</summary>
        public const float CounterTextLeftDp = 38f;
        public const float CounterTextRightDp = 8f;

        /// <summary>
        /// Room per glyph at <see cref="CounterFontDp"/> in the built-in bold font, in dp: the measured advance (a digit
        /// 11, "+" 12, "K" 15 at density 1) plus 0.5 dp, because text laid out at a phone's density comes out up to ~2%
        /// wider (seen in Play Mode: "1250" 44.8 dp). Any other glyph is taken as wide.
        /// </summary>
        public const float CounterDigitDp = 11.5f;
        public const float CounterPlusDp = 12.5f;
        public const float CounterWideGlyphDp = 15.5f;

        /// <summary>Most digits drawn as themselves (<see cref="CoinBalanceText.MaxShown"/>).</summary>
        public const int CounterMaxDigits = 5;

        /// <summary>The widest the pill gets: five digits. The booster slots are sized against this, so they never move.</summary>
        public const float CounterMaxWidthDp = CounterTextLeftDp + CounterMaxDigits * CounterDigitDp + CounterTextRightDp;
        public const float SideMarginDp = 16f;
        public const float GapDp = 8f;
        public const int CounterFontDp = 20;

        public const int BoosterSlots = 3;
        public const float BoosterMaxDp = 56f;

        public const float LevelLabelWidthDp = 120f;
        public const float LevelLabelHeightDp = 28f;
        public const int LevelFontDp = 20;

        /// <summary>Level label centre, as a fraction of the top reserve measured up from the reserve's bottom edge.</summary>
        public const float LevelLabelCentreInReserve = 0.68f;

        /// <summary>
        /// Pill width for <paramref name="text"/> (a <see cref="CoinBalanceText"/> string) at the full font size: room for
        /// the icon, the glyphs and the right inset, never under <see cref="CounterMinWidthDp"/> nor over
        /// <see cref="CounterMaxWidthDp"/>. The text is never shrunk to fit; it is the pill that grows.
        /// </summary>
        public static float CounterWidthFor(string text)
        {
            float glyphs = 0f;
            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    glyphs += c >= '0' && c <= '9' ? CounterDigitDp : c == '+' ? CounterPlusDp : CounterWideGlyphDp;
                }
            }
            return Mathf.Clamp(CounterTextLeftDp + glyphs + CounterTextRightDp, CounterMinWidthDp, CounterMaxWidthDp);
        }

        /// <summary>
        /// The HUD for a safe area of <paramref name="safeSizeDp"/>, with the coin pill <paramref name="counterWidthDp"/>
        /// wide (clamped to <see cref="CounterMinWidthDp"/>..<see cref="CounterMaxWidthDp"/>; it grows leftwards from
        /// beside the settings button). The booster slots always leave room for the widest pill, so a growing balance
        /// never moves them.
        /// </summary>
        public static HudMetrics Compute(Vector2 safeSizeDp, float topReserveFraction, float bottomReserveFraction,
            float counterWidthDp = CounterMinWidthDp)
        {
            float w = safeSizeDp.x;
            float h = safeSizeDp.y;
            float top = Mathf.Max(0f, h * topReserveFraction);
            float bottom = Mathf.Max(0f, h * bottomReserveFraction);

            // Bottom bar, right to left: settings, counter, then the booster slots fill what is left from the left.
            var settings = new Rect(w - SideMarginDp - SettingsDp, 0f, SettingsDp, SettingsDp);
            float counterWidth = Mathf.Clamp(counterWidthDp, CounterMinWidthDp, CounterMaxWidthDp);
            var counter = new Rect(settings.xMin - GapDp - counterWidth, 0f, counterWidth, CounterHeightDp);
            float room = settings.xMin - GapDp - CounterMaxWidthDp - GapDp - SideMarginDp;
            float booster = Mathf.Clamp((room - (BoosterSlots - 1) * GapDp) / BoosterSlots, MinTouchDp, BoosterMaxDp);

            // One row centred in the reserve; on a reserve shorter than the row, keep the touch floor and sit on the bottom.
            float rowHeight = Mathf.Max(booster, SettingsDp);
            float y = Mathf.Max(bottom * 0.5f, rowHeight * 0.5f);
            settings.y = y - SettingsDp * 0.5f;
            counter.y = y - CounterHeightDp * 0.5f;
            var boosters = new Rect[BoosterSlots];
            for (int i = 0; i < BoosterSlots; i++)
            {
                boosters[i] = new Rect(SideMarginDp + i * (booster + GapDp), y - booster * 0.5f, booster, booster);
            }

            // Top bar: level label centred, in the upper part of the reserve, never above the safe area.
            float labelY = Mathf.Min(h - top + top * LevelLabelCentreInReserve, h - LevelLabelHeightDp * 0.5f);
            var level = new Rect(w * 0.5f - LevelLabelWidthDp * 0.5f, labelY - LevelLabelHeightDp * 0.5f,
                LevelLabelWidthDp, LevelLabelHeightDp);

            return new HudMetrics(safeSizeDp, top, bottom, level, counter, settings, boosters);
        }
    }
}
