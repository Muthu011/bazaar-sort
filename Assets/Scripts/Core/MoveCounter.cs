namespace Game.Core
{
    /// <summary>
    /// The moves made on the level being played, the number the star rule reads (issue #44,
    /// docs/game-design.md "Stars and coins"). Every successful move counts once; a drop back on the same shelf is a
    /// cancel and never reaches it (Core raises no <see cref="Board.Moved"/> for it).
    ///
    /// Rule (Muthu, PR #63): <b>undone moves still count</b>. Undo (#53) takes a move back on the board but never takes it
    /// off this count, so there is deliberately no way to lower it: only <see cref="Count"/> (up by one) and
    /// <see cref="Reset"/> (back to 0 on a level load: first load, Retry, Next, Replay). MoveCounterTests fails if a
    /// decrement is added.
    /// </summary>
    public sealed class MoveCounter
    {
        /// <summary>Moves since the last <see cref="Reset"/>.</summary>
        public int Value { get; private set; }

        /// <summary>One more move. Holds at <see cref="int.MaxValue"/> instead of wrapping.</summary>
        public void Count()
        {
            if (Value < int.MaxValue) Value++;
        }

        /// <summary>A level was (re)loaded: back to 0. Never called by Undo.</summary>
        public void Reset() => Value = 0;
    }
}
