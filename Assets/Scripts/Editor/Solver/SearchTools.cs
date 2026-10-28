using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Solver
{
    /// <summary>
    /// Search nodes stored as (parent, move, depth) only; a node's board is rebuilt by replaying its line from the
    /// search root (<see cref="BoardCache"/>), which keeps memory small enough for 14-shelf levels.
    /// </summary>
    internal sealed class SearchTree
    {
        public readonly List<int> Parent = new List<int>();
        public readonly List<SolverMove> Move = new List<SolverMove>();
        public readonly List<int> Depth = new List<int>();

        public int Count => Parent.Count;

        /// <summary>Adds a node and returns its index.</summary>
        public int Add(int parent, SolverMove move, int depth)
        {
            Parent.Add(parent);
            Move.Add(move);
            Depth.Add(depth);
            return Parent.Count - 1;
        }

        /// <summary>The moves from the root to node <paramref name="index"/>, in play order.</summary>
        public void LineTo(int index, List<SolverMove> into)
        {
            into.Clear();
            for (int i = index; Parent[i] >= 0; i = Parent[i]) into.Add(Move[i]);
            into.Reverse();
        }
    }

    /// <summary>
    /// Rebuilds a node's board by replaying its line with the Core rules. The boards of recently rebuilt nodes are kept,
    /// so the replay starts from the nearest kept ancestor (best-first search with depth tie-breaking usually expands a
    /// child of a recent node). Kept boards are never moved on: callers only read them or copy them.
    /// </summary>
    internal sealed class BoardCache
    {
        private readonly int _mask;
        private readonly int[] _index;
        private readonly Board[] _boards;

        public BoardCache(int size = 4096)
        {
            _mask = size - 1;
            _index = new int[size];
            _boards = new Board[size];
            for (int i = 0; i < size; i++) _index[i] = -1;
        }

        public Board Get(Board root, SearchTree tree, int index, List<SolverMove> scratch)
        {
            scratch.Clear();
            Board start = root;
            for (int i = index; i > 0; i = tree.Parent[i])
            {
                int slot = i & _mask;
                if (_index[slot] == i)
                {
                    start = _boards[slot];
                    break;
                }
                scratch.Add(tree.Move[i]);
            }
            if (scratch.Count == 0) return start;

            var board = start.Clone();
            for (int k = scratch.Count - 1; k >= 0; k--)
            {
                var move = scratch[k];
                if (!board.TryMove(move.FromShelf, move.FromSlot, move.ToShelf, out var result))
                {
                    throw new InvalidOperationException($"Replay rejected {move}: {result}.");
                }
            }

            int own = index & _mask;
            _index[own] = index;
            _boards[own] = board;
            return board;
        }
    }

    /// <summary>Binary heap ordered by f ascending, then depth descending, then insertion order (deterministic).</summary>
    internal sealed class MinHeap
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
