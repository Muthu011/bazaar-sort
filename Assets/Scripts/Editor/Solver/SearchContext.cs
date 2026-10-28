using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Core;

namespace Game.Solver
{
    /// <summary>
    /// Reads <see cref="Board"/> states for the search. It never decides what a move does: moves are always applied
    /// with <see cref="Board.TryMove(int,int,int)"/> on a <see cref="Board.Clone"/>, and legality always comes from
    /// <see cref="Board.CanMove"/>, so the solver cannot disagree with the game.
    ///
    /// Canonical state key. Under the Core rules the position of an item inside a row never matters (a move fills the
    /// leftmost free slot, a triple clears wherever it sits, a layer moves forward only when the front row is empty), so a
    /// shelf's state is its front row as a multiset plus its remaining hidden layers (each as a multiset, in order).
    /// Shelves of kind <c>normal</c> are interchangeable, so the key sorts their signatures and permutations of
    /// identical shelves are visited once. Any other kind (locked, conveyor, ...) is pinned to its index, so its
    /// position and lock state are always part of the key (a lock's remaining count follows from the clears made,
    /// which the remaining items already fix).
    /// </summary>
    internal sealed class SearchContext
    {
        private const ulong FrontSeed = 0x51A7E0F1C3D2B4A5UL;
        private const ulong KeySeedA = 0x0123456789ABCDEFUL;
        private const ulong KeySeedB = 0xFEDCBA9876543210UL;

        private readonly Dictionary<string, int> _typeIds = new Dictionary<string, int>(StringComparer.Ordinal);
        // Fast path: items are only ever moved, never created, so every string instance on any later board was already
        // on the root board. Looking them up by reference skips hashing the text.
        private readonly Dictionary<string, int> _instanceIds = new Dictionary<string, int>(ReferenceComparer.Instance);
        private readonly bool[] _pinned;
        // _hiddenHash[shelf][r]: hash of the last r hidden layers of that shelf at the root, i.e. its remaining
        // hidden layers when HiddenLayerCount == r. Hidden layers never change, they are only consumed from the front.
        private readonly ulong[][] _hiddenHash;
        private readonly ulong[] _keyScratch;
        private readonly ulong[] _sigScratch;

        // Heuristic scratch, indexed by type id: _cellsWith[t * 4 + n] = cells holding exactly n items of type t.
        private readonly int[] _cellsWith;
        private readonly int[] _totals;
        private readonly List<int> _touched = new List<int>();

        private readonly bool _sortShelves;

        public SearchContext(Board root, IReadOnlyList<bool> pinned, bool sortShelves = true)
        {
            _sortShelves = sortShelves;
            if (root == null) throw new ArgumentNullException(nameof(root));
            Root = root;
            ShelfCount = root.ShelfCount;
            _pinned = new bool[ShelfCount];
            if (pinned != null)
            {
                if (pinned.Count != ShelfCount) throw new ArgumentException("One pinned flag per shelf.", nameof(pinned));
                for (int i = 0; i < ShelfCount; i++) _pinned[i] = pinned[i];
            }

            for (int i = 0; i < ShelfCount; i++)
            {
                var shelf = root.GetShelf(i);
                for (int slot = 0; slot < Shelf.SlotCount; slot++) Register(shelf.GetFrontItem(slot));
                for (int d = 0; d < shelf.HiddenLayerCount; d++)
                {
                    for (int slot = 0; slot < Shelf.SlotCount; slot++) Register(shelf.GetHiddenItem(d, slot));
                }
            }

            _hiddenHash = new ulong[ShelfCount][];
            for (int i = 0; i < ShelfCount; i++)
            {
                var shelf = root.GetShelf(i);
                int count = shelf.HiddenLayerCount;
                var table = new ulong[count + 1];
                for (int r = 0; r <= count; r++)
                {
                    ulong h = (ulong)r;
                    for (int d = count - r; d < count; d++)
                    {
                        GetSorted(TypeId(shelf.GetHiddenItem(d, 0)), TypeId(shelf.GetHiddenItem(d, 1)),
                            TypeId(shelf.GetHiddenItem(d, 2)), out int x, out int y, out int z);
                        h = Hashing.Step(Hashing.Step(Hashing.Step(h, (ulong)x), (ulong)y), (ulong)z);
                    }
                    table[r] = h;
                }
                _hiddenHash[i] = table;
            }

            _keyScratch = new ulong[ShelfCount];
            _sigScratch = new ulong[ShelfCount];
            _cellsWith = new int[(TypeCount + 1) * 4];
            _totals = new int[TypeCount + 1];
        }

