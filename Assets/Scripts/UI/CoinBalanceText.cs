using System.Globalization;

namespace Game.UI
{
    /// <summary>
    /// The HUD coin pill's number (issue #44): the wallet balance as whole digits up to <see cref="MaxShown"/> (5 digits),
    /// then <see cref="OverflowText"/>. The pill widens to fit the text (<see cref="HudLayout.CounterWidthFor"/>) and the
    /// text is never shrunk (Muthu, PR #63). "99K+" rather than "99999+": six glyphs would not fit beside three 48 dp
    /// booster slots on a 360 dp phone, while "99K+" is narrower than five digits and still true ("more than 99
    /// thousand"). Called only when the shown balance changes (a win's coins landing, a spend, a level load), never per
    /// frame. Pure, Edit Mode tested.
    /// </summary>
    public static class CoinBalanceText
    {
        /// <summary>Largest balance drawn as itself.</summary>
        public const int MaxShown = 99999;

        /// <summary>Drawn for any balance above <see cref="MaxShown"/>.</summary>
        public const string OverflowText = "99K+";

        public static string For(int balance)
        {
            if (balance <= 0) return "0";
            if (balance > MaxShown) return OverflowText;
            return balance.ToString(CultureInfo.InvariantCulture);
        }
    }
}
