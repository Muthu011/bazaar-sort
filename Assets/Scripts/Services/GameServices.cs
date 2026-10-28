namespace Game.Services
{
    /// <summary>
    /// The one service locator (CLAUDE.md: no singletons except GameServices). Real services are made on first use:
    /// settings over PlayerPrefs, haptics through the Android vibrator on the phone and a no-op elsewhere. Tests swap
    /// in fakes with <see cref="Install"/> and put the defaults back with <see cref="ResetToDefaults"/>.
    /// </summary>
    public static class GameServices
    {
        private static ISettingsService _settings;
        private static IHaptics _haptics;

        public static ISettingsService Settings => _settings ??= new SettingsService(new PlayerPrefsStore());

        public static IHaptics Haptics => _haptics ??= new Haptics(CreateDevice(), Settings);

        /// <summary>Replaces the services (tests). A null argument keeps the current one.</summary>
        public static void Install(ISettingsService settings, IHaptics haptics)
        {
            if (settings != null) _settings = settings;
            if (haptics != null) _haptics = haptics;
        }

        /// <summary>Drops installed services; the next access makes the real ones again.</summary>
        public static void ResetToDefaults()
        {
            _settings = null;
            _haptics = null;
        }

        private static IHapticDevice CreateDevice()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidHapticDevice();
#else
            return new NoOpHapticDevice();
#endif
        }
    }
}
