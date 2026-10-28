using System.Collections.Generic;
using System.Linq;
using Game.Levels;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The canonical duplicate hash written to solver.hash (issue #20).</summary>
    public class LayoutHashTests
    {
        private static List<string> Row(string a, string b, string c) => new List<string> { a, b, c };

        private static ShelfData Shelf(params List<string>[] layers)
        {
            return new ShelfData { Kind = ShelfKinds.Normal, Layers = layers.ToList() };
        }

        private static LevelData Level(params ShelfData[] shelves)
        {
            return new LevelData { Id = "test", Shelves = shelves.ToList() };
        }

        // Four shelves, three types, one hidden layer.
        private static LevelData Original()
        {
            return Level(
                Shelf(Row("tumbler", "tawa", null), Row("idli_stand", "tumbler", "tawa")),
                Shelf(Row("tawa", "idli_stand", null)),
                Shelf(Row("tumbler", "idli_stand", "tawa")),
                Shelf(Row("idli_stand", null, null), Row("tumbler", null, null)));
        }

        [Test]
        public void Hash_IsSixteenLowercaseHexDigits()
        {
            StringAssert.IsMatch("^[0-9a-f]{16}$", LayoutHash.Compute(Original()));
        }

        [Test]
        public void Hash_IgnoresShelfOrder()
        {
            var reordered = Original();
            reordered.Shelves.Reverse();
            var shelf = reordered.Shelves[0];
            reordered.Shelves.RemoveAt(0);
            reordered.Shelves.Insert(2, shelf);

            Assert.AreEqual(LayoutHash.Compute(Original()), LayoutHash.Compute(reordered));
        }

        [Test]
        public void Hash_IgnoresMirroring()
        {
            var mirrored = Original();
            mirrored.Shelves.Reverse();
            foreach (var s in mirrored.Shelves)
            {
                foreach (var layer in s.Layers) layer.Reverse();
            }

            Assert.AreEqual(LayoutHash.Compute(Original()), LayoutHash.Compute(mirrored));
        }

        [Test]
        public void Hash_IgnoresRenamingAndSwappingTypes()
        {
            var renamed = Original();
            var map = new Dictionary<string, string>
            {
                { "tumbler", "tawa" }, { "tawa", "masala_dabba" }, { "idli_stand", "tumbler" },
            };
            foreach (var s in renamed.Shelves)
            {
                foreach (var layer in s.Layers)
                {
                    for (int i = 0; i < layer.Count; i++)
                    {
                        if (layer[i] != null) layer[i] = map[layer[i]];
                    }
                }
            }

            Assert.AreEqual(LayoutHash.Compute(Original()), LayoutHash.Compute(renamed));
        }

        [Test]
        public void Hash_SymmetricTypes_RenamingStillMatches()
        {
            // a and b play identical roles, so the refinement cannot tell them apart and both orders are tried.
            var level = Level(Shelf(Row("a", "b", null)), Shelf(Row("b", "a", null)), Shelf(Row("a", "b", null)));
            var swapped = Level(Shelf(Row("b", "a", null)), Shelf(Row("a", "b", null)), Shelf(Row("b", "a", null)));

            Assert.AreEqual(LayoutHash.Compute(level, out bool exact), LayoutHash.Compute(swapped));
            Assert.IsTrue(exact);
        }

        [Test]
        public void Hash_ChangesWithADifferentLayout()
        {
            var different = Original();
            // Swap one tumbler and one tawa between two shelves' front rows.
            different.Shelves[1].Layers[0][0] = "tumbler";
            different.Shelves[2].Layers[0][0] = "tawa";

            Assert.AreNotEqual(LayoutHash.Compute(Original()), LayoutHash.Compute(different));
        }

        [Test]
        public void Hash_ChangesWithADifferentHiddenLayer()
        {
            var different = Original();
            // Front rows unchanged; an idli stand and a tumbler swap between the hidden layers of shelves 0 and 3.
            different.Shelves[0].Layers[1][0] = "tumbler";
            different.Shelves[3].Layers[1][0] = "idli_stand";

            Assert.AreNotEqual(LayoutHash.Compute(Original()), LayoutHash.Compute(different));
        }

        [Test]
        public void Hash_ChangesWithLayerOrder()
        {
            var level = Level(Shelf(Row("a", "a", null), Row("b", "b", "b")), Shelf(Row("a", null, null)));
            var swapped = Level(Shelf(Row("b", "b", "b"), Row("a", "a", null)), Shelf(Row("a", null, null)));

            Assert.AreNotEqual(LayoutHash.Compute(level), LayoutHash.Compute(swapped));
        }

        [Test]
        public void Hash_ChangesWithShelfKindAndLockClears()
        {
            var locked = Original();
            locked.Shelves[2].Kind = ShelfKinds.Locked;
            locked.Shelves[2].LockClears = 2;
            var lockedThree = Original();
            lockedThree.Shelves[2].Kind = ShelfKinds.Locked;
            lockedThree.Shelves[2].LockClears = 3;

            string plain = LayoutHash.Compute(Original());
            Assert.AreNotEqual(plain, LayoutHash.Compute(locked));
            Assert.AreNotEqual(LayoutHash.Compute(locked), LayoutHash.Compute(lockedThree));
        }

        [Test]
        public void Hash_IsStableAcrossCalls()
        {
            Assert.AreEqual(LayoutHash.Compute(Original()), LayoutHash.Compute(Original()));
        }
    }
}
