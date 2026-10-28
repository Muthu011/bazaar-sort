using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// The snap-back and shake motion of a tile, as pure functions of time, so it is tested in Edit Mode and the view
    /// only samples it (no tween library, no allocation).
    /// </summary>
    public static class TileMotion
    {
        /// <summary>Seconds a released tile takes to glide back to its slot.</summary>
        public const float ReturnSeconds = 0.12f;

        /// <summary>Seconds of the "not allowed" shake.</summary>
        public const float ShakeSeconds = 0.28f;

        /// <summary>Full side-to-side swings in one shake.</summary>
        public const float ShakeCycles = 3f;

        /// <summary>Peak shake offset, in tiles.</summary>
        public const float ShakeAmplitudeTiles = 0.08f;

        /// <summary>Total length of a motion.</summary>
        public static float Duration(bool glide, bool shake)
        {
            return (glide ? ReturnSeconds : 0f) + (shake ? ShakeSeconds : 0f);
        }

        /// <summary>
        /// Position <paramref name="elapsed"/> seconds into a motion that glides from <paramref name="from"/> to
        /// <paramref name="home"/> (when <paramref name="glide"/>) and then shakes sideways around home by up to
        /// <paramref name="amplitude"/> (when <paramref name="shake"/>). Exactly <paramref name="home"/> once done.
        /// </summary>
        public static Vector2 Position(Vector2 from, Vector2 home, float elapsed, bool glide, bool shake, float amplitude)
        {
            if (elapsed < 0f) elapsed = 0f;

            if (glide)
            {
                if (elapsed < ReturnSeconds)
                {
                    float u = elapsed / ReturnSeconds;
                    float eased = 1f - (1f - u) * (1f - u); // ease out
                    return Vector2.LerpUnclamped(from, home, eased);
                }
                elapsed -= ReturnSeconds;
            }

            if (shake && elapsed < ShakeSeconds)
            {
                float u = elapsed / ShakeSeconds;
                float x = Mathf.Sin(2f * Mathf.PI * ShakeCycles * u) * (1f - u) * amplitude;
                return new Vector2(home.x + x, home.y);
            }

            return home;
        }
    }
}
