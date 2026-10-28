using System;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Pixels per dp (Android density-independent pixel): dpi / 160. Used to size touch areas in dp. Pure math apart
    /// from <see cref="Current"/>, so tests pass a density explicitly.
    /// </summary>
    public static class ScreenDensity
    {
        /// <summary>Android's baseline: 1 dp is 1 px at 160 dpi.</summary>
        public const float BaselineDpi = 160f;

        /// <summary>
        /// When the device reports no dpi, assume the screen's short side is this many dp. Most Android phones are
        /// 360 to 412 dp wide, so 360 gives the larger (safer) density.
        /// </summary>
        public const float FallbackShortSideDp = 360f;

        /// <summary>Density for the current screen.</summary>
        public static float Current => FromDpi(Screen.dpi, Screen.width, Screen.height);

        /// <summary>
        /// <paramref name="dpi"/> / 160, or, when dpi is 0 or unknown, the screen's short side over
        /// <see cref="FallbackShortSideDp"/> (never below 1).
        /// </summary>
        public static float FromDpi(float dpi, float screenWidthPx, float screenHeightPx)
        {
            if (dpi > 0f && !float.IsInfinity(dpi)) return dpi / BaselineDpi;
            float shortSide = Math.Min(screenWidthPx, screenHeightPx);
            return Math.Max(1f, shortSide / FallbackShortSideDp);
        }
    }
}
