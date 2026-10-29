using System;
using System.Collections.Generic;
using Game.Core;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Issue #31: the visited-state key of the searches includes every lock counter and is canonical: it does not change
    /// when shelves are reordered or item types are renamed.
    /// </summary>
    public class StateKeyTests
    {
        private static string[] Row(string a, string b, string c) => new[] { a, b, c };

        // Shelf layers and lockClears for a small board with two locked shelves and hidden layers.
        private static readonly string[][][] Layers =
        {
            new[] { Row("a", "b", null), Row("c", "c", "d") },
            new[] { Row("b", "a", "e") },
            new[] { Row(null, null, null) },
            new[] { Row("d", "e", "b"), Row("a", "e", "d") },
            new[] { Row("c", "f", "f") },
            new[] { Row("f", null, null) },
        };

        private static readonly int[] Locks = { 0, 0, 0, 2, 1, 0 };

        private static Board Build(string[][][] layers, int[] locks) => SyntheticLevels.Build(layers, locks);

        private static StateKey KeyOf(Board board) => new SearchContext(board, null).Key(board);

        private static string[][][] Rename(string[][][] layers, Func<string, string> rename)
        {
            var result = new string[layers.Length][][];
            for (int i = 0; i < layers.Length; i++)
            {
                result[i] = new string[layers[i].Length][];
                for (int l = 0; l < layers[i].Length; l++)
                {
                    result[i][l] = new string[3];
                    for (int s = 0; s < 3; s++) result[i][l][s] = layers[i][l][s] == null ? null : rename(layers[i][l][s]);
                }
            }
            return result;
        }

        private static T[] Permute<T>(T[] items, int[] order)
        {
            var result = new T[items.Length];
            for (int i = 0; i < order.Length; i++) result[i] = items[order[i]];
            return result;
        }

        [Test]
        public void Key_LockCountersChangeTheKey()
        {
            var locks = (int[])Locks.Clone();
            var baseKey = KeyOf(Build(Layers, locks));

            locks[3] = 3; // the same items, but shelf 3 needs one clear more
            Assert.AreNotEqual(baseKey, KeyOf(Build(Layers, locks)));

            locks[3] = 0; // shelf 3 already open
            Assert.AreNotEqual(baseKey, KeyOf(Build(Layers, locks)));
        }

        [Test]
        public void Key_LockCountersChangeTheKey_WithinOneSearch()
        {
            var board = Build(Layers, Locks);
            var context = new SearchContext(board, null);
            var locks = (int[])Locks.Clone();
            locks[4] = 2;

            Assert.AreNotEqual(context.Key(board), context.Key(Build(Layers, locks)));
            Assert.AreNotEqual(context.CheapKey(board), context.CheapKey(Build(Layers, locks)));
        }

        [Test]
        public void Key_ShelfOrderDoesNotChangeTheKey_LockedShelvesIncluded()
        {
            var baseKey = KeyOf(Build(Layers, Locks));
            int[] order = { 4, 2, 0, 5, 3, 1 };

            Assert.AreEqual(baseKey, KeyOf(Build(Permute(Layers, order), Permute(Locks, order))));
        }

        [Test]
        public void Key_TypeRenamingDoesNotChangeTheKey()
        {
            var baseKey = KeyOf(Build(Layers, Locks));
            var names = new Dictionary<string, string>
            {
                { "a", "f" }, { "b", "d" }, { "c", "a" }, { "d", "e" }, { "e", "b" }, { "f", "c" },
            };

            Assert.AreEqual(baseKey, KeyOf(Build(Rename(Layers, n => names[n]), Locks)));
            Assert.AreEqual(baseKey, KeyOf(Build(Rename(Layers, n => "item_" + n), Locks)));
        }

        [Test]
        public void Key_RenamingAndReorderingTogether_DoNotChangeTheKey()
        {
            var baseKey = KeyOf(Build(Layers, Locks));
            int[] order = { 5, 4, 3, 2, 1, 0 };
            var renamed = Rename(Layers, n => ((char)('z' - (n[0] - 'a'))).ToString());

            Assert.AreEqual(baseKey, KeyOf(Build(Permute(renamed, order), Permute(Locks, order))));
        }

        [Test]
        public void Key_DifferentPuzzlesGetDifferentKeys()
        {
            var baseKey = KeyOf(Build(Layers, Locks));

            // Swap one "a" with one "c" between a front row and a hidden layer: same items, different puzzle.
            var changed = Rename(Layers, n => n);
            changed[0][0][0] = "c";
            changed[0][1][0] = "a";
            Assert.AreNotEqual(baseKey, KeyOf(Build(changed, Locks)));

            // Swap the c in shelf 4's front row with the e in shelf 1's: still every type three times.
            var swapped = Rename(Layers, n => n);
            swapped[1][0][2] = "c";
            swapped[4][0][0] = "e";
            Assert.AreNotEqual(baseKey, KeyOf(Build(swapped, Locks)));

            // Front row and hidden layer of shelf 3 swapped: this one is the same puzzle with a and b renamed, so the
            // canonical key must treat it as equal.
            var layersSwapped = Rename(Layers, n => n);
            (layersSwapped[3][0], layersSwapped[3][1]) = (layersSwapped[3][1], layersSwapped[3][0]);
            Assert.AreEqual(baseKey, KeyOf(Build(layersSwapped, Locks)));
        }

        [Test]
        public void Key_SymmetricTypesAreMergedWithinOneSearch()
        {
            // a and b play the same role: moving an a or a b onto the empty shelf gives the same puzzle.
            var board = SyntheticLevels.Build(new[]
            {
                new[] { Row("a", "b", "c") },
                new[] { Row("b", "a", "c") },
                new[] { Row("a", "b", "c") },
                new[] { Row(null, null, null) },
            });
            var context = new SearchContext(board, null);
            var withA = SearchContext.Apply(context.Root, new SolverMove(0, 0, 3));
            var withB = SearchContext.Apply(context.Root, new SolverMove(0, 1, 3));

            Assert.AreNotEqual(context.CheapKey(withA), context.CheapKey(withB));
            Assert.AreEqual(context.Key(withA), context.Key(withB));
        }

        [Test]
        public void Key_CachedAndUncachedKeysAgree()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(10, 12, 3, 5), new[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1 });
            var context = new SearchContext(board, null);
            var rng = new Random(7);
            var moves = new List<SolverMove>();
            var current = context.Root;
            for (int step = 0; step < 200 && !current.IsWon; step++)
            {
                Assert.AreEqual(context.UncachedKey(current), context.Key(current));
                Assert.AreEqual(context.UncachedKey(current), context.Key(current)); // second call hits the cache
                context.GenerateMoves(current, moves, false);
                if (moves.Count == 0) break;
                current = SearchContext.Apply(current, moves[rng.Next(moves.Count)]);
            }
        }

        [Test]
        public void PlayKey_KeepsTheSimulatedPlayersNotionOfTheSameBoard()
        {
            // The play policy ("not seen earlier in this play") is defined on this key: normal shelves in any order are
            // the same board, a locked shelf keeps its place, and type names count.
            var board = Build(Layers, Locks);
            var context = new SearchContext(board, null);
            int[] swapNormal = { 0, 1, 5, 3, 4, 2 }; // two normal shelves with no hidden layers

            int[] moveLocked = { 0, 1, 2, 4, 3, 5 };

            Assert.AreEqual(context.PlayKey(board),
                context.PlayKey(Build(Permute(Layers, swapNormal), Permute(Locks, swapNormal))));
            Assert.AreNotEqual(PlayKeyOf(board),
                PlayKeyOf(Build(Permute(Layers, moveLocked), Permute(Locks, moveLocked))));
        }

        private static StateKey PlayKeyOf(Board board) => new SearchContext(board, null).PlayKey(board);
    }
}
