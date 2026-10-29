using System;
using System.Collections.Generic;

namespace Game.Services
{
    /// <summary>
    /// The saved player progress, exactly as written to disk (Unity <c>JsonUtility</c> field names). Levels are keyed by
    /// level id (the level file's name, e.g. <c>w1_l001</c>), never by list position, so adding or reordering levels
    /// keeps what the player has done. No coins: the clear counter is display only until boosters (Muthu, #21).
    /// </summary>
    [Serializable]
    public sealed class ProgressData
    {
        /// <summary>Save format version; see <see cref="ProgressSerializer.CurrentVersion"/>.</summary>
        public int version = ProgressSerializer.CurrentVersion;

        /// <summary>Id of the furthest level the player may play; empty before the first win.</summary>
        public string highestUnlocked = string.Empty;

        /// <summary>One entry per level won at least once, with its best star count.</summary>
        public List<LevelStars> levels = new List<LevelStars>();

        public ProgressData Clone()
        {
            var copy = new ProgressData { version = version, highestUnlocked = highestUnlocked };
            foreach (var entry in levels) copy.levels.Add(new LevelStars { id = entry.id, stars = entry.stars });
            return copy;
        }
    }

    /// <summary>Best stars for one level (1..<see cref="ProgressRules.MaxStars"/>).</summary>
    [Serializable]
    public sealed class LevelStars
    {
        public string id;
        public int stars;
    }
}
