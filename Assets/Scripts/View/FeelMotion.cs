using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Game-feel motion for board tiles (issue #21), as pure functions of time like <see cref="TileMotion"/> and
    /// <see cref="ClearRevealMotion"/>, so timings and easing are Edit Mode tested and <see cref="BoardView"/> only
    /// samples them on its own clock (<see cref="BoardView.AdvanceAnimations"/>). No allocation.
    ///
    /// Pick-up: the tile squashes to <see cref="PickUpSquash"/> (1.1 wide, 0.9 tall), springs once the other way and
    /// settles at rest. Move: the tile flies to its new slot on an arc in <see cref="ArcSeconds"/>, eased out, growing a
    /// little at the top of the arc.
    /// </summary>
    public static class FeelMotion
    {
        /// <summary>Seconds of the pick-up squash and settle.</summary>
        public const float PickUpSeconds = 0.2f;

        /// <summary>Squash at the moment of pick-up: x = 1 + this, y = 1 - this.</summary>
        public const float PickUpSquash = 0.1f;

        /// <summary>Seconds of the move flight (issue #21: about 0.25 s, ease-out).</summary>
        public const float ArcSeconds = 0.25f;

        /// <summary>Arc height for a flight between neighbouring slots, in tiles.</summary>
        public const float ArcHeightTiles = 0.5f;

        /// <summary>Extra arc height per unit of flight distance, so long flights arc higher.</summary>
        public const float ArcHeightPerDistance = 0.2f;

        /// <summary>Highest arc, in tiles.</summary>
        public const float MaxArcHeightTiles = 1.5f;

        /// <summary>Tile scale at the top of the arc (it comes "towards" the player a little).</summary>
        public const float ArcPeakScale = 1.08f;

        /// <summary>
        /// Pick-up scale (x, y) <paramref name="elapsed"/> seconds after the tile is picked: (1.1, 0.9) at once, one
        /// damped swing to a slight stretch, exactly (1, 1) from <see cref="PickUpSeconds"/> on.
        /// </summary>
        public static Vector2 PickUpScale(float elapsed)
        {
            if (elapsed >= PickUpSeconds) return Vector2.one;
            if (elapsed < 0f) elapsed = 0f;
            float u = elapsed / PickUpSeconds;
            float d = PickUpSquash * Mathf.Cos(2f * Mathf.PI * u) * (1f - u) * (1f - u);
            return new Vector2(1f + d, 1f - d);
        }

        /// <summary>Flight progress 0..1, eased out (fast start, soft landing). Exactly 1 from <see cref="ArcSeconds"/>.</summary>
        public static float ArcProgress(float elapsed)
        {
            if (elapsed <= 0f) return 0f;
            if (elapsed >= ArcSeconds) return 1f;
            float u = elapsed / ArcSeconds;
            return 1f - (1f - u) * (1f - u);
        }

        /// <summary>Arc height in pixels for a flight of <paramref name="distance"/> pixels with tiles of <paramref name="tileSize"/>.</summary>
        public static float ArcHeight(float distance, float tileSize)
        {
            return Mathf.Min(ArcHeightTiles * tileSize + ArcHeightPerDistance * distance, MaxArcHeightTiles * tileSize);
        }

        /// <summary>
        /// Centre of a flying tile: on the straight line from <paramref name="from"/> to <paramref name="to"/> at
        /// <see cref="ArcProgress"/>, lifted by a parabola peaking at <paramref name="height"/> mid-flight. Exactly
        /// <paramref name="to"/> once done.
        /// </summary>
        public static Vector2 ArcPosition(Vector2 from, Vector2 to, float height, float elapsed)
        {
            float p = ArcProgress(elapsed);
            if (p >= 1f) return to;
            var position = Vector2.LerpUnclamped(from, to, p);
            position.y += height * 4f * p * (1f - p);
            return position;
        }

        /// <summary>Where the flight's shadow is: the same line without the lift.</summary>
        public static Vector2 ArcGround(Vector2 from, Vector2 to, float elapsed)
        {
            float p = ArcProgress(elapsed);
            return p >= 1f ? to : Vector2.LerpUnclamped(from, to, p);
        }

        /// <summary>Tile scale during the flight: 1, up to <see cref="ArcPeakScale"/> mid-flight, exactly 1 on landing.</summary>
        public static float ArcScale(float elapsed)
        {
            float p = ArcProgress(elapsed);
            if (p <= 0f || p >= 1f) return 1f;
            return 1f + (ArcPeakScale - 1f) * 4f * p * (1f - p);
        }
    }
}
