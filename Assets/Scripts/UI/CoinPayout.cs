namespace Game.UI
{
    /// <summary>
    /// A win's coins on their way from the Win card to the HUD pill (issue #44, Muthu on PR #63). The coins are saved
    /// the moment the board is won, but the pill keeps showing the old balance (<see cref="Shown"/> = wallet minus
    /// <see cref="Held"/>) until they fly in from the card's "+N" line, and climbs as each coin lands, so the number
    /// never jumps before the Win card. Pure, Edit Mode tested; the HUD drives it.
    /// </summary>
    public sealed class CoinPayout
    {
        private int _total;

        /// <summary>Coins already in the wallet that the pill doesn't show yet.</summary>
        public int Held { get; private set; }

        /// <summary>Coins that fly for the held amount (<see cref="UiFeel.CoinsFlownFor"/>).</summary>
        public int Flown { get; private set; }

        /// <summary>Flying coins that have landed.</summary>
        public int Landed { get; private set; }

        /// <summary>True between <see cref="Launch"/> and the last landing.</summary>
        public bool Flying => Flown > 0 && Landed < Flown && Launched;

        /// <summary>True once the held coins have set off.</summary>
        public bool Launched { get; private set; }

        /// <summary>A win is about to add <paramref name="coins"/> to the wallet: hold them back from the pill.</summary>
        public void Hold(int coins)
        {
            _total = coins < 0 ? 0 : coins;
            Held = _total;
            Flown = UiFeel.CoinsFlownFor(_total);
            Landed = 0;
            Launched = false;
        }

        /// <summary>The coins set off. Returns how many fly (0 when nothing is held or they already set off).</summary>
        public int Launch()
        {
            if (Held == 0 || Launched) return 0;
            Launched = true;
            return Flown;
        }

        /// <summary>One coin landed: the pill counts up its share. False when no coin of this payout was flying.</summary>
        public bool Land()
        {
            if (!Flying) return false;
            Landed++;
            Held = _total - UiFeel.CoinsLanded(_total, Flown, Landed);
            return true;
        }

        /// <summary>Held coins after <paramref name="landed"/> landings (for texts made ahead of the flight).</summary>
        public int HeldAfter(int landed) => _total - UiFeel.CoinsLanded(_total, Flown, landed);

        /// <summary>Shows everything at once (level load, a card without the coin line, no tweens).</summary>
        public void Release()
        {
            _total = 0;
            Held = 0;
            Flown = 0;
            Landed = 0;
            Launched = false;
        }

        /// <summary>The balance the pill shows for a wallet of <paramref name="wallet"/>.</summary>
        public int Shown(int wallet)
        {
            int shown = wallet - Held;
            return shown < 0 ? 0 : shown;
        }
    }
}
