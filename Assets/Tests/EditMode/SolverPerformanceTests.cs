using Game.Solver;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Issue #20: a 14-shelf level must solve in under 10 s on the Mac. The board is a fixed synthetic deal
    /// (<see cref="SyntheticLevels.Deal"/>: 14 shelves, 16 types = 48 items, 4 hidden layers, 6 free front slots, seed 1).
    /// The time is logged so it can be quoted in the pull request.
    /// </summary>
    public class SolverPerformanceTests
    {
        private const double LimitSeconds = 10;

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
    }
}
