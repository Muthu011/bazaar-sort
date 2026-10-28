namespace Game.Services
{
    /// <summary>
    /// Records pulses for tests. As an <see cref="IHapticDevice"/> it sits under the real <see cref="Haptics"/> (so the
    /// setting check is exercised); as an <see cref="IHaptics"/> it records every request. Counting allocates nothing.
    /// </summary>
    public sealed class FakeHaptics : IHaptics, IHapticDevice
    {
        public int LightCount { get; private set; }
        public int WrongMoveCount { get; private set; }
        public int Total => LightCount + WrongMoveCount;

        public void Play(HapticPulse pulse)
        {
            if (pulse == HapticPulse.WrongMove) WrongMoveCount++;
            else LightCount++;
        }

        public void Clear()
        {
            LightCount = 0;
            WrongMoveCount = 0;
        }
    }
}
