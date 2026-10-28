namespace Game.Services
{
    /// <summary>Game-facing haptics: plays a pulse when the player has haptics on.</summary>
    public interface IHaptics
    {
        void Play(HapticPulse pulse);
    }

    /// <summary><see cref="IHaptics"/> that checks <see cref="ISettingsService.HapticsEnabled"/> on every pulse.</summary>
    public sealed class Haptics : IHaptics
    {
        private readonly IHapticDevice _device;
        private readonly ISettingsService _settings;

        public Haptics(IHapticDevice device, ISettingsService settings)
        {
            _device = device ?? throw new System.ArgumentNullException(nameof(device));
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        }

        public void Play(HapticPulse pulse)
        {
            if (!_settings.HapticsEnabled) return;
            _device.Play(pulse);
        }
    }
}
