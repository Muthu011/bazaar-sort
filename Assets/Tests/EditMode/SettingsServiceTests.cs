using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="SettingsService"/> over the fake store (issue #21): never touches real PlayerPrefs.</summary>
    public class SettingsServiceTests
    {
        [Test]
        public void Haptics_AreOnByDefault()
        {
            Assert.IsTrue(new SettingsService(new FakeKeyValueStore()).HapticsEnabled);
        }

        [Test]
        public void TurningHapticsOff_IsSavedAndLoadedBack()
        {
            var store = new FakeKeyValueStore();
            var settings = new SettingsService(store);
            settings.HapticsEnabled = false;
            Assert.AreEqual(0, store.GetInt(SettingsService.HapticsKey, -1));
            Assert.AreEqual(1, store.SaveCount, "Saved at once.");

            var reloaded = new SettingsService(store);
            Assert.IsFalse(reloaded.HapticsEnabled, "A new session reads the saved value.");

            reloaded.HapticsEnabled = true;
            Assert.IsTrue(new SettingsService(store).HapticsEnabled);
        }

        [Test]
        public void SettingTheSameValue_DoesNotSaveOrNotify()
        {
            var store = new FakeKeyValueStore();
            var settings = new SettingsService(store);
            int changed = 0;
            settings.Changed += () => changed++;
            settings.HapticsEnabled = true;
            Assert.AreEqual(0, store.SaveCount);
            Assert.AreEqual(0, changed);
            settings.HapticsEnabled = false;
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void FakeSettings_HoldsValueAndNotifies()
        {
            var fake = new FakeSettingsService();
            int changed = 0;
            fake.Changed += () => changed++;
            Assert.IsTrue(fake.HapticsEnabled);
            fake.HapticsEnabled = false;
            Assert.IsFalse(fake.HapticsEnabled);
            Assert.AreEqual(1, changed);
        }
    }
}
