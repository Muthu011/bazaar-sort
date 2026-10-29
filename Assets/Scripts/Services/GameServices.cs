using System.IO;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// The one service locator (CLAUDE.md: no singletons except GameServices). Real services are made on first use:
    /// settings over PlayerPrefs, haptics through the Android vibrator on the phone and a no-op elsewhere, progress as
    /// <c>progress.json</c> in <c>Application.persistentDataPath</c>. Tests swap in fakes with <see cref="Install"/>
    /// and put the defaults back with <see cref="ResetToDefaults"/>.
    /// </summary>
    public static class GameServices
    {
        private static ISettingsService _settings;
        private static IHaptics _haptics;
        private static IProgressService _progress;

        public static ISettingsService Settings => _settings ??= new SettingsService(new PlayerPrefsStore());

        public static IHaptics Haptics => _haptics ??= new Haptics(CreateDevice(), Settings);

        public static IProgressService Progress => _progress ??= new ProgressService(
            new JsonFileProgressStore(Path.Combine(Application.persistentDataPath, JsonFileProgressStore.DefaultFileName)));

        /// <summary>Replaces the services (tests). A null argument keeps the current one.</summary>
        public static void Install(ISettingsService settings, IHaptics haptics, IProgressService progress = null)
        {
            if (settings != null) _settings = settings;
            if (haptics != null) _haptics = haptics;
            if (progress != null) _progress = progress;
        }

        /// <summary>Drops installed services; the next access makes the real ones again.</summary>
        public static void ResetToDefaults()
        {
            _settings = null;
            _haptics = null;
            _progress = null;
        }

        /// <summary>
        /// The project enters play mode without a domain reload, so statics outlive a play session. Dropping the services
        /// here makes every play session read the save from disk again, like a fresh app launch on the phone.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => ResetToDefaults();

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
