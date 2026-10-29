using System;

namespace Game.Services
{
    /// <summary>
    /// The player's saved progress: best stars per level and the furthest unlocked level, by level id. Knows nothing
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

        /// <summary>Forgets everything and saves the empty progress (dev level select only).</summary>
        void Reset();

        /// <summary>Raised after progress changes.</summary>
        event Action Changed;
    }
}
