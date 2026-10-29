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
    /// Partial expansion (issue #38): most children of an expanded state have a higher f than it and are never expanded
    /// before the solution is found, yet building their keys was most of the work. So a state is expanded in rounds: each
    /// round pushes only the children whose f equals the round's threshold (scored from the parent's tallies by
    /// <see cref="SearchContext.ChildHeuristic"/>), and the state goes back on the open list with the lowest f it put
    /// off. A child that moves one item without clearing or revealing anything has its heuristic worked out before it is
    /// built (<see cref="SearchContext.PredictChildHeuristic"/>), so the children a round puts off are mostly never
    /// built; every child the search keeps is built by the Core rules and checked against that. That is Partial
    /// Expansion A* (Yoshizumi, Miura and Ishida, 2000): every child is still pushed before any state
    /// with a higher f is expanded, so the order by f, and with it optimality, is that of plain A*. The plain breadth-first
    /// reference search (no heuristic) expands every state in one round.
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
            // Per node: the f it was pushed with (its round's threshold) and the threshold of its last round (-1: none).
            var pushedF = new List<int>();
            var lastRound = new List<int>();
            // Children are tried on one scratch board (Board.CopyStateFrom + Board.TryMove), so no board is allocated
            // per child; only expanded nodes' boards are rebuilt.
            var scratch = root.Clone();

            bool reference = options.ReferenceMode;
            seen[context.CheapKey(root)] = 0;
            int rootF = reference ? 0 : context.HeuristicOfLastRead();
            tree.Add(-1, default, 0);
            pushedF.Add(rootF);
            lastRound.Add(-1);
            open.Push(rootF, 0, 0);
            var cache = new BoardCache();
            long expanded = 0;
            long rounds = 0;
            long generated = 1;

            while (open.Count > 0)
            {
                int index = open.Pop();
                int depth = tree.Depth[index];
                int previous = lastRound[index];
                var board = cache.Get(root, tree, index, replay);
                var key = context.Key(board);
                if (previous < 0)
                {
                    if (closed.TryGetValue(key, out int done) && done <= depth) continue;
                    closed[key] = depth;
                    expanded++;
                    if (expanded > options.MaxExpandedNodes)
                    {
                        return Unknown($"node budget of {options.MaxExpandedNodes} expanded states ran out", expanded, generated, stopwatch);
                    }
                }
                else if (closed[key] < depth)
                {
                    continue; // a shallower copy of this state was expanded since: its rounds cover these children
                }
                rounds++;
                if ((rounds & 255) == 0 && stopwatch.Elapsed.TotalSeconds > options.TimeLimitSeconds)
                {
                    return Unknown($"time limit of {options.TimeLimitSeconds:0.#} s ran out", expanded, generated, stopwatch);
                }
                if (!budget.CheckClock())
                {
                    return Unknown(budget.Reason, expanded, generated, stopwatch);
                }

                // The reference search has no heuristic: one round takes every child.
                int threshold = reference ? int.MaxValue : pushedF[index];
                int putOff = int.MaxValue;
                if (!reference) context.BeginChildren(board);
                context.GenerateMoves(board, moves, !reference);
                int childDepth = depth + 1;
                for (int m = 0; m < moves.Count; m++)
                {
                    var move = moves[m];
                    // A child whose f is known without making the move and falls outside this round is not built.
                    int predicted = reference ? -1 : context.PredictChildHeuristic(move);
                    if (predicted >= 0)
                    {
                        int predictedF = childDepth + predicted;
                        if (predictedF <= previous) continue;
                        if (predictedF > threshold)
                        {
                            if (predictedF < putOff) putOff = predictedF;
                            continue;
                        }
                    }
                    if (!budget.Charge())
                    {
                        return Unknown(budget.Reason, expanded, generated, stopwatch);
                    }
                    scratch.CopyStateFrom(board);
                    if (!scratch.TryMove(move.FromShelf, move.FromSlot, move.ToShelf, out var result))
                    {
                        throw new InvalidOperationException($"Solver tried an illegal move {move}: {result}.");
                    }
                    generated++;
                    if (scratch.IsWon)
                    {
                        // Only in a node's first round: a won child has f = depth + 1 <= f of the node (h >= 1).
                        var line = new List<SolverMove>();
                        tree.LineTo(index, line);
                        line.Add(move);
                        return new SolveResult(Solvability.Solvable, line, expanded, generated,
                            stopwatch.Elapsed.TotalSeconds, null);
                    }
                    if (!scratch.HasLegalMove()) continue; // stuck: can never win

                    StateKey cheap = default;
                    bool keyed = false;
                    int h = 0;
                    if (!reference)
                    {
                        h = context.ChildHeuristic(scratch, move);
                        if (predicted >= 0 && h != predicted)
                        {
                            throw new InvalidOperationException(
                                $"Solver predicted heuristic {predicted} for {move} but the Core board gives {h}.");
                        }
                        if (h < 0)
                        {
                            // A clear: read the whole board.
                            cheap = context.CheapKey(scratch);
                            keyed = true;
                            h = context.HeuristicOfLastRead();
                        }
                    }
                    int f = childDepth + h;
                    if (f <= previous) continue; // pushed (or found a duplicate) in an earlier round
                    if (f > threshold)
                    {
                        if (f < putOff) putOff = f;
                        continue;
                    }

                    if (!keyed) cheap = reference ? context.CheapKey(scratch) : context.ChildCheapKey();
                    if (seen.TryGetValue(cheap, out int known) && known <= childDepth) continue;
                    seen[cheap] = childDepth;

                    int child = tree.Add(index, move, childDepth);
                    pushedF.Add(f);
                    lastRound.Add(-1);
                    open.Push(f, childDepth, child);
                }

                if (putOff != int.MaxValue)
                {
                    // Come back for the children put off when the search reaches their f.
                    lastRound[index] = threshold;
                    pushedF[index] = putOff;
                    open.Push(putOff, depth, index);
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
