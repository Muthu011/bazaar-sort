using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// The triple-clear pop and the layer-reveal slide, as pure functions of time (like <see cref="TileMotion"/>), so
    /// the timing and easing are tested in Edit Mode and <see cref="BoardView"/> only samples them. No allocation.
    ///
    /// Clear: the three tiles pop up to <see cref="ClearPeakScale"/> over <see cref="ClearPopSeconds"/>, then shrink to
    /// nothing while fading out over <see cref="ClearFadeSeconds"/>. Reveal: the next layer's tiles slide from their
    /// small dimmed spot in the hidden strip to the front slots, growing to full size and brightness.
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

        /// <summary>Reveal progress 0..1 (eased out) <paramref name="elapsed"/> seconds into the slide.</summary>
        public static float RevealProgress(float elapsed)
        {
            if (elapsed <= 0f) return 0f;
            if (elapsed >= RevealSeconds) return 1f;
            return EaseOut(elapsed / RevealSeconds);
        }

        /// <summary>
        /// Centre of a revealed tile: starts at <paramref name="hidden"/> (its spot in the hidden strip) and ends
        /// exactly at <paramref name="front"/> (its front slot).
        /// </summary>
        public static Vector2 RevealPosition(Vector2 hidden, Vector2 front, float elapsed)
        {
            float p = RevealProgress(elapsed);
            return p >= 1f ? front : Vector2.LerpUnclamped(hidden, front, p);
        }

        /// <summary>Scale of a revealed tile: <paramref name="hiddenScale"/> (hidden size / front size) up to exactly 1.</summary>
        public static float RevealScale(float hiddenScale, float elapsed)
        {
            float p = RevealProgress(elapsed);
            return p >= 1f ? 1f : hiddenScale + (1f - hiddenScale) * p;
        }

        /// <summary>Brightness of a revealed tile: <paramref name="hiddenBrightness"/> up to exactly 1.</summary>
        public static float RevealBrightness(float hiddenBrightness, float elapsed)
        {
            float p = RevealProgress(elapsed);
            return p >= 1f ? 1f : hiddenBrightness + (1f - hiddenBrightness) * p;
        }

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);

        private static float EaseIn(float u) => u * u;
    }
}
