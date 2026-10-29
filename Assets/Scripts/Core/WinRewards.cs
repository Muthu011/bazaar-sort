using System;

namespace Game.Core
{
    /// <summary>
    /// What a win earns (issue #44, docs/game-design.md "Stars and coins"). Pure rules, Edit Mode tested.
    /// <list type="bullet">
    /// <item>Stars, by the moves the player made against the solver's minimum (<c>solver.minMoves</c> in the level
    /// file): 3 stars at or under minMoves x 1.5, 2 stars at or under minMoves x 2.5, otherwise 1. The thresholds are
    /// compared as real numbers, never rounded: with minMoves 5, 7 moves is 3 stars (7 &lt;= 7.5) and 8 is 2 stars.
    /// The comparison is done in whole numbers (2 x moves against 3 x minMoves and 5 x minMoves), so it is exact.</item>
    /// <item>A level with no known minimum (minMoves missing, 0 or negative: a level the solver has not stamped, e.g.
    /// a test board) gives <see cref="MaxStars"/>: the player is never marked down against a number that doesn't
    /// exist. Shipped levels always have one (LevelPackTests).</item>
    /// <item>Coins (Muthu, PR #63; amounts from <see cref="RewardConfig"/>): the first win of a level pays
    /// <see cref="RewardConfig.FirstWinCoins"/> plus <see cref="RewardConfig.CoinsPerExtraStar"/> per star above 1, so
    /// 10, 15 or 20. A replay (the level was won before) pays <see cref="RewardConfig.CoinsPerExtraStar"/> per star above
    /// the previous best, otherwise 0. So a level never pays more in total than a 3-star first win: 1 star (10) then a
    /// 3-star replay (+10) is 20.</item>
    /// <item>Spending: a price can be paid only when the balance covers it; the balance never goes below 0.</item>
    /// </list>
    /// </summary>
    public static class WinRewards
    {
        public const int MaxStars = 3;
        public const int MinStars = 1;

        /// <summary>3 stars at or under minMoves x 1.5 (written as 3/2 so the check stays in whole numbers).</summary>
        public const int ThreeStarNumerator = 3;

        /// <summary>2 stars at or under minMoves x 2.5 (5/2).</summary>
        public const int TwoStarNumerator = 5;

        /// <summary>Shared denominator of the two star factors.</summary>
        public const int StarDenominator = 2;

        /// <summary>Stars for a win in <paramref name="moves"/> moves on a level whose shortest win is <paramref name="minMoves"/>.</summary>
        public static int StarsFor(int moves, int minMoves)
        {
            if (minMoves <= 0) return MaxStars;
            long scaledMoves = (long)Math.Max(0, moves) * StarDenominator;
            if (scaledMoves <= (long)minMoves * ThreeStarNumerator) return 3;
            if (scaledMoves <= (long)minMoves * TwoStarNumerator) return 2;
            return 1;
        }

        /// <summary>
        /// Coins a win with <paramref name="stars"/> stars earns on a level whose best stars before this win were
        /// <paramref name="previousBestStars"/> (0 = never won: a first win). Stars are clamped to 1..3, the previous best
        /// to 0..3. <paramref name="config"/> null means <see cref="RewardConfig.Default"/>.
        /// </summary>
        public static int CoinsForWin(int stars, int previousBestStars, RewardConfig config = null)
        {
            config ??= RewardConfig.Default;
            int clamped = Math.Min(MaxStars, Math.Max(MinStars, stars));
            int previous = Math.Min(MaxStars, Math.Max(0, previousBestStars));
            if (previous == 0) return config.FirstWinCoins + config.CoinsPerExtraStar * (clamped - MinStars);
            return config.CoinsPerExtraStar * Math.Max(0, clamped - previous);
        }

        /// <summary>True when <paramref name="balance"/> covers <paramref name="price"/> (a price of 0 always does).</summary>
        public static bool CanAfford(int balance, int price)
        {
            if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "A price can't be negative.");
            return balance >= price;
        }

        /// <summary><paramref name="balance"/> plus <paramref name="earned"/>, held at <see cref="int.MaxValue"/> instead of overflowing.</summary>
        public static int AddCoins(int balance, int earned)
        {
            if (earned < 0) throw new ArgumentOutOfRangeException(nameof(earned), "Earned coins can't be negative.");
            long sum = (long)Math.Max(0, balance) + earned;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }
    }
}
