using System;

namespace Game.View
{
    /// <summary>
    /// Order of play through a list of levels. Pure maths, Edit Mode tested. Next never loops: after the last level
    /// there is no next one (the game shows "More levels coming soon" with Replay, which goes to <see cref="First"/>).
    /// </summary>
    public static class LevelSequence
    {
        /// <summary>Index of the first level; Replay after the last level goes here.</summary>
        public const int First = 0;

        /// <summary>True when a level follows <paramref name="current"/> in a list of <paramref name="count"/>.</summary>
        public static bool HasNext(int current, int count) => TryGetNext(current, count, out _);

        /// <summary>
        /// The level after <paramref name="current"/> in a list of <paramref name="count"/>. False after the last
        /// level (<paramref name="next"/> is then -1). With no current level (out of range) the next one is the first.
        /// </summary>
        public static bool TryGetNext(int current, int count, out int next)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (current < 0 || current >= count)
            {
                next = First;
                return true;
            }
            if (current == count - 1)
            {
                next = -1;
                return false;
            }
            next = current + 1;
            return true;
        }
    }
}
