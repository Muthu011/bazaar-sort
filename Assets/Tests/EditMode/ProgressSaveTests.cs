using System.Collections.Generic;
using System.IO;
using Game.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// The progress save (issue #22): JSON format, the atomic file store in a temp folder (never the real save), load
    /// of missing, corrupt, truncated and other-version files, warn-once, and the service over the fake store.
    /// </summary>
    public class ProgressSaveTests
    {
        private string _dir;
        private string _path;
        private List<string> _warnings;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "bazaar-sort-progress-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, JsonFileProgressStore.DefaultFileName);
            _warnings = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private ProgressService Service(IProgressStore store) => new ProgressService(store, _warnings.Add);

        private static ProgressData Sample()
        {
            var data = new ProgressData { highestUnlocked = "w1_l003" };
            data.levels.Add(new LevelStars { id = "w1_l001", stars = 3 });
            data.levels.Add(new LevelStars { id = "w1_l002", stars = 2 });
            return data;
        }

        // ---- Serializer ----

        [Test]
        public void Serializer_RoundTrips_WithVersion()
        {
            string json = ProgressSerializer.Write(Sample());
            StringAssert.Contains("\"version\":1", json);
            Assert.IsTrue(ProgressSerializer.TryRead(json, out var data, out string problem), problem);
            Assert.AreEqual(ProgressSerializer.CurrentVersion, data.version);
            Assert.AreEqual("w1_l003", data.highestUnlocked);
            Assert.AreEqual(2, data.levels.Count);
            Assert.AreEqual("w1_l002", data.levels[1].id);
            Assert.AreEqual(2, data.levels[1].stars);
        }

        [Test]
        public void Serializer_RoundTripsCoins()
        {
            var sample = Sample();
            sample.coins = 135;
            string json = ProgressSerializer.Write(sample);
            StringAssert.Contains("\"coins\":135", json);
            Assert.IsTrue(ProgressSerializer.TryRead(json, out var data, out string problem), problem);
            Assert.AreEqual(135, data.coins);
            Assert.AreEqual(135, data.Clone().coins);
        }

        [Test]
        public void Serializer_ReadsASaveFromBeforeCoins_AsZeroCoins_KeepingEverythingElse()
        {
            // Exactly what a build before issue #44 wrote: no "coins" field.
            const string old = "{\"version\":1,\"highestUnlocked\":\"w1_l003\",\"levels\":[{\"id\":\"w1_l001\",\"stars\":3},"
                + "{\"id\":\"w1_l002\",\"stars\":2}],\"tutorialsDone\":[\"match3\"]}";
            Assert.IsTrue(ProgressSerializer.TryRead(old, out var data, out string problem), problem);
            Assert.AreEqual(0, data.coins);
            Assert.AreEqual("w1_l003", data.highestUnlocked);
            Assert.AreEqual(2, data.levels.Count);
            Assert.AreEqual(2, data.levels[1].stars);
            CollectionAssert.AreEqual(new[] { "match3" }, data.tutorialsDone);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json at all")]
        [TestCase("{\"version\":1,\"highestUnlocked\":\"w1_l0")] // truncated mid-write
        [TestCase("{\"version\":1,\"levels\":[{\"id\":\"w1_l001\",\"stars\":3}")] // truncated, no closing brace
        [TestCase("[1,2,3]")]
        [TestCase("{}")] // no version
        [TestCase("{\"version\":0}")]
        [TestCase("{\"version\":2,\"highestUnlocked\":\"w1_l002\",\"levels\":[]}")] // newer build's save
        [TestCase("{\"version\":1,\"levels\":[{\"id\":\"\",\"stars\":3}]}")]
        [TestCase("{\"version\":1,\"levels\":[{\"id\":\"w1_l001\",\"stars\":0}]}")]
        [TestCase("{\"version\":1,\"levels\":[{\"id\":\"w1_l001\",\"stars\":4}]}")]
        [TestCase("{\"version\":1,\"levels\":[{\"id\":\"w1_l001\",\"stars\":3},{\"id\":\"w1_l001\",\"stars\":3}]}")]
        [TestCase("{\"version\":1,\"levels\":[],\"coins\":-5}")] // negative wallet
        public void Serializer_RefusesBadSaves_WithAReason(string json)
        {
            Assert.IsFalse(ProgressSerializer.TryRead(json, out var data, out string problem));
            Assert.IsNull(data);
            Assert.IsFalse(string.IsNullOrEmpty(problem));
        }

        [Test]
        public void Serializer_NamesTheVersion_OnAMismatch()
        {
            ProgressSerializer.TryRead("{\"version\":7}", out _, out string problem);
            StringAssert.Contains("7", problem);
        }

        // ---- File store: save and load ----

        [Test]
        public void FileStore_SavesAndLoadsBack()
        {
            var store = new JsonFileProgressStore(_path);
            Assert.IsTrue(store.Save(Sample(), out string problem), problem);
            Assert.IsTrue(File.Exists(_path));
            Assert.IsFalse(File.Exists(store.TempPath), "Temp file is renamed into place.");

            var status = new JsonFileProgressStore(_path).Load(out var data, out problem);
            Assert.AreEqual(ProgressLoadStatus.Loaded, status, problem);
            Assert.AreEqual("w1_l003", data.highestUnlocked);
            Assert.AreEqual(2, data.levels.Count);
        }

        [Test]
        public void FileStore_OverwritesAnOlderSave()
        {
            var store = new JsonFileProgressStore(_path);
            store.Save(Sample(), out _);
            var newer = Sample();
            newer.highestUnlocked = "w1_l009";
            Assert.IsTrue(store.Save(newer, out string problem), problem);
            store.Load(out var data, out _);
            Assert.AreEqual("w1_l009", data.highestUnlocked);
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        [Test]
        public void FileStore_CreatesTheFolder()
        {
            var store = new JsonFileProgressStore(Path.Combine(_dir, "nested", "deeper", "progress.json"));
            Assert.IsTrue(store.Save(Sample(), out string problem), problem);
            Assert.AreEqual(ProgressLoadStatus.Loaded, store.Load(out _, out _));
        }

        [Test]
        public void FileStore_MissingFile_IsFreshProgress_NotAProblem()
        {
            var status = new JsonFileProgressStore(_path).Load(out var data, out string problem);
            Assert.AreEqual(ProgressLoadStatus.Missing, status);
            Assert.IsNull(problem);
            Assert.IsNotNull(data);
            Assert.AreEqual(string.Empty, data.highestUnlocked);
            Assert.AreEqual(0, data.levels.Count);
        }

        [Test]
        public void FileStore_CorruptFile_IsFreshProgress_AndIsMovedAside()
        {
            File.WriteAllText(_path, "{\"version\":1,\"highestUnl\u0000\u0000garbage");
            var store = new JsonFileProgressStore(_path);
            var status = store.Load(out var data, out string problem);
            Assert.AreEqual(ProgressLoadStatus.Unreadable, status);
            Assert.IsFalse(string.IsNullOrEmpty(problem));
            Assert.AreEqual(0, data.levels.Count);
            Assert.IsFalse(File.Exists(_path), "The bad file no longer sits where the next save goes.");
            Assert.IsTrue(File.Exists(store.UnreadablePath), "Kept for a bug report.");

            Assert.IsTrue(store.Save(Sample(), out problem), problem);
            Assert.AreEqual(ProgressLoadStatus.Loaded, store.Load(out _, out _), "The next save works.");
        }

        [Test]
        public void FileStore_NewerVersion_IsFreshProgress()
        {
            File.WriteAllText(_path, "{\"version\":2,\"highestUnlocked\":\"w1_l010\",\"levels\":[]}");
            var status = new JsonFileProgressStore(_path).Load(out var data, out string problem);
            Assert.AreEqual(ProgressLoadStatus.Unreadable, status);
            StringAssert.Contains("version 2", problem);
            Assert.AreEqual(string.Empty, data.highestUnlocked);
        }

        [Test]
        public void FileStore_CrashWhileWritingTemp_KeepsTheOldSave()
        {
            var store = new JsonFileProgressStore(_path);
            store.Save(Sample(), out _);
            File.WriteAllText(store.TempPath, "{\"version\":1,\"highestUnlocked\":\"w1_l0"); // half-written temp
            var status = store.Load(out var data, out string problem);
            Assert.AreEqual(ProgressLoadStatus.Loaded, status, problem);
            Assert.AreEqual("w1_l003", data.highestUnlocked);

            Assert.IsTrue(store.Save(Sample(), out problem), "A stale temp file doesn't block the next save: " + problem);
        }

        [Test]
        public void FileStore_CrashBetweenDeleteAndMove_RecoversFromTemp()
        {
            var store = new JsonFileProgressStore(_path);
            File.WriteAllText(store.TempPath, ProgressSerializer.Write(Sample()));
            var status = store.Load(out var data, out string problem);
            Assert.AreEqual(ProgressLoadStatus.Loaded, status, problem);
            Assert.AreEqual("w1_l003", data.highestUnlocked);
        }

        [Test]
        public void FileStore_WriteFailure_ReturnsFalse_DoesNotThrow()
        {
            // The save path is a folder: every write fails like a broken disk.
            Directory.CreateDirectory(_path);
            var store = new JsonFileProgressStore(_path);
            Assert.DoesNotThrow(() => Assert.IsFalse(store.Save(Sample(), out string problem)));
        }

        // ---- Service ----

        [Test]
        public void Service_RecordsWin_SavesAtOnce_AndAFreshServiceReadsIt()
        {
            var store = new JsonFileProgressStore(_path);
            var progress = Service(store);
            int changed = 0;
            progress.Changed += () => changed++;
            progress.RecordWin("w1_l001", 3, "w1_l002", 0);
            Assert.AreEqual(1, changed);
            Assert.IsTrue(File.Exists(_path));

            var restarted = Service(new JsonFileProgressStore(_path));
            Assert.AreEqual(3, restarted.GetStars("w1_l001"));
            Assert.AreEqual(0, restarted.GetStars("w1_l002"));
            Assert.AreEqual("w1_l002", restarted.HighestUnlocked);
            Assert.AreEqual(ProgressLoadStatus.Loaded, restarted.LoadStatus);
            CollectionAssert.IsEmpty(_warnings);
        }

        [Test]
        public void Service_KeepsBestStars()
        {
            var progress = Service(new FakeProgressStore());
            progress.RecordWin("w1_l001", 3, "w1_l002", 0);
            progress.RecordWin("w1_l001", 1, "w1_l002", 0);
            Assert.AreEqual(3, progress.GetStars("w1_l001"));
        }

        [Test]
        public void Service_LoadsOnce()
        {
            var store = new FakeProgressStore();
            var progress = Service(store);
            progress.GetStars("a");
            progress.GetStars("b");
            _ = progress.HighestUnlocked;
            Assert.AreEqual(1, store.LoadCount);
        }

        [Test]
        public void Service_MissingSave_StartsFresh_WithoutWarning()
        {
            var progress = Service(new FakeProgressStore());
            Assert.AreEqual(string.Empty, progress.HighestUnlocked);
            Assert.AreEqual(0, progress.GetStars("w1_l001"));
            Assert.AreEqual(ProgressLoadStatus.Missing, progress.LoadStatus);
            CollectionAssert.IsEmpty(_warnings);
        }

        [Test]
        public void Service_CorruptSave_StartsFresh_AndWarnsOnlyOnce()
        {
            File.WriteAllText(_path, "\u0001\u0002 definitely not progress");
            var progress = Service(new JsonFileProgressStore(_path));
            Assert.DoesNotThrow(() => progress.GetStars("w1_l001"));
            Assert.AreEqual(0, progress.GetStars("w1_l001"));
            Assert.AreEqual(string.Empty, progress.HighestUnlocked);
            Assert.AreEqual(ProgressLoadStatus.Unreadable, progress.LoadStatus);
            Assert.AreEqual(1, _warnings.Count);

            progress.RecordWin("w1_l001", 3, "w1_l002", 0);
            progress.GetStars("w1_l001");
            Assert.AreEqual(1, _warnings.Count, "One warning, however much the game goes on.");
            Assert.AreEqual(3, Service(new JsonFileProgressStore(_path)).GetStars("w1_l001"), "Fresh progress is saved over it.");
        }

        [Test]
        public void Service_FailedWrites_KeepPlaying_InMemory_AndWarnOnce()
        {
            var store = new FakeProgressStore { FailSaves = true };
            var progress = Service(store);
            progress.RecordWin("w1_l001", 3, "w1_l002", 0);
            progress.RecordWin("w1_l002", 3, "w1_l003", 0);
            Assert.AreEqual(3, progress.GetStars("w1_l002"));
            Assert.AreEqual("w1_l003", progress.HighestUnlocked);
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void Service_Reset_ForgetsEverything_AndSaves()
        {
            var store = new FakeProgressStore();
            var progress = Service(store);
            progress.RecordWin("w1_l001", 3, "w1_l002", 0);
            progress.Reset();
            Assert.AreEqual(0, progress.GetStars("w1_l001"));
            Assert.AreEqual(string.Empty, progress.HighestUnlocked);
            Assert.AreEqual(0, Service(store).GetStars("w1_l001"));
        }

        // ---- Coin wallet (issue #44) ----

        [Test]
        public void Wallet_StartsEmpty()
        {
            Assert.AreEqual(0, Service(new FakeProgressStore()).Coins);
        }

        [Test]
        public void Wallet_Earn_AddsOnAWin_InTheSameSave_AndPersists()
        {
            var store = new FakeProgressStore();
            var progress = Service(store);
            progress.RecordWin("w1_l001", 3, "w1_l002", 20);
            progress.RecordWin("w1_l002", 2, "w1_l003", 15);
            Assert.AreEqual(35, progress.Coins);
            Assert.AreEqual(2, store.SaveCount, "One save per win, coins included.");
            Assert.AreEqual(35, Service(store).Coins, "A fresh launch reads the wallet back.");
        }

        [Test]
        public void Wallet_Earn_RefusesNegativeCoins()
        {
            var progress = Service(new FakeProgressStore());
            Assert.Throws<System.ArgumentOutOfRangeException>(() => progress.RecordWin("w1_l001", 3, "w1_l002", -1));
            Assert.AreEqual(0, progress.Coins);
            Assert.AreEqual(0, progress.GetStars("w1_l001"), "A refused win records nothing.");
        }

        [Test]
        public void Wallet_Spend_Pays_Saves_AndRaisesChanged()
        {
            var store = new FakeProgressStore();
            var progress = Service(store);
            progress.RecordWin("w1_l001", 3, "w1_l002", 60);
            int saves = store.SaveCount;
            int changed = 0;
            progress.Changed += () => changed++;

            Assert.IsTrue(progress.TrySpendCoins(50));
            Assert.AreEqual(10, progress.Coins);
            Assert.AreEqual(saves + 1, store.SaveCount);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(10, Service(store).Coins, "The spend is saved.");
        }

        [Test]
        public void Wallet_Spend_ExactBalance_LeavesZero()
        {
            var progress = Service(new FakeProgressStore());
            progress.RecordWin("w1_l001", 3, "w1_l002", 20);
            Assert.IsTrue(progress.TrySpendCoins(20));
            Assert.AreEqual(0, progress.Coins);
        }

        [Test]
        public void Wallet_Spend_InsufficientFunds_ChangesNothing_AndDoesNotSave()
        {
            var store = new FakeProgressStore();
            var progress = Service(store);
            progress.RecordWin("w1_l001", 3, "w1_l002", 20);
            int saves = store.SaveCount;
            int changed = 0;
            progress.Changed += () => changed++;

            Assert.IsFalse(progress.TrySpendCoins(21));
            Assert.AreEqual(20, progress.Coins);
            Assert.AreEqual(saves, store.SaveCount);
            Assert.AreEqual(0, changed);
        }

        [Test]
        public void Wallet_Spend_FromEmpty_Fails()
        {
            var progress = Service(new FakeProgressStore());
            Assert.IsFalse(progress.TrySpendCoins(1));
            Assert.AreEqual(0, progress.Coins);
        }

        [Test]
        public void Wallet_Spend_ZeroIsFree_AndNegativeThrows()
        {
            var store = new FakeProgressStore();
            var progress = Service(store);
            Assert.IsTrue(progress.TrySpendCoins(0));
            Assert.AreEqual(0, store.SaveCount, "Nothing changed, nothing saved.");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => progress.TrySpendCoins(-10));
            Assert.AreEqual(0, progress.Coins);
        }

        [Test]
        public void Wallet_SaveFromBeforeCoins_LoadsWithoutWarning_AndEarnsFromZero()
        {
            var store = new FakeProgressStore
            {
                Json = "{\"version\":1,\"highestUnlocked\":\"w1_l002\",\"levels\":[{\"id\":\"w1_l001\",\"stars\":3}],\"tutorialsDone\":[]}",
            };
            var progress = Service(store);
            Assert.AreEqual(ProgressLoadStatus.Loaded, progress.LoadStatus);
            Assert.AreEqual(0, progress.Coins);
            Assert.AreEqual(3, progress.GetStars("w1_l001"));
            progress.RecordWin("w1_l002", 1, "w1_l003", 10);
            Assert.AreEqual(10, Service(store).Coins);
            Assert.AreEqual(3, Service(store).GetStars("w1_l001"));
            CollectionAssert.IsEmpty(_warnings);
        }

        [Test]
        public void Wallet_FileStore_RoundTripsCoinsAndBestStars()
        {
            var progress = Service(new JsonFileProgressStore(_path));
            progress.RecordWin("w1_l001", 2, "w1_l002", 15);
            progress.RecordWin("w1_l001", 1, "w1_l002", 10);
            Assert.IsTrue(progress.TrySpendCoins(5));

            var restarted = Service(new JsonFileProgressStore(_path));
            Assert.AreEqual(20, restarted.Coins);
            Assert.AreEqual(2, restarted.GetStars("w1_l001"), "Best stars kept, not the last.");
            CollectionAssert.IsEmpty(_warnings);
        }

        [Test]
        public void Wallet_Reset_EmptiesIt()
        {
            var store = new FakeProgressStore();
            var progress = Service(store);
            progress.RecordWin("w1_l001", 3, "w1_l002", 20);
            progress.Reset();
            Assert.AreEqual(0, progress.Coins);
            Assert.AreEqual(0, Service(store).Coins);
        }

        [Test]
        public void GameServices_InstallsAndResetsProgress()
        {
            var fake = Service(new FakeProgressStore());
            try
            {
                GameServices.Install(null, null, fake);
                Assert.AreSame(fake, GameServices.Progress);
            }
            finally
            {
                GameServices.ResetToDefaults();
            }
        }
    }
}
