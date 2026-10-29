using System;

namespace Game.Services
{
    /// <summary>
    /// The player's saved progress: best stars per level and the furthest unlocked level, by level id,
    /// and the tutorials already completed. Knows nothing
    /// about level order; <see cref="ProgressRules"/> turns it into "continue here" and "what does this win unlock".
    /// </summary>
    public interface IProgressService
    {
        /// <summary>Id of the furthest unlocked level, or empty before the first win.</summary>
        string HighestUnlocked { get; }

        /// <summary>Best stars for <paramref name="levelId"/>, 0 when never won.</summary>
        int GetStars(string levelId);

        /// <summary>
        /// Records a win with <paramref name="stars"/> (the best count is kept) and sets <paramref name="highestUnlocked"/>,
        /// then saves at once.
        /// </summary>
        void RecordWin(string levelId, int stars, string highestUnlocked);

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
