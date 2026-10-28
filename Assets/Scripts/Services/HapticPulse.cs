namespace Game.Services
{
    /// <summary>The haptic cues the game uses. Both are short and light.</summary>
    public enum HapticPulse
    {
        /// <summary>A triple clear: a barely-there tick.</summary>
        Light = 0,

        /// <summary>A refused move: a short soft buzz.</summary>
        WrongMove = 1,
    }

    /// <summary>Duration and strength of each pulse (Android VibrationEffect.createOneShot arguments).</summary>
    public static class HapticPulses
    {
        public const long LightMs = 12;
        public const int LightAmplitude = 60;
        public const long WrongMoveMs = 35;
        public const int WrongMoveAmplitude = 120;

        public static long DurationMs(HapticPulse pulse) => pulse == HapticPulse.WrongMove ? WrongMoveMs : LightMs;

        /// <summary>1..255, as Android expects.</summary>
        public static int Amplitude(HapticPulse pulse) => pulse == HapticPulse.WrongMove ? WrongMoveAmplitude : LightAmplitude;
    }
}
