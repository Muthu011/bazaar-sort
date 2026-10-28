namespace Game.UI
{
    /// <summary>
    /// The HUD clear counter's display logic (issue #21). Display only: counts the triples cleared in the level being
    /// played, starts at 0 on every load (first load, Retry, Next, Replay) and is never saved, spent or valued; no coin
    /// field exists in Core or save data (Muthu: real coins come with boosters in Slice 3).
    ///
    /// A clear whose coin is flying shows when the coin lands (<see cref="CoinLanded"/>); a clear with no coin (skipped
    /// pop, coin pool busy) shows at once. <see cref="Shown"/> never passes <see cref="Cleared"/>. Pure, Edit Mode tested;
    /// texts are made once so showing a number never allocates.
    /// </summary>
    public sealed class ClearCounter
    {
        /// <summary>Largest number drawn as itself; above it the counter reads "99+".</summary>
        public const int MaxShown = 99;

        private static readonly string[] Texts = BuildTexts();

        /// <summary>Clears in this level so far.</summary>
        public int Cleared { get; private set; }

        /// <summary>The number on the HUD.</summary>
        public int Shown { get; private set; }

        /// <summary>Coins still flying.</summary>
        public int InFlight { get; private set; }

        /// <summary>Text for <see cref="Shown"/>.</summary>
        public string Text => Shown > MaxShown ? Texts[MaxShown + 1] : Texts[Shown];

        /// <summary>A level was loaded: back to zero, any flying coin no longer counts.</summary>
        public void Reset()
        {
            Cleared = 0;
            Shown = 0;
            InFlight = 0;
        }

        /// <summary>A triple was cleared. With <paramref name="coinFlying"/> the number waits for the coin.</summary>
        public void AddClear(bool coinFlying)
        {
            Cleared++;
            if (coinFlying) InFlight++;
            else Shown++;
        }

        /// <summary>A coin reached the counter: the number ticks up by one. Returns false if that coin no longer counts.</summary>
        public bool CoinLanded()
        {
            if (InFlight == 0) return false;
            InFlight--;
            if (Shown < Cleared) Shown++;
            return true;
        }

        private static string[] BuildTexts()
        {
            var texts = new string[MaxShown + 2];
            for (int i = 0; i <= MaxShown; i++) texts[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            texts[MaxShown + 1] = MaxShown + "+";
            return texts;
        }
    }
}
