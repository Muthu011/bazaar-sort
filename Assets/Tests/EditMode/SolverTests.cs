using System.IO;
using Game.Core;
using Game.Levels;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Shortest-solution search, simulated plays and dead-end rate (issue #20).</summary>
    public class SolverTests
    {
        private static string[] Row(string a, string b, string c) => new[] { a, b, c };

        private static Board MakeBoard(params string[][][] shelves) => SyntheticLevels.Build(shelves);

        private static void AssertLineWins(Board root, SolveResult result)
        {
            var board = root.Clone();
            foreach (var move in result.Solution)
            {
                Assert.IsFalse(board.IsWon, "won before the line ended");
                Assert.IsTrue(board.TryMove(move.FromShelf, move.FromSlot, move.ToShelf, out var why), $"{move}: {why}");
            }
            Assert.IsTrue(board.IsWon, "line does not win");
        }

        [Test]
        public void Solve_OneMoveBoard_MinMovesIsOne()
        {
            var board = MakeBoard(
                new[] { Row("a", "a", null) },
                new[] { Row("a", null, null) });

            var result = ShortestSolver.Solve(board);

            Assert.AreEqual(Solvability.Solvable, result.Solvability);
            Assert.AreEqual(1, result.MinMoves);
            AssertLineWins(board, result);
        }

        [Test]
        public void Solve_RevealedTripleClearsByItself_MinMovesIsOne()
        {
            // Clearing the a's reveals b,b,b, which clears straight away and wins.
            var board = MakeBoard(
                new[] { Row("a", "a", null), Row("b", "b", "b") },
                new[] { Row("a", null, null) });

            var result = ShortestSolver.Solve(board);

            Assert.AreEqual(1, result.MinMoves);
            AssertLineWins(board, result);
        }

        [Test]
        public void Solve_InterleavedPairs_MinMovesIsFour()
        {
            // Each type is spread one per shelf (lower bound 2 + 2), and 4 is reachable:
            // (0,1,1) (2,0,0) (1,1,0) (2,1,1).
            var board = MakeBoard(
                new[] { Row("a", "b", null) },
                new[] { Row("b", "a", null) },
                new[] { Row("a", "b", null) });

            var result = ShortestSolver.Solve(board);

            Assert.AreEqual(Solvability.Solvable, result.Solvability);
            Assert.AreEqual(4, result.MinMoves);
            AssertLineWins(board, result);
        }

        [Test]
        public void Solve_AlreadyWonBoard_MinMovesIsZero()
        {
            var board = MakeBoard(new[] { Row(null, null, null) });
            Assert.AreEqual(0, ShortestSolver.Solve(board).MinMoves);
        }

        [Test]
        public void Solve_ThirdItemBehindTheOnlyShelfThatCouldEmpty_IsUnsolvable()
        {
            // Four items in front, six front slots on two shelves: shelf 0 can never empty (the four items don't fit on
            // shelf 1), so the third a and b behind it never come forward and no triple can ever form.
            var board = MakeBoard(
                new[] { Row("a", "b", null), Row("a", "b", null) },
                new[] { Row("a", "b", null) });

            var result = ShortestSolver.Solve(board);

            Assert.AreEqual(Solvability.Unsolvable, result.Solvability);
            Assert.AreEqual(-1, result.MinMoves);
            CollectionAssert.IsEmpty(result.Solution);
        }

        [Test]
        public void Solve_NoLegalMoveAtStart_IsUnsolvable()
        {
            var board = MakeBoard(
                new[] { Row("a", "b", "c") },
                new[] { Row("b", "c", "a") },
                new[] { Row("c", "a", "b") });

            Assert.AreEqual(Solvability.Unsolvable, ShortestSolver.Solve(board).Solvability);
        }

        [Test]
        public void Solve_BudgetRunsOut_ReportsUnknownNotAGuess()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(8, 9, 2, 7));
            var result = ShortestSolver.Solve(board, null, new SolverOptions { MaxExpandedNodes = 3 });

            Assert.AreEqual(Solvability.Unknown, result.Solvability);
            Assert.AreEqual(-1, result.MinMoves);
            StringAssert.Contains("budget", result.Reason);
        }

        [TestCase(1, 3)]
        [TestCase(2, 3)]
        [TestCase(3, 8)]
        public void Solve_World1Levels_MatchHandProvenMinimums(int level, int expected)
        {
            // pack-1.md: the L1 to L3 lines are proven minimal by exhaustive search.
            var load = LevelLoader.Load(File.ReadAllText($"Assets/Levels/World1/w1_l{level:D3}.json"));
            var result = ShortestSolver.Solve(load.Board);

            Assert.AreEqual(expected, result.MinMoves);
            AssertLineWins(load.Board, result);
        }

        [TestCase(4, 4, 1, 1UL)]
        [TestCase(4, 4, 1, 2UL)]
        [TestCase(4, 4, 1, 3UL)]
        [TestCase(5, 5, 2, 4UL)]
        [TestCase(5, 5, 1, 5UL)]
        [TestCase(4, 4, 2, 6UL)]
        public void Solve_AStarAgreesWithPlainBreadthFirstSearch(int shelves, int types, int hidden, ulong seed)
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(shelves, types, hidden, seed));

            var fast = ShortestSolver.Solve(board);
            var reference = ShortestSolver.Solve(board, null, new SolverOptions { ReferenceMode = true });

            Assert.AreNotEqual(Solvability.Unknown, reference.Solvability, "reference search ran out of budget");
            Assert.AreEqual(reference.Solvability, fast.Solvability);
            Assert.AreEqual(reference.MinMoves, fast.MinMoves);
            if (fast.Solvability == Solvability.Solvable) AssertLineWins(board, fast);
        }

        [TestCase(5, 5, 1, 4UL, 1)]
        [TestCase(5, 5, 1, 5UL, 2)]
        [TestCase(5, 5, 2, 4UL, 1)]
        public void Solve_WithALockedShelf_AStarAgreesWithPlainBreadthFirstSearch(int shelves, int types, int hidden,
            ulong seed, int lockClears)
        {
            // Issue #31: lock counters are part of the state; the canonical search must still find the true minimum.
            var locks = new int[shelves];
            locks[shelves - 1] = lockClears;
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(shelves, types, hidden, seed), locks);

            var fast = ShortestSolver.Solve(board);
            var reference = ShortestSolver.Solve(board, null, new SolverOptions { ReferenceMode = true });

            Assert.AreNotEqual(Solvability.Unknown, reference.Solvability, "reference search ran out of budget");
            Assert.AreEqual(reference.Solvability, fast.Solvability);
            Assert.AreEqual(reference.MinMoves, fast.MinMoves);
            if (fast.Solvability == Solvability.Solvable) AssertLineWins(board, fast);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void DeadEndOracle_AgreesWithTheShortestSolutionSearch_AlongRandomPlays(int hiddenLayers)
        {
            // Issue #31: the dead-end search expands states a little at a time and recognises known-alive boards by
            // their cheap key; its answers must still be facts: alive exactly when a fresh shortest-solution search
            // (checked against plain breadth-first search above) finds a win. Small tight boards with a locked shelf,
            // so the random plays reach dead ends as well as live states.
            int alive = 0, dead = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var board = SyntheticLevels.Build(SyntheticLevels.Deal(4, 4, hiddenLayers, seed), new[] { 0, 0, 0, 1 });
                var context = new SearchContext(board, null);
                var oracle = new LivenessOracle(context, 500_000, null);
                var rng = new System.Random((int)seed);
                var moves = new System.Collections.Generic.List<SolverMove>();
                var current = context.Root;
                for (int step = 0; step < 60 && !current.IsWon; step++)
                {
                    var fresh = ShortestSolver.Solve(current);
                    Assert.AreNotEqual(Solvability.Unknown, fresh.Solvability);
                    bool expected = fresh.Solvability == Solvability.Solvable;
                    Assert.AreEqual(expected, oracle.IsAlive(current), $"seed {seed} step {step}");
                    if (expected) alive++;
                    else dead++;
                    if (expected && rng.Next(3) == 0) oracle.MarkAlive(current);

                    context.GenerateMoves(current, moves, false);
                    if (moves.Count == 0) break;
                    current = SearchContext.Apply(current, moves[rng.Next(moves.Count)]);
                }
            }
            Assert.Greater(alive, 0);
            Assert.Greater(dead, 0, "the random plays should reach some dead ends");
        }

        [Test]
        public void Analysis_UnsolvableBoard_EveryPlayLosesAndEveryStateIsADeadEnd()
        {
            var board = MakeBoard(
                new[] { Row("a", "b", null), Row("a", "b", null) },
                new[] { Row("a", "b", null) });

            var analysis = LevelAnalyzer.AnalyzeBoard(board, null, new SolverOptions { Plays = 50 });

            Assert.AreEqual(0, analysis.Plays.Wins);
            Assert.AreEqual(1.0, analysis.Plays.DeadEndRate);
            Assert.IsTrue(analysis.Plays.DeadEndComplete);
        }

        [Test]
        public void Analysis_ForcedWin_WinRateIsOneAndNoDeadEnds()
        {
            var board = MakeBoard(
                new[] { Row("a", "a", null) },
                new[] { Row("a", null, null) });

            var analysis = LevelAnalyzer.AnalyzeBoard(board, null, new SolverOptions { Plays = 50 });

            Assert.AreEqual(1.0, analysis.Plays.WinRate);
            Assert.AreEqual(0.0, analysis.Plays.DeadEndRate);
        }

        [Test]
        public void Analysis_SameSeed_GivesIdenticalWinAndDeadEndRates()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(5, 6, 2, 11));
            var options = new SolverOptions { Plays = 300 };

            var first = LevelAnalyzer.AnalyzeBoard(board, null, options);
            var second = LevelAnalyzer.AnalyzeBoard(board.Clone(), null, options);

            Assert.AreEqual(first.Plays.Wins, second.Plays.Wins);
            Assert.AreEqual(first.Plays.LossesStuck, second.Plays.LossesStuck);
            Assert.AreEqual(first.Plays.DeadStates, second.Plays.DeadStates);
            Assert.AreEqual(first.Plays.AliveStates, second.Plays.AliveStates);
            Assert.AreEqual(first.Plays.DeadEndRate, second.Plays.DeadEndRate);
        }

        [Test]
        public void Analysis_DoesNotChangeTheBoardItWasGiven()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(5, 6, 2, 11));
            int items = board.ItemCount;
            string front = board.GetShelf(0).GetFrontItem(0);

            LevelAnalyzer.AnalyzeBoard(board, null, new SolverOptions { Plays = 20 });

            Assert.AreEqual(items, board.ItemCount);
            Assert.AreEqual(front, board.GetShelf(0).GetFrontItem(0));
        }
    }
}
