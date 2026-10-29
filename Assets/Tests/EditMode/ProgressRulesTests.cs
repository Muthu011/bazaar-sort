using System.IO;
using System.Linq;
using Game.Levels;
using Game.Services;
using Game.UI;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Unlock and continue (issue #22) over the fake store, the play order of level files, and the generated level
    /// list that the game, the level select and the save all read.
    /// </summary>
    public class ProgressRulesTests
    {
        private static readonly string[] Ids = { "w1_l001", "w1_l002", "w1_l003", "w1_l004" };

        private static ProgressService Fresh() => new ProgressService(new FakeProgressStore(), _ => { });

        // ---- Continue ----

        [Test]
        public void Continue_OnAFreshSave_IsLevelOne()
        {
            var point = ProgressRules.Continue(Ids, Fresh());
            Assert.AreEqual(0, point.Index);
            Assert.IsFalse(point.AllCleared);
        }

        [Test]
        public void Continue_IsTheNextUnplayedLevel()
        {
            var progress = Fresh();
            ProgressRules.RecordWin(Ids, progress, 0);
            ProgressRules.RecordWin(Ids, progress, 1);
            Assert.AreEqual(2, ProgressRules.Continue(Ids, progress).Index);
        }

        [Test]
        public void Continue_IsTheFirstUnplayedLevel_WhenOneWasSkipped()
        {
            // Only possible through the dev level select: L1 and L3 won, L2 never.
            var progress = Fresh();
            ProgressRules.RecordWin(Ids, progress, 0);
            ProgressRules.RecordWin(Ids, progress, 2);
            Assert.AreEqual(1, ProgressRules.Continue(Ids, progress).Index);
        }

        [Test]
        public void Continue_WithEveryLevelWon_IsTheLastLevel_AllCleared()
        {
            var progress = Fresh();
            for (int i = 0; i < Ids.Length; i++) ProgressRules.RecordWin(Ids, progress, i);
            var point = ProgressRules.Continue(Ids, progress);
            Assert.IsTrue(point.AllCleared);
            Assert.AreEqual(Ids.Length - 1, point.Index);
        }

        [Test]
        public void Continue_AfterANewLevelShipsIsThatLevel()
        {
            // Every level of a 3-level game won; the next update adds a 4th (e.g. w1_l015 after #36).
            var progress = Fresh();
            var three = Ids.Take(3).ToArray();
            for (int i = 0; i < three.Length; i++) ProgressRules.RecordWin(three, progress, i);
            Assert.IsTrue(ProgressRules.Continue(three, progress).AllCleared);

            var point = ProgressRules.Continue(Ids, progress);
            Assert.IsFalse(point.AllCleared);
            Assert.AreEqual(3, point.Index);
        }

        [Test]
        public void Continue_IgnoresSavedLevelsThatNoLongerExist()
        {
            var progress = Fresh();
            progress.RecordWin("w9_l999", 3, "w9_l999");
            Assert.AreEqual(0, ProgressRules.Continue(Ids, progress).Index);
            Assert.AreEqual(0, ProgressRules.HighestUnlockedIndex(Ids, progress));
        }

        // ---- Unlock ----

        [Test]
        public void LevelOne_IsAlwaysUnlocked_AndNothingElseOnAFreshSave()
        {
            var progress = Fresh();
            Assert.IsTrue(ProgressRules.IsUnlocked(Ids, progress, 0));
            Assert.IsFalse(ProgressRules.IsUnlocked(Ids, progress, 1));
            Assert.IsFalse(ProgressRules.IsUnlocked(Ids, progress, -1));
            Assert.IsFalse(ProgressRules.IsUnlocked(Ids, progress, Ids.Length));
        }

        [Test]
        public void Win_UnlocksTheNextLevel_WithThreeStars()
        {
            var progress = Fresh();
            ProgressRules.RecordWin(Ids, progress, 0);
            Assert.AreEqual("w1_l002", progress.HighestUnlocked);
            Assert.AreEqual(3, progress.GetStars("w1_l001"));
            Assert.IsTrue(ProgressRules.IsUnlocked(Ids, progress, 1));
            Assert.IsFalse(ProgressRules.IsUnlocked(Ids, progress, 2));
        }

        [Test]
        public void ReplayingAnEarlyLevel_NeverLowersTheUnlock()
        {
            var progress = Fresh();
            for (int i = 0; i < 3; i++) ProgressRules.RecordWin(Ids, progress, i);
            ProgressRules.RecordWin(Ids, progress, 0);
            Assert.AreEqual("w1_l004", progress.HighestUnlocked);
        }

        [Test]
        public void WinningTheLastLevel_KeepsItAsTheHighestUnlock()
        {
            var progress = Fresh();
            for (int i = 0; i < Ids.Length; i++) ProgressRules.RecordWin(Ids, progress, i);
            Assert.AreEqual("w1_l004", progress.HighestUnlocked);
        }

        [Test]
        public void StarsForAWin_MatchTheWinCard()
        {
            Assert.AreEqual(3, ProgressRules.StarsForWin(), "Always 3 for now (Muthu, #21).");
            Assert.AreEqual(UiFeel.WinStars, ProgressRules.StarsForWin());
        }

        [Test]
        public void RecordWin_IsOneSave()
        {
            var store = new FakeProgressStore();
            var progress = new ProgressService(store, _ => { });
            ProgressRules.RecordWin(Ids, progress, 1);
            Assert.AreEqual(1, store.SaveCount);
        }

        // ---- Level order ----

        [Test]
        public void LevelOrder_SortsByWorldThenIndex_AndSkipsOtherFiles()
        {
            var sorted = LevelOrder.Sort(new[]
            {
                "Assets/Levels/World2/w2_l001.json",
                "Assets/Levels/World1/w1_l015.json",
                "Assets/Levels/World1/w1_l002.json",
                "Assets/Levels/World1/notes.json",
                "Assets/Levels/World1/w1_l001.json.meta",
                "Assets/Levels/World1/w1_l010.json",
                "Assets/Levels/World1/w1_l001.json",
                "Assets/Levels/World1/pack-1.md",
            });
            CollectionAssert.AreEqual(new[]
            {
                "Assets/Levels/World1/w1_l001.json",
                "Assets/Levels/World1/w1_l002.json",
                "Assets/Levels/World1/w1_l010.json",
                "Assets/Levels/World1/w1_l015.json",
                "Assets/Levels/World2/w2_l001.json",
            }, sorted);
        }

        [Test]
        public void LevelOrder_ParsesWorldAndIndex()
        {
            Assert.IsTrue(LevelOrder.TryParse("w1_l015.json", out int world, out int index));
            Assert.AreEqual(1, world);
            Assert.AreEqual(15, index);
            Assert.IsFalse(LevelOrder.TryParse("w1_l000.json", out _, out _));
            Assert.IsFalse(LevelOrder.TryParse("w1-l001.json", out _, out _));
            Assert.IsFalse(LevelOrder.TryParse(null, out _, out _));
        }

        // ---- Generated level list ----

        [Test]
        public void LevelList_ListsEveryLevelFileOnDisk_InPlayOrder()
        {
            var list = AssetDatabase.LoadAssetAtPath<LevelList>("Assets/Levels/LevelList.asset");
            Assert.IsNotNull(list, "Run Bazaar Sort/Sync Level List.");
            var onDisk = LevelOrder.Sort(Directory.GetFiles("Assets/Levels", "*.json", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')));
            Assert.GreaterOrEqual(onDisk.Count, 14);
            var listed = list.Levels.Select(AssetDatabase.GetAssetPath).ToList();
            CollectionAssert.AreEqual(onDisk, listed, "Stale level list: run Bazaar Sort/Sync Level List.");
        }

        [Test]
        public void LevelList_NamesAreTheLevelIds()
        {
            // The save keys levels by asset name; it must equal the "id" inside the file.
            var list = AssetDatabase.LoadAssetAtPath<LevelList>("Assets/Levels/LevelList.asset");
            Assert.IsNotNull(list);
            foreach (var level in list.Levels)
            {
                var data = LevelParser.Parse(level.text, level.name);
                Assert.AreEqual(level.name, data.Id, AssetDatabase.GetAssetPath(level));
            }
        }

        [Test]
        public void GameScene_ReadsTheLevelList_WithSavedProgressOn()
        {
            string yaml = File.ReadAllText("Assets/Scenes/Game.unity");
            string guid = AssetDatabase.AssetPathToGUID("Assets/Levels/LevelList.asset");
            Assert.IsFalse(string.IsNullOrEmpty(guid));
            StringAssert.Contains("_levelList: {fileID: 11400000, guid: " + guid, yaml);
            StringAssert.Contains("_useSavedProgress: 1", yaml);
        }
    }
}
