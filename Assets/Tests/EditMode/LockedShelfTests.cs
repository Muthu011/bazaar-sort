using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Locked shelves (issue #19): no moves in or out until the board has made lockClears triple clears.</summary>
    public class LockedShelfTests
    {
        private const string A = "tumbler";
        private const string B = "tawa";
        private const string C = "idli_stand";
        private const string D = "masala_dabba";

        private static string[] Row(string s0, string s1, string s2) => new[] { s0, s1, s2 };

        private static Shelf S(params string[][] layers) => new Shelf(layers);

        private static Shelf L(int lockClears, params string[][] layers) => Shelf.Locked(lockClears, layers);

        private sealed class EventLog
        {
            public readonly List<string> Entries = new List<string>();

            public EventLog(Board board)
            {
                board.Moved += m => Entries.Add($"Moved {m.FromShelf}->{m.ToShelf}");
                board.Cleared += c => Entries.Add($"Cleared {c.ShelfIndex} {c.ItemId}");
                board.LayerRevealed += r => Entries.Add($"Revealed {r.ShelfIndex}");
                board.ShelfUnlocked += i => Entries.Add($"Unlocked {i}");
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

        // ---------- Shelf ----------

        [Test]
        public void Shelf_NormalShelf_IsNotLocked()
        {
            var shelf = S(Row(A, null, null));
            Assert.IsFalse(shelf.IsLocked);
            Assert.AreEqual(0, shelf.LockClears);
            Assert.AreEqual(0, shelf.LockClearsLeft);
        }

        [Test]
        public void Shelf_Locked_StartsLockedWithFullCount()
        {
            var shelf = L(3, Row(A, B, null));
            Assert.IsTrue(shelf.IsLocked);
            Assert.AreEqual(3, shelf.LockClears);
            Assert.AreEqual(3, shelf.LockClearsLeft);
        }

        [Test]
        public void Shelf_LockedWithZeroOrNegativeCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => L(0, Row(A, null, null)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Shelf(new IReadOnlyList<string>[] { Row(A, null, null) }, "s1", -1));
        }

        // ---------- Moves in and out ----------

        private static Board LockedMiddle(int lockClears) => new Board(
            S(Row(A, A, null)),
            L(lockClears, Row(B, C, null)),
            S(Row(A, D, null)));

        [Test]
        public void Locked_ItemsCannotBePickedOrMovedOut()
        {
            var board = LockedMiddle(1);
            Assert.AreEqual(MoveResult.SourceLocked, board.CanPick(1, 0));
            Assert.AreEqual(MoveResult.SourceLocked, board.CanMove(1, 0, 0));
            Assert.IsFalse(board.TryMove(1, 0, 0, out var result));
            Assert.AreEqual(MoveResult.SourceLocked, result);
            Assert.AreEqual(B, board.GetShelf(1).GetFrontItem(0), "Items stay where they are.");
            Assert.AreEqual(2, board.GetShelf(0).FrontItemCount);
        }

        [Test]
        public void Locked_NothingCanBeDroppedOnIt()
        {
            var board = LockedMiddle(1);
            Assert.IsTrue(board.GetShelf(1).HasFreeFrontSlot, "The locked shelf has room; the lock alone refuses.");
            Assert.AreEqual(MoveResult.TargetLocked, board.CanMove(2, 1, 1));
            Assert.IsFalse(board.TryMove(2, 1, 1, out var result));
            Assert.AreEqual(MoveResult.TargetLocked, result);
            Assert.IsNull(board.GetShelf(1).GetFrontItem(2));
            Assert.AreEqual(D, board.GetShelf(2).GetFrontItem(1));
        }

        [Test]
        public void Locked_ItemsStayVisibleInTheFrontRow()
        {
            var board = LockedMiddle(1);
            var shelf = board.GetShelf(1);
            Assert.AreEqual(B, shelf.GetFrontItem(0));
            Assert.AreEqual(C, shelf.GetFrontItem(1));
        }

        [Test]
        public void CanMove_SourceLockedComesBeforeSameShelfAndTargetChecks()
        {
            var board = new Board(L(1, Row(A, B, null)), L(1, Row(C, D, A)), S(Row(B, null, null)));
            Assert.AreEqual(MoveResult.SourceLocked, board.CanMove(0, 0, 0), "Own shelf, but locked");
            Assert.AreEqual(MoveResult.SourceLocked, board.CanMove(0, 0, 1), "Locked to locked");
            Assert.AreEqual(MoveResult.TargetLocked, board.CanMove(2, 0, 1), "Locked and full: the lock is the reason");
            Assert.AreEqual(MoveResult.EmptySlot, board.CanMove(0, 2, 2), "An empty slot is still an empty slot");
        }

        [Test]
        public void CanPick_OpenItem_IsOk()
        {
            var board = LockedMiddle(1);
            Assert.AreEqual(MoveResult.Ok, board.CanPick(0, 0));
            Assert.AreEqual(MoveResult.EmptySlot, board.CanPick(0, 2));
            Assert.AreEqual(MoveResult.InvalidShelf, board.CanPick(9, 0));
            Assert.AreEqual(MoveResult.InvalidSlot, board.CanPick(0, 5));
        }

        // ---------- Count down and open ----------

        [Test]
        public void ClearOnNeighbour_CountsTheLockDown()
        {
            var board = LockedMiddle(2);
            var log = new EventLog(board);

            Assert.IsTrue(board.TryMove(2, 0, 0)); // A onto shelf 0, the locked shelf's neighbour: triple clears.

            Assert.AreEqual(1, log.Count("Cleared 0"));
            Assert.AreEqual(1, board.GetShelf(1).LockClearsLeft);
            Assert.IsTrue(board.GetShelf(1).IsLocked);
            Assert.AreEqual(0, log.Count("Unlocked"));
            Assert.AreEqual(MoveResult.TargetLocked, board.CanMove(2, 1, 1), "Still locked with one clear to go.");
        }

        [Test]
        public void Lock_OpensAtExactlyLockClears_AndRaisesEventOnce()
        {
            // Two clears needed: A on shelf 0, then B on shelf 2.
            var board = new Board(
                S(Row(A, A, null)),
                L(2, Row(C, C, null)),
                S(Row(B, B, A)),
                S(Row(B, C, null)));
            var log = new EventLog(board);

            board.TryMove(2, 2, 0); // clear 1 (A)
            Assert.IsTrue(board.GetShelf(1).IsLocked);
            Assert.AreEqual(0, log.Count("Unlocked"));

            board.TryMove(3, 0, 2); // clear 2 (B)
            Assert.IsFalse(board.GetShelf(1).IsLocked);
            Assert.AreEqual(0, board.GetShelf(1).LockClearsLeft);
            CollectionAssert.AreEqual(new[] { "Moved 3->2", "Cleared 2 tawa", "Unlocked 1" }, log.Entries.GetRange(2, 3));

            // Open now: it takes moves and clears like any shelf. That clear doesn't raise Unlocked again.
            Assert.AreEqual(MoveResult.Ok, board.CanMove(3, 1, 1));
            Assert.IsTrue(board.TryMove(3, 1, 1));
            Assert.AreEqual(1, log.Count("Unlocked"));
            Assert.AreEqual(0, board.GetShelf(1).LockClearsLeft, "The count never goes below zero.");
            Assert.IsTrue(board.IsWon);
        }

        [Test]
        public void OpenedShelf_ItemsCanBeMovedOut()
        {
            var board = new Board(
                S(Row(A, A, null)),
                L(1, Row(B, null, null)),
                S(Row(A, B, B)));
            board.TryMove(2, 0, 0); // clear opens shelf 1
            Assert.AreEqual(MoveResult.Ok, board.CanPick(1, 0));
            Assert.IsTrue(board.TryMove(1, 0, 2));
            Assert.IsTrue(board.IsWon);
        }

        [Test]
        public void ChainedClearsFromOneMove_EachCountDown()
        {
            // Moving A onto shelf 0 clears it, reveals a B triple which clears too: two clears, one move.
            var board = new Board(
                S(Row(A, A, null), Row(B, B, B)),
                L(2, Row(C, D, null)),
                S(Row(A, C, D)),
                S(Row(C, D, null)));
            var log = new EventLog(board);

            board.TryMove(2, 0, 0);

            Assert.AreEqual(2, log.Count("Cleared"));
            Assert.IsFalse(board.GetShelf(1).IsLocked);
            CollectionAssert.AreEqual(new[]
            {
                "Moved 2->0", "Cleared 0 tumbler", "Revealed 0", "Cleared 0 tawa", "Unlocked 1",
            }, log.Entries);
        }

        [Test]
        public void OwnShelfClearsAfterOpening_CountDownOtherLocks()
        {
            // Shelf 1 opens after one clear; its own clear then counts down shelf 2.
            var board = new Board(
                S(Row(A, A, null)),
                L(1, Row(B, B, null)),
                L(2, Row(C, C, null)),
                S(Row(A, B, C)));
            var log = new EventLog(board);

            board.TryMove(3, 0, 0); // clear A on shelf 0: shelf 1 opens, shelf 2 has 1 left
            Assert.IsFalse(board.GetShelf(1).IsLocked);
            Assert.AreEqual(1, board.GetShelf(2).LockClearsLeft);

            board.TryMove(3, 1, 1); // clear B on the newly opened shelf 1: shelf 2 opens
            Assert.IsFalse(board.GetShelf(2).IsLocked);
            CollectionAssert.AreEqual(new[] { "Unlocked 1", "Unlocked 2" }, log.Entries.FindAll(e => e.StartsWith("Unlocked")));

            board.TryMove(3, 2, 2);
            Assert.IsTrue(board.IsWon);
        }

        [Test]
        public void MultipleLocks_OneClearOpensSeveral_InShelfOrderAfterCleared()
        {
            var board = new Board(
                L(1, Row(B, B, null)),
                S(Row(A, A, null)),
                L(1, Row(C, C, null)),
                S(Row(A, B, C)));
            var log = new EventLog(board);

            board.TryMove(3, 0, 1);

            CollectionAssert.AreEqual(new[] { "Moved 3->1", "Cleared 1 tumbler", "Unlocked 0", "Unlocked 2" }, log.Entries);
            Assert.IsFalse(board.GetShelf(0).IsLocked);
            Assert.IsFalse(board.GetShelf(2).IsLocked);
        }

        [Test]
        public void MultipleLocks_DifferentCounts_OpenInTurn()
        {
            var board = new Board(
                S(Row(A, A, null)),
                L(1, Row(C, null, null)),
                L(3, Row(D, null, null)),
                S(Row(A, B, B)),
                S(Row(B, C, C)));
            var log = new EventLog(board);

            board.TryMove(3, 0, 0); // clear 1
            Assert.IsFalse(board.GetShelf(1).IsLocked);
            Assert.AreEqual(2, board.GetShelf(2).LockClearsLeft);

            board.TryMove(4, 0, 3); // clear 2 (B on shelf 3)
            Assert.AreEqual(1, board.GetShelf(2).LockClearsLeft);

            board.TryMove(4, 1, 1); // a second C onto the opened shelf 1: no triple yet, no clear
            Assert.AreEqual(1, board.GetShelf(2).LockClearsLeft);
            board.TryMove(4, 2, 1); // third C: clear 3 opens shelf 2
            Assert.IsFalse(board.GetShelf(2).IsLocked);
            CollectionAssert.AreEqual(new[] { "Unlocked 1", "Unlocked 2" }, log.Entries.FindAll(e => e.StartsWith("Unlocked")));
        }

        [Test]
        public void LockedShelfWithHiddenLayers_RevealsNormallyOnceOpen()
        {
            var board = new Board(
                S(Row(A, A, null)),
                L(1, Row(B, null, null), Row(C, C, C)),
                S(Row(A, B, B)));
            Assert.AreEqual(1, board.GetShelf(1).HiddenLayerCount, "Hidden layers stay put while locked.");

            board.TryMove(2, 0, 0); // opens shelf 1
            board.TryMove(1, 0, 2); // B leaves shelf 1, which reveals a C triple that clears; the B triple clears on shelf 2

            Assert.IsTrue(board.IsWon);
        }

        // ---------- Stuck and win ----------

        /// <summary>
        /// Moving shelf 1's A onto shelf 0 fills shelf 0 and reveals a full row on shelf 1; shelf 2 is full. Only shelf 3
        /// has room.
        /// </summary>
        private static Board AlmostStuck(Shelf shelf3) => new Board(
            S(Row(A, B, null)),
            S(Row(C, null, null), Row(D, B, A)),
            S(Row(D, D, C)),
            shelf3);

        [Test]
        public void Stuck_WhenOnlyLockedShelvesHaveRoom()
        {
            var board = AlmostStuck(L(1, Row(null, null, null)));
            var log = new EventLog(board);

            board.TryMove(1, 0, 0);

            Assert.AreEqual(1, log.Count("Stuck"));
            Assert.IsTrue(board.IsStuck);
            Assert.IsFalse(board.HasLegalMove());
        }

        [Test]
        public void NotStuck_WhenTheSameRoomIsOnAnOpenShelf()
        {
            var board = AlmostStuck(S(Row(null, null, null)));
            var log = new EventLog(board);

            board.TryMove(1, 0, 0);

            Assert.AreEqual(0, log.Count("Stuck"));
            Assert.IsTrue(board.HasLegalMove());
        }

        [Test]
        public void IsStuck_WhenOnlyLockedShelvesHoldItems()
        {
            // Open shelves have room but nothing to move; the locked shelf's items can't leave it.
            var board = new Board(S(Row(null, null, null)), L(1, Row(A, B, null)), S(Row(null, null, null)));
            Assert.IsFalse(board.HasLegalMove());
            Assert.IsTrue(board.IsStuck);
        }

        [Test]
        public void LockOpeningOnTheLastClear_RescuesTheBoard()
        {
            // Shelf 1's A goes to shelf 0: shelf 1 reveals a full row, shelf 0 clears and reveals a full row. Every open
            // shelf is full, but that clear opens shelf 2, which has room: a legal move exists, so not stuck.
            var board = new Board(
                S(Row(A, A, null), Row(B, C, D)),
                S(Row(A, null, null), Row(C, B, D)),
                L(1, Row(D, null, null)),
                S(Row(B, C, D)));
            var log = new EventLog(board);

            board.TryMove(1, 0, 0);

            Assert.AreEqual(1, log.Count("Unlocked 2"));
            Assert.AreEqual(0, log.Count("Stuck"));
            Assert.IsTrue(board.HasLegalMove());
            Assert.AreEqual(MoveResult.Ok, board.CanMove(3, 2, 2));
        }

        [Test]
        public void SameBoard_LockNotYetOpen_IsStuck()
        {
            var board = new Board(
                S(Row(A, A, null), Row(B, C, D)),
                S(Row(A, null, null), Row(C, B, D)),
                L(2, Row(D, null, null)),
                S(Row(B, C, D)));
            var log = new EventLog(board);

            board.TryMove(1, 0, 0);

            Assert.AreEqual(0, log.Count("Unlocked"));
            Assert.AreEqual(1, log.Count("Stuck"));
        }

        [Test]
        public void Win_NeedsLockedItemsCleared()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(A, B, null)),
                L(1, Row(B, B, null)));
            var log = new EventLog(board);

            board.TryMove(1, 0, 0); // clears A and opens shelf 2
            Assert.IsFalse(board.IsWon, "The locked shelf's items are still on the board.");
            Assert.AreEqual(3, board.ItemCount);
            Assert.AreEqual(0, log.Count("Won"));
            Assert.AreEqual(0, log.Count("Stuck"));

            board.TryMove(1, 1, 2);
            Assert.IsTrue(board.IsWon);
            Assert.AreEqual("Won", log.Entries[log.Entries.Count - 1]);
        }

        [Test]
        public void EmptyLockedShelf_DoesNotBlockTheWin()
        {
            var board = new Board(S(Row(A, A, null)), S(Row(A, null, null)), L(5, Row(null, null, null)));
            board.TryMove(1, 0, 0);
            Assert.IsTrue(board.IsWon);
            Assert.IsTrue(board.GetShelf(2).IsLocked, "Nothing on it, so the lock never mattered.");
        }
    }
}
