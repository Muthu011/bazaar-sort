namespace Game.View
{
    /// <summary>How a board ended, as shown to the player once its animations finish (see <see cref="BoardView.OutcomeReady"/>).</summary>
    public enum BoardOutcome
    {
        /// <summary>Still playing.</summary>
        None = 0,

        /// <summary>Every item cleared (Core <c>Board.Won</c>).</summary>
        Won,

        /// <summary>Items left but no legal move (Core <c>Board.Stuck</c>).</summary>
        Stuck,
    }
}
