namespace Game.Core
{
    /// <summary>
    /// The seam the board asks when a move leaves it stuck (no legal move, items left): could a booster the player can
    /// still use get this board moving again? If yes the level is not lost yet, so <see cref="Board.Failed"/> is not
    /// raised; if no, it is.
    ///
    /// Boosters (Undo, Shuffle, Extra Slot) arrive with issue #42, which plugs its own implementation in through
    /// <see cref="Board.Rescue"/>. Until then every board uses <see cref="NoBoosterRescue"/>: nothing can help, so stuck
    /// means failed. Implementations must not change the board.
    /// </summary>
    public interface IBoosterRescue
    {
        /// <summary>True when some booster could still turn the stuck <paramref name="board"/> into a playable one.</summary>
        bool CanRescue(Board board);
    }

    /// <summary>No boosters exist (yet): a stuck board can never be rescued.</summary>
    public sealed class NoBoosterRescue : IBoosterRescue
    {
        public static readonly NoBoosterRescue Instance = new NoBoosterRescue();

        private NoBoosterRescue()
        {
        }

        public bool CanRescue(Board board) => false;
    }
}
