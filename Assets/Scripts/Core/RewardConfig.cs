using System;

namespace Game.Core
{
    /// <summary>
    /// The coin amounts a win pays (issue #44, docs/game-design.md "Stars and coins"). The one place these numbers live:
    /// <see cref="WinRewards.CoinsForWin"/> reads them, never a literal. The game uses <see cref="Default"/>; once remote
    /// config is wired (Firebase Remote Config, approved in docs/approved-dependencies.md, not yet in the project) a
    /// service builds a <see cref="RewardConfig"/> from it and hands it to <c>LevelBootstrap.Rewards</c>. Immutable, so a
    /// shared instance can't be changed under a running level.
    /// </summary>
    public sealed class RewardConfig
    {
        /// <summary>Default coins for the first win of a level, before any star bonus.</summary>
        public const int DefaultFirstWinCoins = 10;

        /// <summary>Default coins per star above 1 on a first win, and per star above the old best on a replay.</summary>
        public const int DefaultCoinsPerExtraStar = 5;

        /// <summary>The live values: 10 for a first win plus 5 per star above 1 (10 / 15 / 20).</summary>
        public static readonly RewardConfig Default = new RewardConfig(DefaultFirstWinCoins, DefaultCoinsPerExtraStar);

        public RewardConfig(int firstWinCoins, int coinsPerExtraStar)
        {
            if (firstWinCoins < 0) throw new ArgumentOutOfRangeException(nameof(firstWinCoins), "Coins can't be negative.");
            if (coinsPerExtraStar < 0) throw new ArgumentOutOfRangeException(nameof(coinsPerExtraStar), "Coins can't be negative.");
            FirstWinCoins = firstWinCoins;
            CoinsPerExtraStar = coinsPerExtraStar;
        }

        /// <summary>Base coins for the first win of a level.</summary>
        public int FirstWinCoins { get; }

        /// <summary>Coins per star above 1 (first win) or above the previous best (replay).</summary>
        public int CoinsPerExtraStar { get; }
    }
}
