using Game.View;

namespace Game.UI
{
    /// <summary>Which result card is on screen.</summary>
    public enum ResultPanel
    {
        None = 0,

        /// <summary>Won a level that has a next one: Next and Retry.</summary>
        Win,

        /// <summary>Lost, out of space (issue #41): Retry, and later a rewarded-ad Continue (hidden until Slice 6).</summary>
        Fail,

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
                case BoardOutcome.Failed: return ResultPanel.Fail;
                default: return ResultPanel.None;
            }
        }
    }
}
