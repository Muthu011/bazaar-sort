namespace Game.UI
{
    /// <summary>
    /// Pre-built strings for the dev fps readout, so updating it never allocates: "60 fps" and "max 17 ms". Values
    /// past the table clamp to the last entry (shown as "240+ fps", "250+ ms"). Built lazily, once. Edit Mode tested.
    /// </summary>
    public static class DevFpsLabels
    {
        public const int MaxFps = 240;
        public const int MaxMs = 250;

        private static string[] _fps;
        private static string[] _ms;

        public static string Fps(int fps)
        {
            if (_fps == null)
            {
                _fps = new string[MaxFps + 1];
                for (int i = 0; i < MaxFps; i++) _fps[i] = i + " fps";
                _fps[MaxFps] = MaxFps + "+ fps";
            }
            return _fps[fps < 0 ? 0 : fps > MaxFps ? MaxFps : fps];
        }

        public static string WorstMs(int ms)
        {
            if (_ms == null)
            {
                _ms = new string[MaxMs + 1];
                for (int i = 0; i < MaxMs; i++) _ms[i] = "max " + i + " ms";
                _ms[MaxMs] = "max " + MaxMs + "+ ms";
            }
            return _ms[ms < 0 ? 0 : ms > MaxMs ? MaxMs : ms];
        }
    }
}
