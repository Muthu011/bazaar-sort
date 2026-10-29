using System;

namespace Game.Services
{
    /// <summary>
    /// The player's saved progress: best stars per level and the furthest unlocked level, by level id,
    /// the tutorials already completed, and the coin wallet (issue #44). Knows nothing
    /// about level order; <see cref="ProgressRules"/> turns it into "continue here" and "what does this win unlock".
    /// </summary>
    public interface IProgressService
    {
        /// <summary>Id of the furthest unlocked level, or empty before the first win.</summary>
        string HighestUnlocked { get; }

        /// <summary>Best stars for <paramref name="levelId"/>, 0 when never won.</summary>
        int GetStars(string levelId);

        /// <summary>Coins in the wallet (0 or more).</summary>
        int Coins { get; }

        /// <summary>
        /// Records a win with <paramref name="stars"/> (the best count is kept, so stars never go down), sets
        /// <paramref name="highestUnlocked"/> and adds <paramref name="coinsEarned"/> to the wallet, then saves once.
        /// </summary>
        void RecordWin(string levelId, int stars, string highestUnlocked, int coinsEarned);

        /// <summary>
        /// Pays <paramref name="price"/> coins from the wallet (boosters, #56) and saves. With too few coins nothing
        /// changes, nothing is saved and it returns false. A price of 0 succeeds without a save.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A negative price.</exception>
        bool TrySpendCoins(int price);

        /// <summary>
        /// True once the tutorial with key <paramref name="tutorialKey"/> (the level JSON <c>tutorial</c> value, e.g.
        /// <c>match3</c>) was completed; it is then skipped (issue #57).
        /// </summary>
        bool IsTutorialDone(string tutorialKey);

        /// <summary>Records that tutorial <paramref name="tutorialKey"/> was completed, then saves at once. Repeats do nothing.</summary>
        void MarkTutorialDone(string tutorialKey);

        /// <summary>Forgets everything and saves the empty progress (dev level select only).</summary>
        void Reset();

        /// <summary>Raised after progress changes.</summary>
        event Action Changed;
    }
}
