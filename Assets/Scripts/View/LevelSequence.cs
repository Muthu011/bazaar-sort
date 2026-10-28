using System;

namespace Game.View
{
    /// <summary>Order of play through a list of levels. Pure maths, Edit Mode tested.</summary>
    public static class LevelSequence
    {
        /// <summary>
        /// The level after <paramref name="current"/> in a list of <paramref name="count"/>. After the last level it
        /// loops back to the first (placeholder until world progression exists).
        /// </summary>
        public static int Next(int current, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (current < 0 || current >= count) return 0;
            return (current + 1) % count;
        }
    }
}
