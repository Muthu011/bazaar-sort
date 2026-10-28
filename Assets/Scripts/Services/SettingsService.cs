using System;

namespace Game.Services
{
    /// <summary>
    /// <see cref="ISettingsService"/> saved through an <see cref="IKeyValueStore"/>: loaded once on construction,
    /// written and saved on every change. Nothing here is economy or progress data.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        public const string HapticsKey = "settings.haptics";

        private readonly IKeyValueStore _store;
        private bool _haptics;

        public SettingsService(IKeyValueStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _haptics = _store.GetInt(HapticsKey, 1) != 0;
        }

        public event Action Changed;

        public bool HapticsEnabled
        {
            get => _haptics;
            set
            {
                if (_haptics == value) return;
                _haptics = value;
                _store.SetInt(HapticsKey, value ? 1 : 0);
                _store.Save();
                Changed?.Invoke();
            }
        }
    }
}
