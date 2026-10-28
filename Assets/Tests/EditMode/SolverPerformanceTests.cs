using Game.Solver;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Issue #20: a 14-shelf level must solve in under 10 s on the Mac. The board is a fixed synthetic deal
    /// (<see cref="SyntheticLevels.Deal"/>: 14 shelves, 16 types = 48 items, 4 hidden layers, 6 free front slots, seed 1).
    /// The time is logged so it can be quoted in the pull request. Issue #31 adds the full analysis of the same deal with
    /// two locked shelves, under the same 10 s.
    /// </summary>
    public class SolverPerformanceTests
    {
        private const double LimitSeconds = 10;

        /// <summary>
        /// Earlier tests (the 30 s budget tests in particular) leave a lot of garbage; collect it first so the timing
        /// measures the solver, not the editor's garbage collector catching up.
        /// </summary>
        [SetUp]
        public void CollectGarbage()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
        }

        [Test]
        public void Solve_FourteenShelfLevel_UnderTenSeconds()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(14, 16, 4, 1));
            Assert.AreEqual(14, board.ShelfCount);

            var result = ShortestSolver.Solve(board, null, new SolverOptions { TimeLimitSeconds = LimitSeconds });

            string report = $"14-shelf synthetic solve: {result.Solvability}, minMoves {result.MinMoves}, " +
                            $"{result.ExpandedNodes} states expanded, {result.GeneratedNodes} boards built, " +
                            $"{result.Seconds:0.00} s";
            Debug.Log(report);
            TestContext.WriteLine(report);

            Assert.AreEqual(Solvability.Solvable, result.Solvability, result.Reason);
            Assert.Less(result.Seconds, LimitSeconds);

            var replay = board.Clone();
            foreach (var move in result.Solution) Assert.IsTrue(replay.TryMove(move.FromShelf, move.FromSlot, move.ToShelf));
            Assert.IsTrue(replay.IsWon);
        }

        /// <summary>
        /// Issue #31: the same 14-shelf deal with its last two shelves locked (2 and 3 clears, as in the World 1 hard
        /// drafts), fully analysed with the default options: shortest solution, 1000 simulated plays and dead-end rate.
        /// </summary>
        [Test]
        public void Analyse_FourteenShelfLevelWithLocks_UnderTenSeconds()
        {
            var locks = new int[14];
            locks[12] = 2;
            locks[13] = 3;
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(14, 16, 4, 1), locks);
            Assert.AreEqual(14, board.ShelfCount);

            var analysis = LevelAnalyzer.AnalyzeBoard(board, null, null, "synthetic 14-shelf, 2 locks");

            string report = $"14-shelf locked analysis: {analysis.Outcome}; {analysis}; " +
                            $"A* {analysis.Shortest.ExpandedNodes} states expanded in {analysis.Shortest.Seconds:0.00} s";
            Debug.Log(report);
            TestContext.WriteLine(report);

            Assert.AreEqual(AnalysisOutcome.Complete, analysis.Outcome, analysis.InconclusiveReason);
            Assert.AreEqual(Solvability.Solvable, analysis.Solvability);
            Assert.Less(analysis.Seconds, LimitSeconds);
        }
    }
}
