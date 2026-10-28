using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="Haptics"/> respects the player's setting (issue #21), checked with the fake device.</summary>
    public class HapticsTests
    {
        [Test]
        public void PlaysThroughTheDevice_WhenOn()
        {
            var device = new FakeHaptics();
            var haptics = new Haptics(device, new SettingsService(new FakeKeyValueStore()));
            haptics.Play(HapticPulse.WrongMove);
            haptics.Play(HapticPulse.Light);
            Assert.AreEqual(1, device.WrongMoveCount);
            Assert.AreEqual(1, device.LightCount);
        }

        [Test]
        public void PlaysNothing_WhenOff()
        {
            var device = new FakeHaptics();
            var settings = new FakeSettingsService { HapticsEnabled = false };
            var haptics = new Haptics(device, settings);
            haptics.Play(HapticPulse.WrongMove);
            haptics.Play(HapticPulse.Light);
            Assert.AreEqual(0, device.Total);
        }

        [Test]
        public void FollowsTheSetting_AsItChanges()
        {
            var device = new FakeHaptics();
            var settings = new FakeSettingsService();
            var haptics = new Haptics(device, settings);
            haptics.Play(HapticPulse.Light);
            settings.HapticsEnabled = false;
            haptics.Play(HapticPulse.Light);
            settings.HapticsEnabled = true;
            haptics.Play(HapticPulse.Light);
            Assert.AreEqual(2, device.LightCount);
        }

        [Test]
        public void Pulses_AreShortAndLight()
        {
            Assert.LessOrEqual(HapticPulses.DurationMs(HapticPulse.Light), 20);
            Assert.LessOrEqual(HapticPulses.DurationMs(HapticPulse.WrongMove), 50);
            Assert.Less(HapticPulses.Amplitude(HapticPulse.Light), HapticPulses.Amplitude(HapticPulse.WrongMove));
            Assert.LessOrEqual(HapticPulses.Amplitude(HapticPulse.WrongMove), 160, "Soft, not a full-strength buzz.");
        }

        [Test]
        public void GameServices_InstallsAndResetsFakes()
        {
            var settings = new FakeSettingsService();
            var fake = new FakeHaptics();
            try
            {
                GameServices.Install(settings, fake);
                Assert.AreSame(settings, GameServices.Settings);
                Assert.AreSame(fake, GameServices.Haptics);
            }
            finally
            {
                GameServices.ResetToDefaults();
            }
            Assert.AreNotSame(fake, GameServices.Haptics, "Defaults come back.");
            GameServices.ResetToDefaults();
        }
    }
}
