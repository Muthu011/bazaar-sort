namespace Game.Core
{
    /// <summary>
    /// Core puzzle constants. Pure C#, no UnityEngine dependency.
    /// </summary>
    public static class GameRules
    {
        /// <summary>Number of identical objects on one shelf that clear together.</summary>
        public const int MatchCount = 3;

        /// <summary>True when <paramref name="identicalCount"/> identical objects form a clearing match.</summary>
        public static bool IsMatch(int identicalCount)
        {
            return identicalCount == MatchCount;
        }
    }
}
