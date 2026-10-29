namespace Game.UI
{
    /// <summary>
    /// The HUD clear tally (issue #21): counts the triples cleared in the level being played, starts at 0 on every load
    /// (first load, Retry, Next, Replay) and is never saved, spent or valued. Clears earn no coins and fly no coin
    /// (the per-clear coin flight was removed, Muthu on PR #63); the pill shows the coin wallet
    /// (<see cref="CoinBalanceText"/>), not this tally. Every clear counts exactly once, animated or skipped. Pure, Edit
    /// Mode tested.
    /// </summary>
    public sealed class ClearCounter
    {
        /// <summary>Clears in this level so far.</summary>
        public int Cleared { get; private set; }

        /// <summary>A level was loaded: back to zero.</summary>
        public void Reset() => Cleared = 0;

        /// <summary>A triple was cleared.</summary>
        public void AddClear() => Cleared++;
    }
}
