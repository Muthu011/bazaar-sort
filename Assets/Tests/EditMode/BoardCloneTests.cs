using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="Board.Clone"/>, used by the level solver to branch a search.</summary>
    public class BoardCloneTests
    {
        private static Board MakeBoard()
        {
            return new Board(
                new Shelf(new[] { "a", "a", null }, new[] { "b", "c", "c" }),
                new Shelf(new[] { "a", "b", "b" }),
                new Shelf(new[] { "c", null, null }));
        }

        private static string Front(Board board, int shelf)
        {
            var s = board.GetShelf(shelf);
            return $"{s.GetFrontItem(0) ?? "-"},{s.GetFrontItem(1) ?? "-"},{s.GetFrontItem(2) ?? "-"}";
        }

        [Test]
        public void Clone_CopiesFrontRowsHiddenLayersAndItemCount()
        {
            var board = MakeBoard();
            var copy = board.Clone();

            Assert.AreNotSame(board, copy);
            Assert.AreEqual(board.ShelfCount, copy.ShelfCount);
            Assert.AreEqual(board.ItemCount, copy.ItemCount);
            for (int i = 0; i < board.ShelfCount; i++)
            {
                Assert.AreNotSame(board.GetShelf(i), copy.GetShelf(i));
                Assert.AreEqual(Front(board, i), Front(copy, i));
                Assert.AreEqual(board.GetShelf(i).HiddenLayerCount, copy.GetShelf(i).HiddenLayerCount);
            }
            Assert.AreEqual("b", copy.GetShelf(0).GetHiddenItem(0, 0));
        }

        [Test]
        public void Clone_MovesOnTheCopyDoNotChangeTheOriginal()
        {
            var board = MakeBoard();
            var copy = board.Clone();

            // Completes the a-triple on shelf 0, which reveals b,c,c there.
            Assert.IsTrue(copy.TryMove(1, 0, 0));
            Assert.AreEqual("b,c,c", Front(copy, 0));
            Assert.AreEqual(0, copy.GetShelf(0).HiddenLayerCount);
            Assert.AreEqual(6, copy.ItemCount);

            Assert.AreEqual("a,a,-", Front(board, 0));
            Assert.AreEqual("a,b,b", Front(board, 1));
            Assert.AreEqual(1, board.GetShelf(0).HiddenLayerCount);
            Assert.AreEqual(9, board.ItemCount);
        }

        [Test]
        public void Clone_HasNoSubscribersFromTheOriginal()
        {
            var board = MakeBoard();
            int moved = 0;
            board.Moved += _ => moved++;

            var copy = board.Clone();
            Assert.IsTrue(copy.TryMove(2, 0, 0));
            Assert.AreEqual(0, moved);
        }

        [Test]
        public void Clone_OfAMidGameBoard_ContinuesFromTheSameState()
        {
            var board = MakeBoard();
            Assert.IsTrue(board.TryMove(1, 0, 0)); // clears a, reveals b,c,c on shelf 0
            var copy = board.Clone();

            Assert.AreEqual(board.ItemCount, copy.ItemCount);
            Assert.IsTrue(copy.TryMove(2, 0, 1)); // c onto shelf 1 (-, b, b) -> c,b,b
            Assert.AreEqual("c,b,b", Front(copy, 1));
            Assert.AreEqual("-,b,b", Front(board, 1));
        }

        // Issue #31: the solver tries moves on one scratch board instead of cloning for every move.
        [Test]
        public void Board_CopyStateFrom_MatchesTheSourceAndStaysIndependent()
        {
            var root = new Board(
                new Shelf(new[] { "a", "a", null }, new[] { "b", "c", "c" }),
                new Shelf(new[] { "a", "b", "b" }),
                Shelf.Locked(1, new[] { "c", null, null }));
            var source = root.Clone();
            Assert.IsTrue(source.TryMove(1, 0, 0)); // clears a, opens the lock, reveals b,c,c

            var target = root.Clone();
            target.CopyStateFrom(source);

            Assert.AreEqual(source.ItemCount, target.ItemCount);
            for (int i = 0; i < root.ShelfCount; i++)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    Assert.AreEqual(source.GetShelf(i).GetFrontItem(slot), target.GetShelf(i).GetFrontItem(slot));
                }
                Assert.AreEqual(source.GetShelf(i).HiddenLayerCount, target.GetShelf(i).HiddenLayerCount);
                Assert.AreEqual(source.GetShelf(i).LockClearsLeft, target.GetShelf(i).LockClearsLeft);
            }

            Assert.IsTrue(target.TryMove(2, 0, 1)); // c from the opened shelf onto shelf 1
            Assert.AreEqual(null, source.GetShelf(1).GetFrontItem(0));
            Assert.AreEqual("c", target.GetShelf(1).GetFrontItem(0));
        }

        [Test]
        public void Board_CopyStateFrom_RejectsAnUnrelatedBoard()
        {
            var a = new Board(new Shelf(new[] { "a", "a", null }), new Shelf(new[] { "a", null, null }));
            var b = new Board(new Shelf(new[] { "a", "a", null }), new Shelf(new[] { "a", null, null }));
            Assert.Throws<System.ArgumentException>(() => a.CopyStateFrom(b));
        }
    }
}