        public Board Root { get; }
        public int ShelfCount { get; }

        /// <summary>Distinct item types on the board; type ids run 1..TypeCount (0 is an empty slot).</summary>
        public int TypeCount => _typeIds.Count;

        private void Register(string item)
        {
            if (item == null || _instanceIds.ContainsKey(item)) return;
            if (!_typeIds.TryGetValue(item, out int id))
            {
                id = _typeIds.Count + 1;
                _typeIds.Add(item, id);
            }
            _instanceIds.Add(item, id);
        }

        public int TypeId(string item)
        {
            if (item == null) return 0;
            if (_instanceIds.TryGetValue(item, out int id)) return id;
            if (_typeIds.TryGetValue(item, out id)) return id;
            throw new InvalidOperationException($"Item '{item}' was not on the board the search started from.");
        }

        private sealed class ReferenceComparer : IEqualityComparer<string>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(string x, string y) => ReferenceEquals(x, y);
            public int GetHashCode(string obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static void GetSorted(int a, int b, int c, out int x, out int y, out int z)
        {
            if (a > b) { int t = a; a = b; b = t; }
            if (b > c) { int t = b; b = c; c = t; }
            if (a > b) { int t = a; a = b; b = t; }
            x = a;
            y = b;
            z = c;
        }

        /// <summary>Everything about shelf <paramref name="index"/> that can affect the rest of the game.</summary>
        public ulong ShelfSignature(Board board, int index)
        {
            var shelf = board.GetShelf(index);
            return Signature(index, TypeId(shelf.GetFrontItem(0)), TypeId(shelf.GetFrontItem(1)),
                TypeId(shelf.GetFrontItem(2)), shelf.HiddenLayerCount);
        }

        private ulong Signature(int index, int a, int b, int c, int hiddenRemaining)
        {
            GetSorted(a, b, c, out int x, out int y, out int z);
            ulong h = Hashing.Step(Hashing.Step(Hashing.Step(FrontSeed, (ulong)x), (ulong)y), (ulong)z);
            h = Hashing.Step(h, _hiddenHash[index][hiddenRemaining]);
            if (_pinned[index]) h = Hashing.Step(h, 0xA5A5A5A500000000UL + (ulong)index);
            return h;
        }

        /// <summary>Canonical key of a board state (normal shelves sorted, pinned shelves kept apart by index).</summary>
        public StateKey Key(Board board)
        {
            for (int i = 0; i < ShelfCount; i++) _keyScratch[i] = ShelfSignature(board, i);
            return KeyFromScratch();
        }

        private StateKey KeyFromScratch()
        {
            if (_sortShelves) Array.Sort(_keyScratch);
            ulong a = KeySeedA;
            ulong b = KeySeedB;
            for (int i = 0; i < ShelfCount; i++)
            {
                a = Hashing.Step(a, _keyScratch[i]);
                b = Hashing.Step(b ^ 0x5bd1e995UL, _keyScratch[i] * 0x9E3779B97F4A7C15UL);
            }
            return new StateKey(a, b);
        }

        /// <summary>
        /// Admissible and consistent lower bound on the moves left. A "cell" is one row of one shelf (the front row or
        /// one hidden layer). Items that are never moved again can only clear together with items from the same cell, and
        /// a type with 3m items left clears in m triples, so at most the m largest cell counts of a type can stay put; every
        /// other item of that type needs at least one move. One move changes one item's cell, so the bound drops by at
        /// most 1 per move. A board that is not won always scores at least 1 (a front row never holds a triple).
        /// </summary>
        public int Heuristic(Board board)
        {
            _touched.Clear();

            for (int i = 0; i < ShelfCount; i++)
            {
                var shelf = board.GetShelf(i);
                AddCell(shelf.GetFrontItem(0), shelf.GetFrontItem(1), shelf.GetFrontItem(2));
                for (int d = 0; d < shelf.HiddenLayerCount; d++)
                {
                    AddCell(shelf.GetHiddenItem(d, 0), shelf.GetHiddenItem(d, 1), shelf.GetHiddenItem(d, 2));
                }
            }

            int h = 0;
            for (int i = 0; i < _touched.Count; i++)
            {
                int t = _touched[i];
                int total = _totals[t];
                int triples = total / GameRules.MatchCount;
                int keep = 0;
                // Take the fullest cells first: 3s, then 2s, then 1s.
                for (int n = 3; n >= 1 && triples > 0; n--)
                {
                    int take = Math.Min(triples, _cellsWith[t * 4 + n]);
                    keep += take * n;
                    triples -= take;
                }
                h += total - keep;
                _totals[t] = 0;
                _cellsWith[t * 4 + 1] = 0;
                _cellsWith[t * 4 + 2] = 0;
                _cellsWith[t * 4 + 3] = 0;
            }
            return h;
        }

        private void Count(int type, int n)
        {
            if (_totals[type] == 0) _touched.Add(type);
            _totals[type] += n;
            _cellsWith[type * 4 + n]++;
        }

        private void AddCell(string a, string b, string c)
        {
            int x = TypeId(a), y = TypeId(b), z = TypeId(c);
            if (x != 0) Count(x, 1 + (y == x ? 1 : 0) + (z == x ? 1 : 0));
            if (y != 0 && y != x) Count(y, 1 + (z == y ? 1 : 0));
            if (z != 0 && z != x && z != y) Count(z, 1);
        }

        /// <summary>
        /// Legal moves from <paramref name="board"/>, checked with <see cref="Board.CanMove"/>. Items of the same type in
        /// one front row give one move each (they are interchangeable). With <paramref name="mergeEquivalentShelves"/>,
        /// shelves with identical signatures are also merged as sources and as targets, because they lead to the same
        /// canonical state; the shortest-path and dead-end searches use that, the simulated player does not.
        /// </summary>
        public void GenerateMoves(Board board, List<SolverMove> into, bool mergeEquivalentShelves)
        {
            into.Clear();
            if (mergeEquivalentShelves)
            {
                for (int i = 0; i < ShelfCount; i++) _sigScratch[i] = ShelfSignature(board, i);
            }

            for (int from = 0; from < ShelfCount; from++)
            {
                var shelf = board.GetShelf(from);
                if (shelf.IsFrontEmpty) continue;
                if (mergeEquivalentShelves && HasEarlierTwin(from, from, -1)) continue;

                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    string item = shelf.GetFrontItem(slot);
                    if (item == null) continue;
                    bool repeat = false;
                    for (int s = 0; s < slot; s++)
                    {
                        if (string.Equals(shelf.GetFrontItem(s), item, StringComparison.Ordinal)) repeat = true;
                    }
                    if (repeat) continue;

                    for (int to = 0; to < ShelfCount; to++)
                    {
                        if (to == from) continue;
                        if (board.CanMove(from, slot, to) != MoveResult.Ok) continue;
                        if (mergeEquivalentShelves && HasEarlierTwin(to, to, from)) continue;
                        into.Add(new SolverMove(from, slot, to));
                    }
                }
            }
        }

