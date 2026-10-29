using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;

namespace Game.Solver
{
    /// <summary>Outcome of <see cref="ShortestSolver.Solve"/>.</summary>
    public sealed class SolveResult
    {
        internal SolveResult(Solvability solvability, IReadOnlyList<SolverMove> solution, long expanded, long generated,
            double seconds, string reason)
        {
            Solvability = solvability;
            Solution = solution ?? Array.Empty<SolverMove>();
            ExpandedNodes = expanded;
            GeneratedNodes = generated;
            Seconds = seconds;
            Reason = reason;
        }

        public Solvability Solvability { get; }

        /// <summary>Exact minimum number of moves when <see cref="Solvability"/> is Solvable, otherwise -1.</summary>
        public int MinMoves => Solvability == Solvability.Solvable ? Solution.Count : -1;

        /// <summary>One shortest winning line (moves as <see cref="Board.TryMove(int,int,int)"/> takes them).</summary>
        public IReadOnlyList<SolverMove> Solution { get; }

        public long ExpandedNodes { get; }
        /// <summary>Child boards built with the Core rules.</summary>
        public long GeneratedNodes { get; }
        public double Seconds { get; }

        /// <summary>Why the answer is Unknown (budget or time ran out); null otherwise.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// Exact shortest solution by A* over canonical board states (<see cref="SearchContext"/>), with the admissible,
    /// consistent heuristic from <see cref="SearchContext.Heuristic"/>. Each move costs 1. Moves are applied by the Core
    /// <see cref="Board"/> itself. Nodes store only (parent, move); a node's board is rebuilt by replaying its line from
    /// the root, which keeps memory small enough for 14-shelf levels.
    ///
    /// Optimality: with a consistent heuristic the first time a state is expanded its depth is minimal. A winning child
    /// of the node being expanded is accepted at once: that node had the lowest f = g + h, a non-won board has h &gt;= 1,
    /// so every other solution costs at least g + 1.
    ///
    /// If the node budget or time limit runs out first, the answer is <see cref="Solvability.Unknown"/>, never a guess.
    /// Unsolvable is reported only when every reachable state has been expanded.
    /// </summary>
    public static class ShortestSolver
    {
        public static SolveResult Solve(Board root, IReadOnlyList<bool> pinnedShelves = null, SolverOptions options = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            options = options ?? SolverOptions.Default;
            return Solve(new SearchContext(root, pinnedShelves, !options.ReferenceMode), options, SearchBudget.For(options));
        }

        /// <summary>
        /// Searches within <paramref name="budget"/> (shared with the other phases of a level analysis) as well as this
        /// search's own <see cref="SolverOptions.MaxExpandedNodes"/> and <see cref="SolverOptions.TimeLimitSeconds"/>.
        /// </summary>
        internal static SolveResult Solve(SearchContext context, SolverOptions options, SearchBudget budget)
        {
            var stopwatch = Stopwatch.StartNew();
            var root = context.Root;
            if (root.IsWon)
            {
                return new SolveResult(Solvability.Solvable, Array.Empty<SolverMove>(), 0, 0, 0, null);
            }

            var tree = new SearchTree();
            // Generation-time duplicate filter on the cheap exact key (same board, normal shelves in any order).
            var seen = new Dictionary<StateKey, int>();
            // Expansion-time filter on the canonical key (also merges shelf reorderings and type renamings).
            var closed = new Dictionary<StateKey, int>();
            var open = new MinHeap();
            var moves = new List<SolverMove>();
            var replay = new List<SolverMove>();
            // Children are tried on one scratch board (Board.CopyStateFrom + Board.TryMove), so no board is allocated
            // per child; only expanded nodes' boards are rebuilt.
            var scratch = root.Clone();

            bool reference = options.ReferenceMode;
            seen[context.CheapKey(root)] = 0;
            tree.Add(-1, default, 0);
            open.Push(reference ? 0 : context.HeuristicOfLastRead(), 0, 0);
            var cache = new BoardCache();
            long expanded = 0;
            long generated = 1;

            while (open.Count > 0)
            {
                int index = open.Pop();
                int depth = tree.Depth[index];
                var board = cache.Get(root, tree, index, replay);
                var key = context.Key(board);
                if (closed.TryGetValue(key, out int done) && done <= depth) continue;
                closed[key] = depth;

                expanded++;
                if (expanded > options.MaxExpandedNodes)
                {
                    return Unknown($"node budget of {options.MaxExpandedNodes} expanded states ran out", expanded, generated, stopwatch);
                }
                if ((expanded & 255) == 0 && stopwatch.Elapsed.TotalSeconds > options.TimeLimitSeconds)
                {
                    return Unknown($"time limit of {options.TimeLimitSeconds:0.#} s ran out", expanded, generated, stopwatch);
                }
                if (!budget.CheckClock())
                {
                    return Unknown(budget.Reason, expanded, generated, stopwatch);
                }

                context.GenerateMoves(board, moves, !reference);
                int childDepth = depth + 1;
                for (int m = 0; m < moves.Count; m++)
                {
                    if (!budget.Charge())
                    {
                        return Unknown(budget.Reason, expanded, generated, stopwatch);
                    }
                    var move = moves[m];
                    scratch.CopyStateFrom(board);
                    if (!scratch.TryMove(move.FromShelf, move.FromSlot, move.ToShelf, out var result))
                    {
                        throw new InvalidOperationException($"Solver tried an illegal move {move}: {result}.");
                    }
                    generated++;
                    if (scratch.IsWon)
                    {
                        var line = new List<SolverMove>();
                        tree.LineTo(index, line);
                        line.Add(move);
                        return new SolveResult(Solvability.Solvable, line, expanded, generated,
                            stopwatch.Elapsed.TotalSeconds, null);
                    }

                    var cheap = context.CheapKey(scratch);
                    if (seen.TryGetValue(cheap, out int known) && known <= childDepth) continue;
                    seen[cheap] = childDepth;
                    if (!scratch.HasLegalMove()) continue; // stuck: can never win
                    int h = reference ? 0 : context.HeuristicOfLastRead();

                    int child = tree.Add(index, move, childDepth);
                    open.Push(childDepth + h, childDepth, child);
                }
            }

            return new SolveResult(Solvability.Unsolvable, null, expanded, generated, stopwatch.Elapsed.TotalSeconds, null);
        }

        private static SolveResult Unknown(string reason, long expanded, long generated, Stopwatch stopwatch)
        {
            return new SolveResult(Solvability.Unknown, null, expanded, generated, stopwatch.Elapsed.TotalSeconds, reason);
        }
    }
}
