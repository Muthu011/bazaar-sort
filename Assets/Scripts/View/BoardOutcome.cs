namespace Game.View
{
    /// <summary>How a board ended, as shown to the player once its animations finish (see <see cref="BoardView.OutcomeReady"/>).</summary>
    public enum BoardOutcome
    {
        /// <summary>Still playing.</summary>
        None = 0,

        /// <summary>Every item cleared (Core <c>Board.Won</c>).</summary>
        Won,

        /// <summary>
        /// Lost: items left, no legal move, and no booster can help (Core <c>Board.Failed</c>, issue #41).
        /// </summary>
        Failed,
    }
}
