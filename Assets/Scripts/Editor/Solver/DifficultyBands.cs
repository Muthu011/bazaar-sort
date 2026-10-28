using System;

namespace Game.Solver
{
    /// <summary>An inclusive range of acceptable estimated first-try win rates.</summary>
    public readonly struct WinRateBand
    {
        public WinRateBand(string name, double min, double max)
        {
            Name = name;
            Min = min;
            Max = max;
        }

        public string Name { get; }
        public double Min { get; }
        public double Max { get; }

        public bool Contains(double winRate) => winRate >= Min - 1e-9 && winRate <= Max + 1e-9;

        public override string ToString() => $"{Name} [{Min:0.00}, {Max:0.00}]";
    }

    /// <summary>
    /// The one place that maps a level to its target first-try win-rate band (estWinRate from
    /// <see cref="PlaySimulator"/>). Source: .claude/agents/level-designer.md.
    /// <list type="table">
    /// <item><term>levels 1 to 10, any role</term><description>0.90 to 1.00 ("above 90%"; 0.90 itself passes)</description></item>
    /// <item><term>medium ("normal")</term><description>0.70 to 0.85</description></item>
    /// <item><term>hard, boss</term><description>0.35 to 0.50</description></item>
    /// <item><term>easy (from level 11)</term><description>0.85 to 1.00: not named in the targets; easier than
    /// normal, so it starts where normal ends</description></item>
    /// <item><term>breather (from level 11)</term><description>0.85 to 1.00: not named in the targets; a rest after a
    /// hard level, treated like easy</description></item>
    /// </list>
    /// The level number is the level's <c>index</c>. Every level today is in World 1, where index is the game-wide
    /// level number; revisit this when World 2 starts (does its index restart at 1?).
    /// Any other role has no band, and the pack tests fail on it.
    /// </summary>
    public static class DifficultyBands
    {
        public const int OnboardingLevels = 10;

        public static readonly WinRateBand Onboarding = new WinRateBand("levels 1-10", 0.90, 1.00);
        public static readonly WinRateBand Easy = new WinRateBand("easy", 0.85, 1.00);
        public static readonly WinRateBand Breather = new WinRateBand("breather", 0.85, 1.00);
        public static readonly WinRateBand Medium = new WinRateBand("medium", 0.70, 0.85);
        public static readonly WinRateBand Hard = new WinRateBand("hard", 0.35, 0.50);
        public static readonly WinRateBand Boss = new WinRateBand("boss", 0.35, 0.50);

        public static bool TryGetBand(int levelIndex, string role, out WinRateBand band)
        {
            if (levelIndex >= 1 && levelIndex <= OnboardingLevels)
            {
                band = Onboarding;
                return true;
            }

            switch (role)
            {
                case "easy": band = Easy; return true;
                case "breather": band = Breather; return true;
                case "medium": band = Medium; return true;
                case "hard": band = Hard; return true;
                case "boss": band = Boss; return true;
                default:
                    band = default;
                    return false;
            }
        }
    }
}
