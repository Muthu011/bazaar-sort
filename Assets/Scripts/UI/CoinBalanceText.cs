using System.Globalization;

namespace Game.UI
{
    /// <summary>
    /// The HUD coin pill's number (issue #44): the wallet balance as whole digits, "9999+" above
    /// <see cref="MaxShown"/> so the text never outgrows the 84 dp pill. Called only when the balance changes (a win,
    /// a spend, a level load), never per frame. Pure, Edit Mode tested.
    /// </summary>
    public static class CoinBalanceText
    {
        /// <summary>Largest balance drawn as itself.</summary>
        public const int MaxShown = 9999;

        public static string For(int balance)
        {
            if (balance <= 0) return "0";
            if (balance > MaxShown) return MaxShown.ToString(CultureInfo.InvariantCulture) + "+";
            return balance.ToString(CultureInfo.InvariantCulture);
        }
    }
}
