using System.IO;
using System.Linq;
using Game.Levels;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// World 1, pack 2 (levels 6 to 15, issues #23 and #32): the sawtooth roles, the locked shelf arriving at L11 and
    /// being used in every later level, and the pack rule that it appears in at least 4 levels after L11 (L12 to L15).
    /// Solvability, win-rate bands and uniqueness are covered by LevelPackTests.
    /// </summary>
    public class World1Pack2Tests
    {
        private const string Dir = "Assets/Levels/World1";

        private static readonly string[] Roles =
        {
            "breather", "medium", "hard", "hard", "boss", // L6 to L10
            "easy", "easy", "medium", "medium", "hard",    // L11 to L15
        };

        private static LevelLoadResult Load(int n)
        {
            string path = $"{Dir}/w1_l{n:D3}.json";
            return LevelLoader.Load(File.ReadAllText(path), Path.GetFileName(path));
        }

        private static int Locks(LevelData level) => level.Shelves.Count(s => s.Kind == "locked");

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        public void Level_LoadsAndStaysWithinEightShelves(int n)
        {
            var result = Load(n);
            CollectionAssert.IsEmpty(result.Errors.Select(e => e.Message));
            Assert.IsTrue(result.Success);
            Assert.AreEqual($"w1_l{n:D3}", result.Level.Id);
            Assert.AreEqual(n, result.Level.Index);
            Assert.LessOrEqual(result.Level.Shelves.Count, 8);
        }

        [Test]
        public void Roles_FollowTheSawtoothFromL6ToL15()
        {
            for (int n = 6; n <= 15; n++)
            {
                Assert.AreEqual(Roles[n - 6], Load(n).Level.Role, $"w1_l{n:D3}");
            }
        }

        [Test]
        public void LockedShelf_FirstAppearsAtL11()
        {
            for (int n = 1; n <= 10; n++)
            {
                Assert.AreEqual(0, Locks(Load(n).Level), $"w1_l{n:D3} must not have a lock");
            }
            Assert.GreaterOrEqual(Locks(Load(11).Level), 1);
        }

        [Test]
        public void LockedShelf_IsUsedInEveryLevelAfterL11()
        {
            for (int n = 12; n <= 15; n++)
            {
                Assert.GreaterOrEqual(Locks(Load(n).Level), 1, $"w1_l{n:D3} should use a locked shelf");
            }
        }

        [Test]
        public void PackRule_LockedShelfIsUsedInAtLeastFourLevelsAfterL11()
        {
            int levelsWithLock = Enumerable.Range(12, 4).Count(n => Locks(Load(n).Level) >= 1);
            Assert.GreaterOrEqual(levelsWithLock, 4, "locked shelf should appear in L12 to L15");
        }

        [Test]
        public void Boss_L10_IsTheBiggestOfTheFirstTen()
        {
            int Items(int n) => Load(n).Board.ItemCount;
            for (int n = 1; n <= 9; n++)
            {
                Assert.Greater(Items(10), Items(n), $"L10 should have more items than L{n}");
            }
        }
    }
}
