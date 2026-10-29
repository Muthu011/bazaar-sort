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

        /// <summary>Coin (clear) counter pill, left of the settings button.</summary>
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
        public const float CounterWidthDp = 84f;
        public const float CounterHeightDp = 40f;
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

        public static HudMetrics Compute(Vector2 safeSizeDp, float topReserveFraction, float bottomReserveFraction)
        {
            float w = safeSizeDp.x;
            float h = safeSizeDp.y;
            float top = Mathf.Max(0f, h * topReserveFraction);
            float bottom = Mathf.Max(0f, h * bottomReserveFraction);

            // Bottom bar, right to left: settings, counter, then the booster slots fill what is left from the left.
            var settings = new Rect(w - SideMarginDp - SettingsDp, 0f, SettingsDp, SettingsDp);
            var counter = new Rect(settings.xMin - GapDp - CounterWidthDp, 0f, CounterWidthDp, CounterHeightDp);
            float room = counter.xMin - GapDp - SideMarginDp;
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
