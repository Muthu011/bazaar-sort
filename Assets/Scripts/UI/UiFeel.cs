using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Timings and sizes of the UI feel (issues #21, #44): button press, Win stars, and the Win card's coins flying into
    /// the HUD pill. Constants and pure maths only, so the values are reviewed in one place and Edit Mode tested; the
    /// tweens are DOTween, built once and restarted (no allocation per frame).
    /// </summary>
    public static class UiFeel
    {
        /// <summary>Scale a button presses down to.</summary>
        public const float PressedScale = 0.92f;

        /// <summary>Seconds to press down (and, played backwards, to come back up).</summary>
        public const float PressSeconds = 0.07f;

        /// <summary>Seconds a coin takes from the Win card's "+N" line to the HUD pill.</summary>
        public const float CoinSeconds = 0.5f;

        /// <summary>How high the coin's path bows above the straight line, in dp.</summary>
        public const float CoinArcDp = 120f;

        /// <summary>Coin size, in dp.</summary>
        public const float CoinDp = 36f;

        /// <summary>Pooled coins: the most that fly for one win.</summary>
        public const int CoinPool = 6;

        /// <summary>One flying coin per this many coins earned (at least one, at most <see cref="CoinPool"/>): 10 -> 2, 20 -> 4.</summary>
        public const int CoinsPerFlyingCoin = 5;

        /// <summary>Gap between one Win coin setting off and the next.</summary>
        public const float CoinStaggerSeconds = 0.08f;

        /// <summary>Star slots on the Win panel, one per possible star; the earned ones are lit (issue #44).</summary>
        public const int WinStars = 3;

        /// <summary>Gap between one star starting to drop and the next.</summary>
        public const float StarStaggerSeconds = 0.18f;

        /// <summary>Seconds a star takes to drop into place.</summary>
        public const float StarDropSeconds = 0.22f;

        /// <summary>How far above its place a star starts, in dp.</summary>
        public const float StarDropDp = 90f;

        /// <summary>Scale punch when a star lands.</summary>
        public const float StarPunch = 0.35f;
        public const float StarPunchSeconds = 0.25f;

        /// <summary>When the last star has landed and punched: confetti starts here.</summary>
        public const float StarsSeconds = (WinStars - 1) * StarStaggerSeconds + StarDropSeconds + StarPunchSeconds;

        /// <summary>
        /// Coin position at flight progress <paramref name="p"/> (0..1, already eased): a curve from
        /// <paramref name="from"/> to <paramref name="to"/> bowing <see cref="CoinArcDp"/> upwards. Exactly the ends at 0
        /// and 1.
        /// </summary>
        public static Vector2 CoinPosition(Vector2 from, Vector2 to, float p)
        {
            if (p <= 0f) return from;
            if (p >= 1f) return to;
            var control = (from + to) * 0.5f + new Vector2(0f, CoinArcDp);
            float q = 1f - p;
            return q * q * from + 2f * q * p * control + p * p * to;
        }

        /// <summary>Coin scale at progress <paramref name="p"/>: grows to 1.3 mid-flight and lands at 0.8 (into the icon).</summary>
        public static float CoinScale(float p)
        {
            p = Mathf.Clamp01(p);
            return Mathf.Lerp(1f, 0.8f, p) + 0.3f * Mathf.Sin(Mathf.PI * p);
        }

        /// <summary>Coins that fly for a win of <paramref name="coins"/> coins; 0 for none.</summary>
        public static int CoinsFlownFor(int coins)
        {
            if (coins <= 0) return 0;
            int flown = (coins + CoinsPerFlyingCoin - 1) / CoinsPerFlyingCoin;
            return Mathf.Clamp(flown, 1, CoinPool);
        }

        /// <summary>
        /// Coins counted on the pill once <paramref name="landed"/> of <paramref name="flown"/> coins have landed, out of
        /// <paramref name="coins"/>: the count climbs in near-equal steps and the last landing always reaches the total.
        /// </summary>
        public static int CoinsLanded(int coins, int flown, int landed)
        {
            if (coins <= 0 || flown <= 0 || landed <= 0) return 0;
            if (landed >= flown) return coins;
            return (int)((long)coins * landed / flown);
        }

        /// <summary>Seconds from the first Win coin setting off to the last one landing.</summary>
        public static float CoinsSecondsFor(int flown) => flown <= 0 ? 0f : (flown - 1) * CoinStaggerSeconds + CoinSeconds;

        /// <summary>When star <paramref name="index"/> starts to drop.</summary>
        public static float StarStart(int index) => index * StarStaggerSeconds;
    }
}
