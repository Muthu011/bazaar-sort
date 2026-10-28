using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// The triple-clear pop and the layer-reveal slide, as pure functions of time (like <see cref="TileMotion"/>), so
    /// the timing and easing are tested in Edit Mode and <see cref="BoardView"/> only samples them. No allocation.
    ///
    /// Clear: the three tiles pop up to <see cref="ClearPeakScale"/> over <see cref="ClearPopSeconds"/>, squashing wide
    /// then stretching tall on the way (<see cref="ClearSquashAspect"/>, issue #21), then shrink to nothing while
    /// fading out over <see cref="ClearFadeSeconds"/>. Reveal: the next layer's tiles slide from their small dimmed
    /// spot in the hidden strip to the front slots, growing to full size and brightness, overshooting the front slot
    /// by a small bounce (<see cref="RevealOvershoot"/>, issue #21) before settling exactly on it.
    /// </summary>
    public static class ClearRevealMotion
    {
        /// <summary>Seconds of the pop up.</summary>
        public const float ClearPopSeconds = 0.1f;

        /// <summary>Seconds of the shrink and fade after the pop.</summary>
        public const float ClearFadeSeconds = 0.25f;

        /// <summary>Whole clear: well under the 0.5 s in issue #6 (style guide: clear effects under 0.6 s).</summary>
        public const float ClearSeconds = ClearPopSeconds + ClearFadeSeconds;

        /// <summary>Largest scale during the pop, relative to the resting tile.</summary>
        public const float ClearPeakScale = 1.2f;

        /// <summary>Seconds a revealed layer takes to slide forward.</summary>
        public const float RevealSeconds = 0.22f;

        /// <summary>Largest squash during the pop: x = s(1 + a), y = s(1 - a).</summary>
        public const float ClearSquash = 0.15f;

        /// <summary>
        /// Back-ease constant of the reveal slide. 1.2 overshoots the front slot by about 5 % of the slide distance
        /// (<see cref="RevealPeakProgress"/>), a small bounce.
        /// </summary>
        public const float RevealOvershoot = 1.2f;

        /// <summary>Highest reveal progress (the bounce), reached at u = 1 - 2c / (3(c + 1)).</summary>
        public static float RevealPeakProgress
        {
            get
            {
                float v = -2f * RevealOvershoot / (3f * (RevealOvershoot + 1f));
                return BackOut(1f + v);
            }
        }

        /// <summary>Tile scale <paramref name="elapsed"/> seconds into a clear: 1, up to the peak, then down to 0.</summary>
        public static float ClearScale(float elapsed)
        {
            if (elapsed <= 0f) return 1f;
            if (elapsed < ClearPopSeconds)
            {
                float u = elapsed / ClearPopSeconds;
                return 1f + (ClearPeakScale - 1f) * EaseOut(u);
            }
            if (elapsed < ClearSeconds)
            {
                float u = (elapsed - ClearPopSeconds) / ClearFadeSeconds;
                return ClearPeakScale * (1f - EaseIn(u));
            }
            return 0f;
        }

        /// <summary>Tile opacity <paramref name="elapsed"/> seconds into a clear: 1 through the pop, then fades to 0.</summary>
        public static float ClearAlpha(float elapsed)
        {
            if (elapsed <= ClearPopSeconds) return 1f;
            if (elapsed < ClearSeconds)
            {
                float u = (elapsed - ClearPopSeconds) / ClearFadeSeconds;
                return 1f - u;
            }
            return 0f;
        }

        /// <summary>
        /// Squash aspect <paramref name="elapsed"/> seconds into a clear: during the pop it swings wide (positive) then
        /// tall (negative) and is back to 0 at the peak; 0 before and after. Apply as x = s(1 + a), y = s(1 - a).
        /// </summary>
        public static float ClearSquashAspect(float elapsed)
        {
            if (elapsed <= 0f || elapsed >= ClearPopSeconds) return 0f;
            float u = elapsed / ClearPopSeconds;
            return ClearSquash * Mathf.Sin(2f * Mathf.PI * u) * (1f - 0.5f * u);
        }

        /// <summary>
        /// Reveal progress <paramref name="elapsed"/> seconds into the slide: 0 to a little over 1 (the bounce, at most
        /// <see cref="RevealPeakProgress"/>), then exactly 1 from <see cref="RevealSeconds"/> on.
        /// </summary>
        public static float RevealProgress(float elapsed)
        {
            if (elapsed <= 0f) return 0f;
            if (elapsed >= RevealSeconds) return 1f;
            return BackOut(elapsed / RevealSeconds);
        }

        /// <summary>
        /// Centre of a revealed tile: starts at <paramref name="hidden"/> (its spot in the hidden strip) and ends
        /// exactly at <paramref name="front"/> (its front slot).
        /// </summary>
        public static Vector2 RevealPosition(Vector2 hidden, Vector2 front, float elapsed)
        {
            if (elapsed >= RevealSeconds) return front;
            return Vector2.LerpUnclamped(hidden, front, RevealProgress(elapsed));
        }

        /// <summary>Scale of a revealed tile: <paramref name="hiddenScale"/> (hidden size / front size) up to exactly 1.</summary>
        public static float RevealScale(float hiddenScale, float elapsed)
        {
            if (elapsed >= RevealSeconds) return 1f;
            return hiddenScale + (1f - hiddenScale) * RevealProgress(elapsed);
        }

        /// <summary>Brightness of a revealed tile: <paramref name="hiddenBrightness"/> up to exactly 1.</summary>
        public static float RevealBrightness(float hiddenBrightness, float elapsed)
        {
            float p = Mathf.Min(1f, RevealProgress(elapsed));
            return p >= 1f ? 1f : hiddenBrightness + (1f - hiddenBrightness) * p;
        }

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);

        private static float EaseIn(float u) => u * u;

        private static float BackOut(float u)
        {
            float v = u - 1f;
            return 1f + (RevealOvershoot + 1f) * v * v * v + RevealOvershoot * v * v;
        }
    }
}
