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
        private struct Node
        {
            public int Parent;
            public SolverMove Move;
            public int Depth;
            public StateKey Key;
        }

        public static SolveResult Solve(Board root, IReadOnlyList<bool> pinnedShelves = null, SolverOptions options = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            options = options ?? SolverOptions.Default;
            return Solve(new SearchContext(root, pinnedShelves, !options.ReferenceMode), options);
        }

        internal static SolveResult Solve(SearchContext context, SolverOptions options)
        {
            var stopwatch = Stopwatch.StartNew();
            var root = context.Root;
            if (root.IsWon)
            {
                return new SolveResult(Solvability.Solvable, Array.Empty<SolverMove>(), 0, 0, 0, null);
            }

            var nodes = new List<Node>();
            var bestDepth = new Dictionary<StateKey, int>();
            var open = new MinHeap();
            var moves = new List<SolverMove>();
            var replay = new List<SolverMove>();

            bool reference = options.ReferenceMode;
            var rootKey = context.Key(root);
            nodes.Add(new Node { Parent = -1, Depth = 0, Key = rootKey });
            bestDepth[rootKey] = 0;
            open.Push(reference ? 0 : context.Heuristic(root), 0, 0);
            long expanded = 0;
            long generated = 1;

            while (open.Count > 0)
            {
                int index = open.Pop();
                var node = nodes[index];
                if (bestDepth[node.Key] < node.Depth) continue;

                expanded++;
                if (expanded > options.MaxExpandedNodes)
                {
                    return Unknown($"node budget of {options.MaxExpandedNodes} expanded states ran out", expanded, generated, stopwatch);
                }
                if ((expanded & 255) == 0 && stopwatch.Elapsed.TotalSeconds > options.TimeLimitSeconds)
                {
                    return Unknown($"time limit of {options.TimeLimitSeconds:0.#} s ran out", expanded, generated, stopwatch);
                }

                var board = Replay(root, nodes, index, replay);
                context.GenerateMoves(board, moves, !reference);
                int childDepth = node.Depth + 1;
                for (int m = 0; m < moves.Count; m++)
                {
                    var child = SearchContext.Apply(board, moves[m]);
                    generated++;
                    if (child.IsWon)
                    {
                        var line = new List<SolverMove>();
                        LineTo(nodes, index, line);
                        line.Add(moves[m]);
                        return new SolveResult(Solvability.Solvable, line, expanded, generated,
                            stopwatch.Elapsed.TotalSeconds, null);
                    }

                    var key = context.Key(child);
                    if (bestDepth.TryGetValue(key, out int known) && known <= childDepth) continue;
                    bestDepth[key] = childDepth;
                    if (!child.HasLegalMove()) continue; // stuck: can never win

                    nodes.Add(new Node { Parent = index, Move = moves[m], Depth = childDepth, Key = key });
                    open.Push(childDepth + (reference ? 0 : context.Heuristic(child)), childDepth, nodes.Count - 1);
                }
            }

            return new SolveResult(Solvability.Unsolvable, null, expanded, generated, stopwatch.Elapsed.TotalSeconds, null);
        }

        private static SolveResult Unknown(string reason, long expanded, long generated, Stopwatch stopwatch)
        {
            return new SolveResult(Solvability.Unknown, null, expanded, generated, stopwatch.Elapsed.TotalSeconds, reason);
        }

        private static void LineTo(List<Node> nodes, int index, List<SolverMove> into)
        {
            into.Clear();
            for (int i = index; nodes[i].Parent >= 0; i = nodes[i].Parent) into.Add(nodes[i].Move);
            into.Reverse();
        }

        private static Board Replay(Board root, List<Node> nodes, int index, List<SolverMove> scratch)
        {
            LineTo(nodes, index, scratch);
            var board = root.Clone();
            for (int i = 0; i < scratch.Count; i++)
            {
                var move = scratch[i];
                if (!board.TryMove(move.FromShelf, move.FromSlot, move.ToShelf, out var result))
                {
                    throw new InvalidOperationException($"Replay rejected {move}: {result}.");
                }
            }
            return board;
        }

        /// <summary>Binary heap ordered by f ascending, then depth descending, then insertion order (deterministic).</summary>
        private sealed class MinHeap
        {
            private struct Entry
            {
                public int F;
                public int Depth;
                public int Index;
            }

            private readonly List<Entry> _items = new List<Entry>();

            public int Count => _items.Count;

            private static bool Less(Entry a, Entry b)
            {
                if (a.F != b.F) return a.F < b.F;
                if (a.Depth != b.Depth) return a.Depth > b.Depth;
                return a.Index < b.Index;
            }

            public void Push(int f, int depth, int index)
            {
                _items.Add(new Entry { F = f, Depth = depth, Index = index });
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (!Less(_items[i], _items[parent])) break;
                    (_items[i], _items[parent]) = (_items[parent], _items[i]);
                    i = parent;
                }
            }

            public int Pop()
            {
                int result = _items[0].Index;
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1;
                    if (left >= _items.Count) break;
                    int right = left + 1;
                    int smallest = right < _items.Count && Less(_items[right], _items[left]) ? right : left;
                    if (!Less(_items[smallest], _items[i])) break;
                    (_items[i], _items[smallest]) = (_items[smallest], _items[i]);
                    i = smallest;
                }
                return result;
            }
        }
    }
}
