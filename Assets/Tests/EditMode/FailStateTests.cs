using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Fail state (issue #41): <see cref="Board.Failed"/> follows <see cref="Board.Stuck"/> when no booster can help
    /// (<see cref="Board.Rescue"/>), never otherwise. Boosters are issue #42; today the default rescue says no.
    /// </summary>
    public class FailStateTests
    {
        private const string A = "tumbler";
        private const string B = "masala_dabba";
        private const string C = "idli_stand";
        private const string D = "tawa";

        private static string[] Row(string s0, string s1, string s2) => new[] { s0, s1, s2 };

        private static Shelf S(params string[][] layers) => new Shelf(layers);

        /// <summary>Moving shelf 0's A onto shelf 1 fills it and reveals a full row on shelf 0: stuck after one move.</summary>
        private static Board OneMoveFromStuck() => new Board(
            S(Row(A, null, null), Row(B, C, D)),
            S(Row(B, C, null)),
            S(Row(D, A, B)));

        /// <summary>A rescue that answers a fixed value and records what it was asked.</summary>
        private sealed class FakeRescue : IBoosterRescue
        {
            public bool Answer;
            public int Calls;
            public Board AskedAbout;

            public bool CanRescue(Board board)
            {
                Calls++;
                AskedAbout = board;
                return Answer;
            }
        }

        private static List<string> Log(Board board)
        {
            var log = new List<string>();
            board.Moved += _ => log.Add("Moved");
            board.Cleared += _ => log.Add("Cleared");
            board.LayerRevealed += _ => log.Add("Revealed");
            board.Won += () => log.Add("Won");
            board.Stuck += () => log.Add("Stuck");
            board.Failed += () => log.Add("Failed");
            return log;
        }

        [Test]
        public void DefaultRescue_IsNoBooster_AndNeverHelps()
        {
            var board = OneMoveFromStuck();
            Assert.AreSame(NoBoosterRescue.Instance, board.Rescue);
            Assert.IsFalse(NoBoosterRescue.Instance.CanRescue(board));
        }

        [Test]
        public void SettingNullRescue_RestoresTheDefault()
        {
            var board = OneMoveFromStuck();
            board.Rescue = new FakeRescue();
            board.Rescue = null;
            Assert.AreSame(NoBoosterRescue.Instance, board.Rescue);
        }

        [Test]
        public void StuckMove_WithNoBooster_RaisesFailedOnce_RightAfterStuck()
        {
            var board = OneMoveFromStuck();
            var log = Log(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            CollectionAssert.AreEqual(new[] { "Moved", "Revealed", "Stuck", "Failed" }, log);
            Assert.IsTrue(board.IsStuck);
            Assert.IsTrue(board.IsFailed);
        }

        [Test]
        public void StuckMove_WhenABoosterCanHelp_RaisesStuckButNotFailed()
        {
            var board = OneMoveFromStuck();
            var rescue = new FakeRescue { Answer = true };
            board.Rescue = rescue;
            var log = Log(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            CollectionAssert.AreEqual(new[] { "Moved", "Revealed", "Stuck" }, log);
            Assert.AreEqual(1, rescue.Calls, "The board asks once, after the move settled.");
            Assert.AreSame(board, rescue.AskedAbout, "The rescue is asked about this board.");
            Assert.IsTrue(board.IsStuck);
            Assert.IsFalse(board.IsFailed);
        }

        [Test]
        public void StuckMove_WhenTheBoosterSaysNo_RaisesFailed()
        {
            var board = OneMoveFromStuck();
            board.Rescue = new FakeRescue { Answer = false };
            var log = Log(board);

            board.TryMove(0, 0, 1);
            CollectionAssert.AreEqual(new[] { "Moved", "Revealed", "Stuck", "Failed" }, log);
        }

        [Test]
        public void RescueIsNotAsked_WhileALegalMoveRemains()
        {
            // Two free slots on shelf 1: after one move into it, one remains.
            var board = new Board(
                S(Row(A, B, null)),
                S(Row(C, null, null)),
                S(Row(D, A, B)));
            var rescue = new FakeRescue();
            board.Rescue = rescue;
            var log = Log(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            Assert.IsTrue(board.HasLegalMove());
            Assert.AreEqual(0, rescue.Calls);
            Assert.IsFalse(board.IsFailed);
            CollectionAssert.DoesNotContain(log, "Failed");
            CollectionAssert.DoesNotContain(log, "Stuck");
        }

        [Test]
        public void WinningMove_NeverFails_AndDoesNotAskTheRescue()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(A, null, null)));
            var rescue = new FakeRescue();
            board.Rescue = rescue;
            var log = Log(board);

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.IsTrue(board.IsWon);
            Assert.IsFalse(board.IsFailed);
            Assert.AreEqual(0, rescue.Calls);
            CollectionAssert.AreEqual(new[] { "Moved", "Cleared", "Won" }, log);
        }

        [Test]
        public void MoveThatFillsTheLastSlotButClears_DoesNotFail()
        {
            var board = new Board(
                S(Row(A, A, null)),
                S(Row(B, C, A)),
                S(Row(B, B, C)));
            var log = Log(board);

            Assert.IsTrue(board.TryMove(1, 2, 0));
            Assert.IsFalse(board.IsFailed);
            CollectionAssert.DoesNotContain(log, "Failed");
        }

        [Test]
        public void FailedBoard_RejectsEveryMove_AndRaisesNothingMore()
        {
            var board = OneMoveFromStuck();
            board.TryMove(0, 0, 1);
            var log = Log(board);

            for (int from = 0; from < board.ShelfCount; from++)
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    for (int to = 0; to < board.ShelfCount; to++)
                        Assert.IsFalse(board.TryMove(from, slot, to));
            Assert.IsEmpty(log);
        }

        [Test]
        public void StuckOnlyAmongOpenShelves_WithALockedShelfLeft_Fails()
        {
            // Shelf 2 is locked with a free slot; it takes no moves, so the board is stuck and, with no booster, lost.
            var board = new Board(
                S(Row(A, null, null), Row(B, C, D)),
                S(Row(B, C, null)),
                new Shelf(new IReadOnlyList<string>[] { Row(D, A, null) }, "s3", 1));
            var log = Log(board);

            Assert.IsTrue(board.TryMove(0, 0, 1));
            Assert.IsTrue(board.IsFailed);
            CollectionAssert.AreEqual(new[] { "Moved", "Revealed", "Stuck", "Failed" }, log);
        }

        [Test]
        public void ConstructionDoesNotRaiseFailed_EvenIfInitiallyStuck()
        {
            var board = new Board(S(Row(A, B, C)), S(Row(D, A, B)));
            var log = Log(board);
            Assert.IsTrue(board.IsFailed, "The state reads as failed...");
            Assert.IsEmpty(log, "...but only a move raises events.");
        }

        [Test]
        public void Clone_StartsWithTheDefaultRescue_AndCanFailOnItsOwn()
        {
            var board = OneMoveFromStuck();
            board.Rescue = new FakeRescue { Answer = true };
            var clone = board.Clone();
            Assert.AreSame(NoBoosterRescue.Instance, clone.Rescue);

            var log = Log(clone);
            clone.TryMove(0, 0, 1);
            CollectionAssert.AreEqual(new[] { "Moved", "Revealed", "Stuck", "Failed" }, log);
            Assert.IsFalse(board.IsStuck, "The original is untouched.");
        }

        [Test]
        public void CopyStateFrom_LetsFailedBeRaisedAgain()
        {
            var start = OneMoveFromStuck();
            var board = start.Clone();
            board.TryMove(0, 0, 1);
            board.CopyStateFrom(start);
            int failed = 0;
            board.Failed += () => failed++;

            board.TryMove(0, 0, 1);
            Assert.AreEqual(1, failed);
        }
    }
}
