namespace Game.Services
{
    /// <summary>
    /// The phone's vibrator, without any setting check. <see cref="AndroidHapticDevice"/> on the phone,
    /// <see cref="NoOpHapticDevice"/> in the editor and on other platforms, <see cref="FakeHapticDevice"/> in tests.
    /// </summary>
    public interface IHapticDevice
    {
        void Play(HapticPulse pulse);
    }

    /// <summary>Does nothing: the editor and platforms without a vibrator.</summary>
    public sealed class NoOpHapticDevice : IHapticDevice
    {
        public void Play(HapticPulse pulse) { }
    }
}
