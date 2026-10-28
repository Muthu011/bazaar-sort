using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class BoardTests
    {
        private const string A = "tumbler";
        private const string B = "dabba";
        private const string C = "idli_stand";
        private const string D = "tawa";

        private static string[] Row(string s0, string s1, string s2) => new[] { s0, s1, s2 };

        private static Shelf S(params string[][] layers) => new Shelf(layers);

        /// <summary>Records every board event as a short string, in order.</summary>
        private sealed class EventLog
        {
            public readonly List<string> Entries = new List<string>();

            public EventLog(Board board)
            {
                board.Moved += m => Entries.Add($"Moved {m.ItemId} {m.FromShelf}:{m.FromSlot}->{m.ToShelf}:{m.ToSlot}");
                board.Cleared += c => Entries.Add($"Cleared {c.ShelfIndex} {c.ItemId}");
                board.LayerRevealed += r => Entries.Add($"Revealed {r.ShelfIndex} left={r.HiddenLayersRemaining}");
                board.Won += () => Entries.Add("Won");
                board.Stuck += () => Entries.Add("Stuck");
            }

            public int Count(string prefix)
            {
                int n = 0;
                foreach (var e in Entries)
                {
                    if (e.StartsWith(prefix, StringComparison.Ordinal)) n++;
                }
                return n;
            }
        }

        // ---------- Construction ----------

        [Test]
        public void Construct_CountsItemsInAllLayers()
        {
            var board = new Board(
                S(Row(A, A, null), Row(B, B, B)),
                S(Row(A, null, null)));
            Assert.AreEqual(2, board.ShelfCount);
            Assert.AreEqual(6, board.ItemCount);
            Assert.IsFalse(board.IsWon);
        }

        [Test]
        public void FromLayers_BuildsBoardFromPlainLevelData()
        {
            string[][][] data =
            {
                new[] { Row(A, A, null), Row(B, B, B) },
                new[] { Row(A, null, null) },
            };
            var board = Board.FromLayers(data);
            Assert.AreEqual(2, board.ShelfCount);
            Assert.AreEqual(A, board.GetShelf(0).GetFrontItem(0));
            Assert.AreEqual(1, board.GetShelf(0).HiddenLayerCount);
            Assert.AreEqual(6, board.ItemCount);
        }

        [Test]
        public void Construct_RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => new Board((IEnumerable<Shelf>)null));
            Assert.Throws<ArgumentException>(() => new Board(new Shelf[0]));
            Assert.Throws<ArgumentException>(() => new Board(S(Row(A, null, null)), null));
            var shared = S(Row(A, null, null));
            Assert.Throws<ArgumentException>(() => new Board(shared, shared));
        }

        [Test]
        public void Construct_EmptyFrontWithHiddenLayer_RevealsSilently()
        {
            var shelf = S(Row(null, null, null), Row(null, null, null), Row(B, B, null));
            var board = new Board(shelf, S(Row(B, null, null)));
            Assert.AreEqual(B, board.GetShelf(0).GetFrontItem(0));
            Assert.AreEqual(0, board.GetShelf(0).HiddenLayerCount);
        }

        [Test]
        public void Construct_EmptyBoard_IsWonAndNotStuck()
        {
            var board = new Board(S(Row(null, null, null)));
            Assert.IsTrue(board.IsWon);
            Assert.IsFalse(board.IsStuck);
        }

        [Test]
        public void GetShelf_RejectsOutOfRange()
        {
            var board = new Board(S(Row(A, null, null)));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.GetShelf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => board.GetShelf(1));
        }

        // ---------- Legal moves ----------

        [Test]
        public void TryMove_Legal_MovesItemToLeftmostFreeSlot()
        {
            var board = new Board(
                S(Row(A, B, null)),
                S(Row(null, C, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(0, 1, 1, out var result));
            Assert.AreEqual(MoveResult.Ok, result);
            Assert.IsNull(board.GetShelf(0).GetFrontItem(1));
            Assert.AreEqual(B, board.GetShelf(1).GetFrontItem(0));
            Assert.AreEqual(C, board.GetShelf(1).GetFrontItem(1));
            CollectionAssert.AreEqual(new[] { $"Moved {B} 0:1->1:0" }, log.Entries);
        }

        [Test]
        public void TryMove_LeftmostFreeSlot_SkipsFilledSlots()
        {
            var board = new Board(
                S(Row(A, null, null)),
                S(Row(B, C, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            Assert.AreEqual(A, board.GetShelf(1).GetFrontItem(2));
            Assert.AreEqual($"Moved {A} 0:0->1:2", log.Entries[0]);
        }

        // ---------- Illegal moves ----------

        [Test]
        public void TryMove_ToFullShelf_IsRejected()
        {
            var board = new Board(
                S(Row(A, null, null)),
                S(Row(B, C, D)));
            var log = new EventLog(board);

            Assert.IsFalse(board.TryMove(0, 0, 1, out var result));
            Assert.AreEqual(MoveResult.TargetFull, result);
            Assert.AreEqual(A, board.GetShelf(0).GetFrontItem(0));
            Assert.IsEmpty(log.Entries);
        }

        [Test]
        public void TryMove_FromEmptySlot_IsRejected()
        {
            var board = new Board(
                S(Row(A, null, null)),
                S(Row(null, null, null)));
            var log = new EventLog(board);

            Assert.IsFalse(board.TryMove(0, 1, 1, out var result));
            Assert.AreEqual(MoveResult.EmptySlot, result);
            Assert.IsEmpty(log.Entries);
        }

        [Test]
        public void TryMove_ToSameShelf_IsRejected()
        {
            var board = new Board(
                S(Row(A, null, null)),
                S(Row(null, null, null)));
            var log = new EventLog(board);

            Assert.IsFalse(board.TryMove(0, 0, 0, out var result));
            Assert.AreEqual(MoveResult.SameShelf, result);
            Assert.AreEqual(A, board.GetShelf(0).GetFrontItem(0));
            Assert.IsEmpty(log.Entries);
        }

        [TestCase(-1, 0, 1, MoveResult.InvalidShelf)]
        [TestCase(2, 0, 1, MoveResult.InvalidShelf)]
        [TestCase(0, 0, -1, MoveResult.InvalidShelf)]
        [TestCase(0, 0, 2, MoveResult.InvalidShelf)]
        [TestCase(0, -1, 1, MoveResult.InvalidSlot)]
        [TestCase(0, 3, 1, MoveResult.InvalidSlot)]
        public void TryMove_OutOfRangeIndices_AreRejectedWithoutThrowing(int from, int slot, int to, MoveResult expected)
        {
            var board = new Board(
                S(Row(A, null, null)),
                S(Row(null, null, null)));
            var log = new EventLog(board);

            Assert.IsFalse(board.TryMove(from, slot, to, out var result));
            Assert.AreEqual(expected, result);
            Assert.AreEqual(expected, board.CanMove(from, slot, to));
            Assert.IsEmpty(log.Entries);
        }

        [Test]
        public void CanMove_DoesNotChangeBoard()
        {
            var board = new Board(
                S(Row(A, null, null)),
                S(Row(null, null, null)));
            var log = new EventLog(board);

            Assert.AreEqual(MoveResult.Ok, board.CanMove(0, 0, 1));
            Assert.AreEqual(A, board.GetShelf(0).GetFrontItem(0));
            Assert.IsEmpty(log.Entries);
        }

        // ---------- Triple clear ----------

        [Test]
        public void MoveCompletingTriple_ClearsFrontRow()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(A, B, null)),
                S(Row(B, B, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                Assert.IsNull(board.GetShelf(0).GetFrontItem(slot));
            }
            Assert.AreEqual(3, board.ItemCount);
            CollectionAssert.AreEqual(new[] { $"Moved {A} 1:0->0:2", $"Cleared 0 {A}" }, log.Entries);
        }

        [Test]
        public void ThreeDifferentItems_DoNotClear()
        {
            var board = new Board(
                S(Row(A, B, null)),
                S(Row(C, null, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.AreEqual(0, log.Count("Cleared"));
            Assert.AreEqual(3, board.GetShelf(0).FrontItemCount);
        }

        [Test]
        public void TwoIdenticalItems_DoNotClear()
        {
            var board = new Board(
                S(Row(A, null, null)),
                S(Row(A, B, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.AreEqual(0, log.Count("Cleared"));
            Assert.AreEqual(A, board.GetShelf(0).GetFrontItem(1));
        }

        [Test]
        public void ItemIds_AreCaseSensitive()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(A.ToUpperInvariant(), B, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.AreEqual(0, log.Count("Cleared"));
        }

        // ---------- Layer reveal ----------

        [Test]
        public void ClearWithLayerBehind_RevealsNextLayer()
        {
            var board = new Board(
                S(Row(A, A, null), Row(B, C, null), Row(D, null, null)),
                S(Row(A, B, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            var shelf = board.GetShelf(0);
            Assert.AreEqual(B, shelf.GetFrontItem(0));
            Assert.AreEqual(C, shelf.GetFrontItem(1));
            Assert.IsNull(shelf.GetFrontItem(2));
            Assert.AreEqual(1, shelf.HiddenLayerCount);
            Assert.AreEqual(D, shelf.GetHiddenItem(0, 0));
            CollectionAssert.AreEqual(
                new[] { $"Moved {A} 1:0->0:2", $"Cleared 0 {A}", "Revealed 0 left=1" },
                log.Entries);
        }

        [Test]
        public void PartialFrontRow_DoesNotRevealLayer()
        {
            var board = new Board(
                S(Row(A, B, null), Row(C, C, C)),
                S(Row(null, null, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            Assert.AreEqual(0, log.Count("Revealed"));
            Assert.AreEqual(1, board.GetShelf(0).HiddenLayerCount);
        }

        [Test]
        public void MovingLastFrontItemAway_RevealsNextLayer()
        {
            var board = new Board(
                S(Row(A, null, null), Row(B, B, null)),
                S(Row(B, null, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            Assert.AreEqual(B, board.GetShelf(0).GetFrontItem(0));
            Assert.AreEqual(B, board.GetShelf(0).GetFrontItem(1));
            Assert.AreEqual(0, board.GetShelf(0).HiddenLayerCount);
            CollectionAssert.AreEqual(
                new[] { $"Moved {A} 0:0->1:1", "Revealed 0 left=0" },
                log.Entries);
        }

        [Test]
        public void RevealedLayerThatIsATriple_ClearsImmediately_AndRevealsNext()
        {
            var board = new Board(
                S(Row(A, A, null), Row(B, B, B), Row(C, null, null)),
                S(Row(A, C, C)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            CollectionAssert.AreEqual(
                new[]
                {
                    $"Moved {A} 1:0->0:2",
                    $"Cleared 0 {A}",
                    "Revealed 0 left=1",
                    $"Cleared 0 {B}",
                    "Revealed 0 left=0",
                },
                log.Entries);
            Assert.AreEqual(C, board.GetShelf(0).GetFrontItem(0));
            Assert.AreEqual(3, board.ItemCount);
        }

        [Test]
        public void EmptyHiddenLayer_IsSkippedToNextLayer()
        {
            var board = new Board(
                S(Row(A, A, null), Row(null, null, null), Row(B, null, null)),
                S(Row(A, B, B)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.AreEqual(B, board.GetShelf(0).GetFrontItem(0));
            Assert.AreEqual(0, board.GetShelf(0).HiddenLayerCount);
            Assert.AreEqual(2, log.Count("Revealed"));
        }

        [Test]
        public void ClearWithNoLayersLeft_LeavesEmptyFront()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(A, B, null)),
                S(Row(B, B, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.IsTrue(board.GetShelf(0).IsFrontEmpty);
            Assert.AreEqual(0, board.GetShelf(0).HiddenLayerCount);
            Assert.AreEqual(0, log.Count("Revealed"));
            Assert.IsTrue(board.GetShelf(0).HasFreeFrontSlot);
        }

        [Test]
        public void ClearedShelf_CanReceiveItemsAgain()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(A, B, null)),
                S(Row(B, B, null)));

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.IsTrue(board.TryMove(1, 1, 0));
            Assert.AreEqual(B, board.GetShelf(0).GetFrontItem(0));
        }

        // ---------- Win ----------

        [Test]
        public void ClearingLastItems_RaisesWonOnce_AndNotStuck()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(A, null, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.IsTrue(board.IsWon);
            Assert.IsFalse(board.IsStuck);
            Assert.AreEqual(0, board.ItemCount);
            Assert.AreEqual("Won", log.Entries[log.Entries.Count - 1]);
            Assert.AreEqual(1, log.Count("Won"));
            Assert.AreEqual(0, log.Count("Stuck"));
        }

        [Test]
        public void AfterWin_MovesAreRejectedAsGameOver_AndWonDoesNotRepeat()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(A, null, null)));
            var log = new EventLog(board);
            board.TryMove(1, 0, 0);

            Assert.IsFalse(board.TryMove(0, 0, 1, out var result));
            Assert.AreEqual(MoveResult.GameOver, result);
            Assert.AreEqual(1, log.Count("Won"));
        }

        [Test]
        public void WonIsRaisedAfterFinalClearAndReveals_NotMidMove()
        {
            // Final move clears shelf 0, which reveals a triple that clears too; only then is the board empty.
            var board = new Board(
                S(Row(A, A, null), Row(B, B, B)),
                S(Row(A, null, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            CollectionAssert.AreEqual(
                new[] { $"Moved {A} 1:0->0:2", $"Cleared 0 {A}", "Revealed 0 left=0", $"Cleared 0 {B}", "Won" },
                log.Entries);
        }

        [Test]
        public void ItemsLeftInHiddenLayers_IsNotWon()
        {
            var board = new Board(
                S(Row(A, A, null), Row(B, null, null)),
                S(Row(A, B, B)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.IsFalse(board.IsWon);
            Assert.AreEqual(0, log.Count("Won"));
        }

        // ---------- Stuck ----------

        [Test]
        public void MoveAndRevealThatFillEveryShelf_RaisesStuckOnce_AfterSettle()
        {
            // A moves into shelf 1's last free slot; shelf 0's front empties and reveals a full layer.
            // Every shelf is now full and nothing cleared, so no legal move remains.
            var board = new Board(
                S(Row(A, null, null), Row(B, C, D)),
                S(Row(B, C, null)),
                S(Row(D, A, B)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            Assert.IsTrue(board.IsStuck);
            Assert.IsFalse(board.IsWon);
            CollectionAssert.AreEqual(
                new[] { $"Moved {A} 0:0->1:2", "Revealed 0 left=0", "Stuck" },
                log.Entries);
        }

        [Test]
        public void MoveThatFillsLastFreeSlotButClears_IsNotStuck()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(B, C, A)),
                S(Row(B, B, C)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 2, 0));
            Assert.IsFalse(board.IsStuck);
            Assert.AreEqual(0, log.Count("Stuck"));
        }

        [Test]
        public void StuckIsNotRaisedMidMove_WhenTargetClearFreesSpace()
        {
            // Mid-move, after shelf 0 reveals a full layer and before shelf 1 clears, every shelf is full.
            // Stuck must only be judged after the whole move settles, by which time shelf 1 has cleared.
            var board = new Board(
                S(Row(C, null, null), Row(D, B, D)),
                S(Row(C, C, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            CollectionAssert.AreEqual(
                new[] { $"Moved {C} 0:0->1:2", "Revealed 0 left=0", $"Cleared 1 {C}" },
                log.Entries);
            Assert.IsFalse(board.IsStuck);
            Assert.AreEqual(0, log.Count("Stuck"));
        }

        [Test]
        public void StuckWhenRevealFillsTheOnlyFreeShelf()
        {
            // Shelf 0's last item leaves, shelf 1 becomes full, and shelf 0 reveals a full layer: no free slot left.
            var board = new Board(
                S(Row(C, null, null), Row(D, B, A)),
                S(Row(A, B, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            Assert.IsTrue(board.IsStuck);
            CollectionAssert.AreEqual(
                new[] { $"Moved {C} 0:0->1:2", "Revealed 0 left=0", "Stuck" },
                log.Entries);
        }

        [Test]
        public void SingleShelfWithFreeSlot_IsStuck_BecauseSameShelfMovesAreIllegal()
        {
            var board = new Board(S(Row(A, B, null)));
            Assert.IsFalse(board.HasLegalMove());
            Assert.IsTrue(board.IsStuck);
        }

        [Test]
        public void StuckBoard_RejectsEveryMove()
        {
            var board = new Board(
                S(Row(A, B, C)),
                S(Row(D, A, B)));
            Assert.IsTrue(board.IsStuck);
            for (int from = 0; from < board.ShelfCount; from++)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    for (int to = 0; to < board.ShelfCount; to++)
                    {
                        Assert.IsFalse(board.TryMove(from, slot, to));
                    }
                }
            }
        }

        [Test]
        public void ConstructionDoesNotRaiseEvents_EvenIfInitiallyStuck()
        {
            var board = new Board(S(Row(A, B, C)), S(Row(D, A, B)));
            var log = new EventLog(board);
            Assert.IsTrue(board.IsStuck);
            Assert.IsEmpty(log.Entries);
        }

        // ---------- Full playthrough ----------

        [Test]
        public void SmallLevel_PlaysThroughToWin()
        {
            var board = new Board(
                S(Row(A, B, null), Row(C, C, C)),
                S(Row(A, B, null)),
                S(Row(A, B, null)));
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(1, 0, 0)); // shelf 0: A B A
            Assert.IsTrue(board.TryMove(0, 1, 1)); // shelf 0: A _ A, shelf 1: B B _
            Assert.IsTrue(board.TryMove(2, 1, 1)); // shelf 1: B B B -> clear
            Assert.IsTrue(board.TryMove(2, 0, 0)); // shelf 0: A A A -> clear, reveal C C C -> clear
            Assert.IsTrue(board.IsWon);
            Assert.AreEqual(3, log.Count("Cleared"));
            Assert.AreEqual(1, log.Count("Revealed"));
            Assert.AreEqual(1, log.Count("Won"));
            Assert.AreEqual(0, log.Count("Stuck"));
        }
    }
}
