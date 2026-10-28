using System;

namespace Game.Services
{
    /// <summary>In-memory <see cref="ISettingsService"/> for tests: no storage at all.</summary>
    public sealed class FakeSettingsService : ISettingsService
    {
        private bool _haptics = true;

        public event Action Changed;

        public bool HapticsEnabled
        {
            get => _haptics;
            set
            {
                if (_haptics == value) return;
                _haptics = value;
                Changed?.Invoke();
            }
        }
    }
}
