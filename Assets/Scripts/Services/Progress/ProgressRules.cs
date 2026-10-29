using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Services
{
    /// <summary>Where the player continues: a level index, or "every level is cleared".</summary>
    public readonly struct ContinuePoint
    {
        public ContinuePoint(int index, bool allCleared)
        {
            Index = index;
            AllCleared = allCleared;
        }

        /// <summary>Level to load: the first level never won, or the last level when <see cref="AllCleared"/>.</summary>
        public int Index { get; }

        /// <summary>
        /// Every available level is won: show "More levels coming soon" with Replay (docs/game-design.md: after the
        /// last available level; Next never loops).
        /// </summary>
        public bool AllCleared { get; }
    }

    /// <summary>
    /// Progress rules over an ordered list of level ids (play order). Pure, Edit Mode tested.
    /// <list type="bullet">
    /// <item>Continue: the first level in play order that was never won. When all are won, the last level with
    /// "More levels coming soon". A level added later (e.g. w1_l015) is unplayed, so the player continues there.</item>
    /// <item>A win unlocks the next level in order; the furthest unlock never goes backwards (replaying L1 keeps L8
    /// unlocked). Winning the last level keeps it as the furthest unlock.</item>
    /// <item>Stars and coins for a win come from <see cref="WinRewards"/> (issue #44); the best star count per level is
    /// kept. A first win pays the full amount; a replay pays only for stars above the previous best (Muthu, PR #63).</item>
    /// </list>
    /// </summary>
    public static class ProgressRules
    {
        public const int MaxStars = WinRewards.MaxStars;

        public static ContinuePoint Continue(IReadOnlyList<string> levelIds, IProgressService progress)
        {
            if (levelIds == null) throw new ArgumentNullException(nameof(levelIds));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (levelIds.Count == 0) throw new ArgumentException("No levels.", nameof(levelIds));
            for (int i = 0; i < levelIds.Count; i++)
            {
                if (progress.GetStars(levelIds[i]) == 0) return new ContinuePoint(i, false);
            }
            return new ContinuePoint(levelIds.Count - 1, true);
        }

        /// <summary>
        /// Index of the furthest unlocked level in <paramref name="levelIds"/>: 0 before any win or when the saved id is
        /// no longer in the list.
        /// </summary>
        public static int HighestUnlockedIndex(IReadOnlyList<string> levelIds, IProgressService progress)
        {
            if (levelIds == null) throw new ArgumentNullException(nameof(levelIds));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            string id = progress.HighestUnlocked;
            if (string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < levelIds.Count; i++)
            {
                if (string.Equals(levelIds[i], id, StringComparison.Ordinal)) return i;
            }
            return 0;
        }

        /// <summary>True when level <paramref name="index"/> is unlocked (level 1 always is).</summary>
        public static bool IsUnlocked(IReadOnlyList<string> levelIds, IProgressService progress, int index)
        {
            return index >= 0 && index < levelIds.Count && index <= HighestUnlockedIndex(levelIds, progress);
        }

        /// <summary>
        /// Coins a win of level <paramref name="index"/> with <paramref name="stars"/> stars would earn now, against the
        /// level's saved best stars (<see cref="WinRewards.CoinsForWin"/>). Reads only; <see cref="RecordWin"/> pays it.
        /// </summary>
        public static int CoinsForWin(IReadOnlyList<string> levelIds, IProgressService progress, int index, int stars,
            RewardConfig config = null)
        {
            if (levelIds == null) throw new ArgumentNullException(nameof(levelIds));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (index < 0 || index >= levelIds.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return WinRewards.CoinsForWin(stars, progress.GetStars(levelIds[index]), config);
        }

        /// <summary>
        /// Records a win of level <paramref name="index"/> with <paramref name="stars"/> stars (see
        /// <see cref="WinRewards.StarsFor"/>), adds its coins (<see cref="CoinsForWin"/>: full on a first win, only the
        /// stars above the old best on a replay) and unlocks the level after it, all in one save. Returns the coins added.
        /// <paramref name="config"/> null means <see cref="RewardConfig.Default"/>.
        /// </summary>
        public static int RecordWin(IReadOnlyList<string> levelIds, IProgressService progress, int index, int stars,
            RewardConfig config = null)
        {
            int coins = CoinsForWin(levelIds, progress, index, stars, config);
            int unlocked = Math.Max(HighestUnlockedIndex(levelIds, progress), Math.Min(index + 1, levelIds.Count - 1));
            stars = Math.Min(MaxStars, Math.Max(WinRewards.MinStars, stars));
            progress.RecordWin(levelIds[index], stars, levelIds[unlocked], coins);
            return coins;
        }
    }
}
