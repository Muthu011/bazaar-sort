namespace Game.UI
{
    /// <summary>How a level cell in the dev level select looks.</summary>
    public enum DevLevelCellState
    {
        /// <summary>Not unlocked in the save yet. Still playable here: it's a dev tool.</summary>
        Locked = 0,

        /// <summary>Unlocked, never won: where Continue would go.</summary>
        Open,

        /// <summary>Won at least once (stars shown).</summary>
        Cleared,

        /// <summary>The level on the board now.</summary>
        Current,
    }

    /// <summary>Picks a dev level cell's look from progress. Pure, Edit Mode tested.</summary>
    public static class DevLevelCell
    {
        public static DevLevelCellState StateFor(bool isCurrent, int stars, bool unlocked)
        {
            if (isCurrent) return DevLevelCellState.Current;
            if (stars > 0) return DevLevelCellState.Cleared;
            return unlocked ? DevLevelCellState.Open : DevLevelCellState.Locked;
        }
    }
}
