using System.Globalization;

namespace Game.UI
{
    /// <summary>
    /// The Win card's coin line (issue #44, Muthu on PR #63): "+N", the coins this win earned. The project has no
    /// localisation system yet, so like the other Win card text (<c>ResultPanels.WinTitle</c> and friends: an English
    /// constant in code) the English template lives here, next to <see cref="Key"/>, the id the string will have in the
    /// localisation table (English, Tamil, Hindi) when it arrives. With 0 coins (a replay that beat no star of the old
    /// best) there is no line at all: <see cref="For"/> returns empty and the card hides it. Called once per win, never
    /// per frame. Pure, Edit Mode tested.
    /// </summary>
    public static class WinCoinsText
    {
        /// <summary>Localisation key of the line.</summary>
        public const string Key = "win.coins_earned";

        /// <summary>English template; {0} is the coin count.</summary>
        public const string English = "+{0}";

        /// <summary>The line for <paramref name="coins"/>, or empty for 0 or fewer (hidden).</summary>
        public static string For(int coins)
        {
            if (coins <= 0) return string.Empty;
            return string.Format(CultureInfo.InvariantCulture, English, coins);
        }

        /// <summary>True when the line is shown for <paramref name="coins"/>.</summary>
        public static bool IsShown(int coins) => coins > 0;
    }
}
