using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// The padlock's count-down tick and its pop-off when the shelf opens, as pure functions of time (like
    /// <see cref="ClearRevealMotion"/>), so <see cref="BoardView"/> only samples them. No allocation.
    ///
    /// Tick: the padlock bumps up to <see cref="TickPeakScale"/> and back to 1 over <see cref="TickSeconds"/> when its
    /// number goes down. Pop: it grows to <see cref="PopEndScale"/> while fading out over <see cref="PopSeconds"/>, then
    /// is hidden.
    /// </summary>
    public static class LockMotion
    {
        /// <summary>Seconds of the count-down bump.</summary>
        public const float TickSeconds = 0.2f;

        /// <summary>Largest scale during the bump.</summary>
        public const float TickPeakScale = 1.25f;

        /// <summary>Seconds of the pop-off when the shelf opens (style guide: effects under 0.6 s).</summary>
        public const float PopSeconds = 0.35f;

        /// <summary>Scale the padlock reaches as it fades out.</summary>
        public const float PopEndScale = 1.6f;

        /// <summary>Padlock scale <paramref name="elapsed"/> seconds into a tick: 1, up to the peak, back to exactly 1.</summary>
        public static float TickScale(float elapsed)
        {
            if (elapsed <= 0f || elapsed >= TickSeconds) return 1f;
            float u = elapsed / TickSeconds;
            return 1f + (TickPeakScale - 1f) * Mathf.Sin(u * Mathf.PI);
        }

        /// <summary>Padlock scale <paramref name="elapsed"/> seconds into the pop: 1 up to <see cref="PopEndScale"/>.</summary>
        public static float PopScale(float elapsed)
        {
            if (elapsed <= 0f) return 1f;
            if (elapsed >= PopSeconds) return PopEndScale;
            float u = elapsed / PopSeconds;
            return 1f + (PopEndScale - 1f) * (1f - (1f - u) * (1f - u));
        }

        /// <summary>Padlock opacity <paramref name="elapsed"/> seconds into the pop: 1 down to 0.</summary>
        public static float PopAlpha(float elapsed)
        {
            if (elapsed <= 0f) return 1f;
            if (elapsed >= PopSeconds) return 0f;
            return 1f - elapsed / PopSeconds;
        }
    }
}
