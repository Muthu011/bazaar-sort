using System;
using Game.Core;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Board.CanReceive (issue #31) is the target half of CanMove: search tools test each target once per board, so
    /// the two must agree on every board, locked shelves and full rows included.
    /// </summary>
    public class BoardCanReceiveTests
    {
        private static void AssertAgrees(Board board)
        {
            for (int from = 0; from < board.ShelfCount; from++)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    for (int to = 0; to < board.ShelfCount; to++)
                    {
                        bool canMove = board.CanMove(from, slot, to) == MoveResult.Ok;
                        bool split = board.CanPick(from, slot) == MoveResult.Ok && from != to && board.CanReceive(to);
                        Assert.AreEqual(canMove, split, $"from {from} slot {slot} to {to}");
                    }
                }
            }
        }

        [Test]
        public void CanReceive_IsFalseForLockedAndFullShelves()
        {
            var board = SyntheticLevels.Build(new[]
            {
                new[] { new[] { "a", "b", "c" } },
                new[] { new[] { "a", null, null } },
                new[] { new[] { "b", "c", null }, new[] { "a", "b", "c" } },
            }, new[] { 0, 0, 1 });

            Assert.IsFalse(board.CanReceive(0), "full front row");
            Assert.IsTrue(board.CanReceive(1));
            Assert.IsFalse(board.CanReceive(2), "locked");
        }

        [Test]
        public void CanReceive_AgreesWithCanMove_AlongRandomPlays([Values(1, 2, 3)] int seed)
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(10, 12, 3, (ulong)seed),
                new[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1 });
            var rng = new Random(seed);
            for (int step = 0; step < 300 && !board.IsWon && board.HasLegalMove(); step++)
            {
                AssertAgrees(board);
                int from, slot, to;
                do
                {
                    from = rng.Next(board.ShelfCount);
                    slot = rng.Next(Shelf.SlotCount);
                    to = rng.Next(board.ShelfCount);
                } while (board.CanMove(from, slot, to) != MoveResult.Ok);
                Assert.IsTrue(board.TryMove(from, slot, to));
            }
            AssertAgrees(board);
        }
    }
}