        // True when a shelf before `limit` (other than `exclude`) has the same signature as `index`.
        private bool HasEarlierTwin(int index, int limit, int exclude)
        {
            for (int j = 0; j < limit; j++)
            {
                if (j != exclude && _sigScratch[j] == _sigScratch[index]) return true;
            }
            return false;
        }

        /// <summary>
        /// Necessary condition for a move to cause a clear, read from the board without changing it: either the target
        /// row holds two of the moved type (the third arrives), or the source row empties and reveals a layer that may be
        /// a triple. The caller confirms by applying the move with <see cref="Board.TryMove(int,int,int)"/>.
        /// </summary>
        public static bool MayClear(Board board, SolverMove move)
        {
            var source = board.GetShelf(move.FromShelf);
            string item = source.GetFrontItem(move.FromSlot);
            if (source.FrontItemCount == 1 && source.HiddenLayerCount > 0) return true;
            var target = board.GetShelf(move.ToShelf);
            int same = 0;
            for (int s = 0; s < Shelf.SlotCount; s++)
            {
                if (string.Equals(target.GetFrontItem(s), item, StringComparison.Ordinal)) same++;
            }
            return same == GameRules.MatchCount - 1;
        }

        /// <summary>A copy of <paramref name="board"/> with <paramref name="move"/> applied by the Core rules.</summary>
        public static Board Apply(Board board, SolverMove move)
        {
            var child = board.Clone();
            if (!child.TryMove(move.FromShelf, move.FromSlot, move.ToShelf, out var result))
            {
                throw new InvalidOperationException($"Solver tried an illegal move {move}: {result}.");
            }
            return child;
        }
    }
}
