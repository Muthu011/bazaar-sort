using System.IO;
using System.Linq;
using Game.Core;
using Game.Levels;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// World 1, pack 1 (levels 1 to 5). There is no solver yet, so each level is proven winnable by replaying a
    /// recorded winning line through <see cref="Board.TryMove"/>. Moves are (from shelf, from slot, to shelf), all
    /// 0-based; the item lands in the target's leftmost free front slot. The same lines are listed in
    /// Assets/Levels/World1/pack-1.md. Replace this with the solver check once it exists.
    /// </summary>
    public class World1PackTests
    {
        private const string Dir = "Assets/Levels/World1";

        private static readonly int[][][] Lines =
        {
            // L1: 3 moves
            new[] { new[] { 1, 1, 0 }, new[] { 2, 2, 1 }, new[] { 3, 0, 2 } },
            // L2: 3 moves
            new[] { new[] { 1, 1, 0 }, new[] { 0, 1, 1 }, new[] { 2, 0, 0 } },
            // L3: 8 moves
            new[]
            {
                new[] { 0, 0, 1 }, new[] { 2, 0, 3 }, new[] { 0, 1, 2 }, new[] { 0, 0, 3 },
                new[] { 1, 2, 0 }, new[] { 0, 1, 1 }, new[] { 2, 1, 0 }, new[] { 3, 0, 2 },
            },
            // L4: 13 moves
            new[]
            {
                new[] { 0, 1, 1 }, new[] { 0, 2, 3 }, new[] { 2, 0, 3 }, new[] { 0, 0, 2 }, new[] { 0, 1, 3 },
                new[] { 0, 2, 3 }, new[] { 1, 0, 0 }, new[] { 3, 1, 1 }, new[] { 0, 0, 3 }, new[] { 4, 1, 3 },
                new[] { 1, 0, 0 }, new[] { 2, 2, 1 }, new[] { 0, 0, 2 },
            },
            // L5: 18 moves
            new[]
            {
                new[] { 3, 1, 5 }, new[] { 0, 0, 1 }, new[] { 4, 1, 0 }, new[] { 0, 0, 1 }, new[] { 0, 1, 2 },
                new[] { 1, 2, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 0 }, new[] { 3, 0, 1 }, new[] { 0, 0, 2 },
                new[] { 1, 0, 0 }, new[] { 0, 0, 2 }, new[] { 1, 1, 0 }, new[] { 0, 0, 1 }, new[] { 1, 0, 2 },
                new[] { 4, 0, 1 }, new[] { 2, 1, 1 }, new[] { 3, 2, 2 },
            },
        };

        private static LevelLoadResult Load(int n)
        {
            string path = $"{Dir}/w1_l{n:D3}.json";
            return LevelLoader.Load(File.ReadAllText(path), Path.GetFileName(path));
        }

        private static int HiddenLayers(LevelData level) => level.Shelves.Sum(s => s.Layers.Count - 1);

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Level_LoadsWithoutErrors(int n)
        {
            var result = Load(n);
            CollectionAssert.IsEmpty(result.Errors.Select(e => e.Message));
            Assert.IsTrue(result.Success);
            Assert.AreEqual($"w1_l{n:D3}", result.Level.Id);
            Assert.AreEqual(n, result.Level.Index);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Level_RecordedLineWins(int n)
        {
            var board = Load(n).Board;
            var line = Lines[n - 1];
            for (int i = 0; i < line.Length; i++)
            {
                Assert.IsFalse(board.IsWon, $"won early, before move {i + 1}");
                Assert.IsTrue(board.TryMove(line[i][0], line[i][1], line[i][2], out var result),
                    $"move {i + 1} ({line[i][0]},{line[i][1]},{line[i][2]}) rejected: {result}");
            }
            Assert.IsTrue(board.IsWon);
            Assert.AreEqual(0, board.ItemCount);
        }

        [Test]
        public void Progression_L1TeachesMatch3WithNoHiddenLayers()
        {
            var level = Load(1).Level;
            Assert.AreEqual("match3", level.Tutorial);
            Assert.AreEqual(0, HiddenLayers(level));
        }

        [Test]
        public void Progression_L2AddsExactlyOneHiddenLayer()
        {
            var level = Load(2).Level;
            Assert.AreEqual("layers", level.Tutorial);
            Assert.AreEqual(1, HiddenLayers(level));
        }

        [Test]
        public void Progression_L3ToL5GrowFromFourToSixShelves()
        {
            Assert.AreEqual(4, Load(3).Board.ShelfCount);
            Assert.AreEqual(5, Load(4).Board.ShelfCount);
            Assert.AreEqual(6, Load(5).Board.ShelfCount);
        }

        [Test]
        public void Levels_StayWithinEightShelvesAndAreDistinct()
        {
            var layouts = new System.Collections.Generic.HashSet<string>();
            for (int n = 1; n <= 5; n++)
            {
                var level = Load(n).Level;
                Assert.LessOrEqual(level.Shelves.Count, 8);
                Assert.IsTrue(layouts.Add(string.Join("|", level.Shelves.Select(s =>
                    string.Join("/", s.Layers.Select(l => string.Join(",", l.Select(x => x ?? "-"))))))));
            }
        }
    }
}
