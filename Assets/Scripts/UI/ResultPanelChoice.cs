using Game.View;

namespace Game.UI
{
    /// <summary>Which result card is on screen.</summary>
    public enum ResultPanel
    {
        None = 0,

        /// <summary>Won a level that has a next one: Next and Retry.</summary>
        Win,

        /// <summary>No legal move left: Retry.</summary>
        Stuck,

        /// <summary>Won the last available level: "More levels coming soon" with Replay (goes to level 1).</summary>
        MoreLevelsSoon,
    }

    /// <summary>
    /// Picks the result card for a board outcome. Pure, Edit Mode tested. Rule (Muthu, PR #17): after the last
    /// available level, show "More levels coming soon" with Replay, which goes to L1; Next never loops.
    /// </summary>
    public static class ResultPanelChoice
    {
        public static ResultPanel For(BoardOutcome outcome, bool hasNextLevel)
        {
            switch (outcome)
            {
                case BoardOutcome.Won: return hasNextLevel ? ResultPanel.Win : ResultPanel.MoreLevelsSoon;
                case BoardOutcome.Stuck: return ResultPanel.Stuck;
                default: return ResultPanel.None;
            }
        }
    }
}
