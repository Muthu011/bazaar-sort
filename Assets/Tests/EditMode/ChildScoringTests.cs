using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Issue #38: the searches score a child from its parent (<see cref="SearchContext.ChildHeuristic"/>,
    /// <see cref="SearchContext.ChildCheapKey"/>) and, for a plain move, before building it
    /// (<see cref="SearchContext.PredictChildHeuristic"/>). Every shortcut must give exactly what reading the child board
    /// made by the Core rules gives, on every level and on synthetic boards with locked shelves.
    /// </summary>
    public class ChildScoringTests
    {
        private static IEnumerable<string> LevelFiles() => SolverRunner.FindLevelFiles();

        [TestCaseSource(nameof(LevelFiles))]
        public void ChildScores_MatchTheChildBoard_OnLevel(string path)
        {
            var load = LevelLoader.Load(File.ReadAllText(path), Path.GetFileName(path));
            Assume.That(load.Success, string.Join("; ", load.Errors));
            CheckRandomWalks(load.Board, 6, 200);
        }

        [TestCase(6, 6, 2, 3UL, 0)]
        [TestCase(8, 9, 3, 5UL, 1)]
        [TestCase(10, 12, 3, 5UL, 2)]
        [TestCase(12, 14, 4, 9UL, 3)]
        public void ChildScores_MatchTheChildBoard_OnSyntheticBoards(int shelves, int types, int hidden, ulong seed,
            int lockedShelves)
        {
            var locks = new int[shelves];
            for (int k = 0; k < lockedShelves; k++) locks[shelves - 1 - k] = k + 1;
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(shelves, types, hidden, seed), locks);
            CheckRandomWalks(board, 6, 200);
        }

        private static void CheckRandomWalks(Board board, int walks, int steps)
        {
            var context = new SearchContext(board, null);
            var moves = new List<SolverMove>();
            int predictedMoves = 0, scoredMoves = 0;
            for (int walk = 0; walk < walks; walk++)
            {
                var rng = new Random(walk + 1);
                var current = context.Root;
                for (int step = 0; step < steps && !current.IsWon; step++)
                {
                    context.BeginChildren(current);
                    // Unmerged moves: every move the merged list has, and more.
                    context.GenerateMoves(current, moves, false);
                    if (moves.Count == 0) break;
                    foreach (var move in moves)
                    {
                        var child = SearchContext.Apply(current, move);
                        int predicted = context.PredictChildHeuristic(move);
                        StateKey predictedCheap = default;
                        bool predictedLegal = false;
                        if (predicted >= 0)
                        {
                            predictedCheap = context.ChildCheapKey();
                            predictedLegal = context.PredictedChildHasLegalMove();
                        }

                        int scored = context.ChildHeuristic(child, move);
                        bool cleared = child.ItemCount != current.ItemCount;
                        StateKey scoredCheap = scored >= 0 ? context.ChildCheapKey() : default;
                        var cheap = context.CheapKey(child);
                        int heuristic = context.Heuristic(child);

                        if (cleared)
                        {
                            Assert.AreEqual(-1, scored, $"{move}: a clear must be read in full");
                            Assert.AreEqual(-1, predicted, $"{move}: a clear must not be predicted");
                            continue;
                        }
                        Assert.AreEqual(heuristic, scored, $"{move}: ChildHeuristic");
                        Assert.AreEqual(cheap, scoredCheap, $"{move}: ChildCheapKey after ChildHeuristic");
                        scoredMoves++;
                        if (predicted < 0) continue;
                        Assert.AreEqual(heuristic, predicted, $"{move}: PredictChildHeuristic");
                        Assert.AreEqual(cheap, predictedCheap, $"{move}: ChildCheapKey after PredictChildHeuristic");
                        Assert.AreEqual(child.HasLegalMove(), predictedLegal, $"{move}: PredictedChildHasLegalMove");
                        predictedMoves++;
                    }
                    current = SearchContext.Apply(current, moves[rng.Next(moves.Count)]);
                }
            }
            Assert.Greater(scoredMoves, 0, "no move was scored from its parent");
            Assert.Greater(predictedMoves, 0, "no move was predicted");
        }
    }
}
