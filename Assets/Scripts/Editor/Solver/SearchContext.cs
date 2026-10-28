using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Solver
{
    /// <summary>
    /// Reads <see cref="Board"/> states for the search. It never decides what a move does: moves are always applied
    /// with <see cref="Board.TryMove(int,int,int)"/> on a <see cref="Board.Clone"/>, and legality always comes from
    /// <see cref="Board.CanMove"/>, so the solver cannot disagree with the game.
    ///
    /// Under the Core rules the position of an item inside a row never matters (a move fills the leftmost free slot, a
    /// triple clears wherever it sits, a layer moves forward only when the front row is empty), so a shelf's state is its
    /// front row as a multiset, its remaining hidden layers (each as a multiset, in order) and its lock counter (clears
    /// still needed before it opens; 0 when open).
    ///
    /// Two keys are built from that (issue #31):
    /// <list type="bullet">
    /// <item><see cref="Key"/>, the visited-state key of the shortest-path and dead-end searches. It is canonical: it
    /// does not change when shelves are reordered (locked shelves too: their lock counter is in the key) or when item
    /// types are renamed, so states that are the same puzzle up to those symmetries are explored once. Types are
    /// relabelled by an invariant colour (colour refinement over the cells they sit in); if some types still tie, every
    /// labelling of the tied types is tried and the smallest key wins, so the key stays exact. Only if the ties are too
    /// many to try does it fall back to the board's own type ids (still exact, just merges less).</item>
    /// <item><see cref="PlayKey"/>, what the simulated player recognises as "a board I have seen": normal shelves
    /// sorted, any other kind pinned to its index, types by name. It is the key the play policy and the stored
    /// estWinRate/deadEndRate numbers were defined with, so it must not change with search tuning.</item>
    /// </list>
    /// </summary>
    internal sealed class SearchContext
    {
        private const ulong FrontSeed = 0x51A7E0F1C3D2B4A5UL;
        private const ulong KeySeedA = 0x0123456789ABCDEFUL;
        private const ulong KeySeedB = 0xFEDCBA9876543210UL;
        private const ulong FallbackTag = 0x0F0F0F0F0F0F0F0FUL;
        private const ulong PinTag = 0xA5A5A5A500000000UL;
        private const ulong LockTag = 0x3C3C3C3C00000000UL;

        /// <summary>Most labellings tried when type colours tie (product of the tied groups' factorials).</summary>
        private const int MaxTieLabellings = 24;

        // One string instance per type (index = id - 1). The root is rebuilt from these, so every board in a search
        // holds only these instances and an id is found by reference in a short scan.
        private readonly string[] _types;
        private readonly Dictionary<string, int> _typeIds = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly bool[] _pinned;
        private readonly bool[] _playPinned;

        // Root hidden layers per shelf, each layer's ids sorted: _hiddenIds[shelf][depth * 3 + k].
        private readonly int[][] _hiddenIds;
        private readonly int[] _rootHidden;
        // _hiddenHash[shelf][r]: hash of the last r hidden layers of that shelf at the root, i.e. its remaining
        // hidden layers when HiddenLayerCount == r. Hidden layers never change, they are only consumed from the front.
        private readonly ulong[][] _hiddenHash;
        // _hiddenCells[shelf][r]: heuristic cell entries (type * 4 + count) of those r layers.
        private readonly int[][][] _hiddenCells;
        // _hiddenCellMax[shelf][r]: sum over those r layers of the largest count of one type in the layer.
        private readonly int[][] _hiddenCellMax;

        // Snapshot of the board last read: sorted front ids, hidden layers left, lock counter.
        private readonly int[] _front;
        private readonly int[] _hidden;
        private readonly int[] _lock;

        private readonly ulong[] _keyScratch;
        private readonly ulong[] _sigScratch;

        // Canonical labelling scratch, indexed by type id.
        private readonly ulong[] _colour;
        private readonly ulong[] _colourNext;
        private readonly ulong[] _sortedColours;
        private readonly int[] _count;
        private readonly int[] _label;
        private readonly int[] _bestLabel;
        private readonly int[] _order;
        private readonly int[] _groupStart;
        private readonly int[] _groupSize;
        private readonly int[] _perm;
        private readonly ulong[] _cellAgg;
        private readonly ulong[] _shelfAgg;

        // Heuristic scratch, indexed by type id: _cellsWith[t * 4 + n] = cells holding exactly n items of type t.
        private readonly int[] _cellsWith;
        private readonly int[] _totals;
        private readonly List<int> _touched = new List<int>();

        private readonly bool _canonical;

        private const int CacheBits = 18;
        private readonly bool[] _cacheFilled = new bool[1 << CacheBits];
        private readonly ulong[] _cacheCheapA = new ulong[1 << CacheBits];
        private readonly ulong[] _cacheCheapB = new ulong[1 << CacheBits];
        private readonly ulong[] _cacheKeyA = new ulong[1 << CacheBits];
        private readonly ulong[] _cacheKeyB = new ulong[1 << CacheBits];

        /// <param name="root">The board the search starts from. It is copied, not changed.</param>
        /// <param name="pinned">
        /// Shelves whose position matters (a kind whose behaviour depends on where it is); null for none. Locked shelves
        /// need no pin: their lock counter is part of every key.
        /// </param>
        /// <param name="canonical">False for the reference search: no shelf sorting, no type relabelling.</param>
        public SearchContext(Board root, IReadOnlyList<bool> pinned, bool canonical = true)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _canonical = canonical;
            ShelfCount = root.ShelfCount;
            _pinned = new bool[ShelfCount];
            _playPinned = new bool[ShelfCount];
            if (pinned != null && pinned.Count != ShelfCount)
            {
                throw new ArgumentException("One pinned flag per shelf.", nameof(pinned));
            }

            // Type ids by first appearance (front row, then hidden layers, shelf by shelf).
            var names = new List<string>();
            for (int i = 0; i < ShelfCount; i++)
            {
                var shelf = root.GetShelf(i);
                for (int slot = 0; slot < Shelf.SlotCount; slot++) Register(shelf.GetFrontItem(slot), names);
                for (int d = 0; d < shelf.HiddenLayerCount; d++)
                {
                    for (int slot = 0; slot < Shelf.SlotCount; slot++) Register(shelf.GetHiddenItem(d, slot), names);
                }
            }
            _types = names.ToArray();

            // Rebuild the root from the shared instances (same layout, same lock counters).
            var shelves = new Shelf[ShelfCount];
            _hiddenIds = new int[ShelfCount][];
            _rootHidden = new int[ShelfCount];
            for (int i = 0; i < ShelfCount; i++)
            {
                var shelf = root.GetShelf(i);
                int hiddenCount = shelf.HiddenLayerCount;
                var layers = new string[hiddenCount + 1][];
                layers[0] = new string[Shelf.SlotCount];
                for (int slot = 0; slot < Shelf.SlotCount; slot++) layers[0][slot] = Canon(shelf.GetFrontItem(slot));
                var ids = new int[hiddenCount * Shelf.SlotCount];
                for (int d = 0; d < hiddenCount; d++)
                {
                    layers[d + 1] = new string[Shelf.SlotCount];
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        layers[d + 1][slot] = Canon(shelf.GetHiddenItem(d, slot));
                    }
                    GetSorted(TypeId(layers[d + 1][0]), TypeId(layers[d + 1][1]), TypeId(layers[d + 1][2]),
                        out ids[d * 3], out ids[d * 3 + 1], out ids[d * 3 + 2]);
                }
                shelves[i] = new Shelf(layers, shelf.Id, shelf.LockClearsLeft);
                _hiddenIds[i] = ids;
                _rootHidden[i] = hiddenCount;
                _pinned[i] = pinned != null && pinned[i];
                _playPinned[i] = _pinned[i] || shelf.LockClears > 0;
            }
            Root = new Board(shelves);

            _hiddenHash = new ulong[ShelfCount][];
            _hiddenCells = new int[ShelfCount][][];
            _hiddenCellMax = new int[ShelfCount][];
            for (int i = 0; i < ShelfCount; i++)
            {
                int count = _rootHidden[i];
                var table = new ulong[count + 1];
                var cells = new int[count + 1][];
                var cellMax = new int[count + 1];
                var entries = new List<int>();
                for (int r = 0; r <= count; r++)
                {
                    ulong h = (ulong)r;
                    entries.Clear();
                    for (int d = count - r; d < count; d++)
                    {
                        int x = _hiddenIds[i][d * 3], y = _hiddenIds[i][d * 3 + 1], z = _hiddenIds[i][d * 3 + 2];
                        h = Hashing.Step(Hashing.Step(Hashing.Step(h, (ulong)x), (ulong)y), (ulong)z);
                        AddCellEntries(x, y, z, entries);
                        cellMax[r] += CellMax(x, y, z);
                    }
                    table[r] = h;
                    cells[r] = entries.ToArray();
                }
                _hiddenHash[i] = table;
                _hiddenCells[i] = cells;
                _hiddenCellMax[i] = cellMax;
            }

            int types = _types.Length + 1;
            _front = new int[ShelfCount * Shelf.SlotCount];
            _hidden = new int[ShelfCount];
            _lock = new int[ShelfCount];
            _keyScratch = new ulong[ShelfCount];
            _sigScratch = new ulong[ShelfCount];
            _colour = new ulong[types];
            _colourNext = new ulong[types];
            _sortedColours = new ulong[types];
            _count = new int[types];
            _label = new int[types];
            _bestLabel = new int[types];
            _order = new int[types];
            _groupStart = new int[types];
            _groupSize = new int[types];
            _perm = new int[types];
            int cellCount = 0;
            for (int i = 0; i < ShelfCount; i++) cellCount += 1 + _rootHidden[i];
            _cellAgg = new ulong[cellCount];
            _shelfAgg = new ulong[ShelfCount];
            _cellsWith = new int[types * 4];
            _totals = new int[types];
        }

        /// <summary>
        /// The board the search starts from: a copy of the given root in which every item of a type is the same string
        /// instance. Same layout and lock counters, so moves and lines are interchangeable with the original.
        /// </summary>
        public Board Root { get; }

        /// <summary>
        /// A context for another thread over the same puzzle: same type ids, pins and keys, its own scratch space and
        /// its own root board (boards of one context must not be used with the other).
        /// </summary>
        public SearchContext CreateSibling()
        {
            var sibling = new SearchContext(Root, _pinned, _canonical);
            Array.Copy(_playPinned, sibling._playPinned, _playPinned.Length);
            return sibling;
        }

        public int ShelfCount { get; }

        /// <summary>Distinct item types on the board; type ids run 1..TypeCount (0 is an empty slot).</summary>
        public int TypeCount => _types.Length;

        private void Register(string item, List<string> names)
        {
            if (item == null || _typeIds.ContainsKey(item)) return;
            names.Add(item);
            _typeIds.Add(item, names.Count);
        }

        private string Canon(string item) => item == null ? null : _types[_typeIds[item] - 1];

        public int TypeId(string item)
        {
            if (item == null) return 0;
            var types = _types;
            for (int k = 0; k < types.Length; k++)
            {
                if (ReferenceEquals(types[k], item)) return k + 1;
            }
            if (_typeIds.TryGetValue(item, out int id)) return id;
            throw new InvalidOperationException($"Item '{item}' was not on the board the search started from.");
        }

        private int FastId(string item)
        {
            if (item == null) return 0;
            var types = _types;
            for (int k = 0; k < types.Length; k++)
            {
                if ((object)types[k] == (object)item) return k + 1;
            }
            return TypeId(item);
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

        // ---------------------------------------------------------------------------------------------------------
        // Snapshot

        /// <summary>Reads front rows, hidden layers left and lock counters of <paramref name="board"/>.</summary>
        private void Read(Board board)
        {
            if (board.ShelfCount != ShelfCount) throw new ArgumentException("Board does not match the search root.");
            for (int i = 0; i < ShelfCount; i++)
            {
                var shelf = board.GetShelf(i);
                GetSorted(FastId(shelf.GetFrontItem(0)), FastId(shelf.GetFrontItem(1)), FastId(shelf.GetFrontItem(2)),
                    out _front[i * 3], out _front[i * 3 + 1], out _front[i * 3 + 2]);
                int r = shelf.HiddenLayerCount;
                if (r > _rootHidden[i]) throw new ArgumentException("Board does not descend from the search root.");
                _hidden[i] = r;
                _lock[i] = shelf.LockClearsLeft;
            }
        }

        // Sorted ids of cell `cell` of shelf `i` in the snapshot: cell 0 is the front row, cell p >= 1 the hidden layer
        // at depth p - 1.
        private void CellIds(int i, int cell, out int x, out int y, out int z)
        {
            if (cell == 0)
            {
                x = _front[i * 3];
                y = _front[i * 3 + 1];
                z = _front[i * 3 + 2];
                return;
            }
            int baseIndex = (_rootHidden[i] - _hidden[i] + cell - 1) * 3;
            var ids = _hiddenIds[i];
            x = ids[baseIndex];
            y = ids[baseIndex + 1];
            z = ids[baseIndex + 2];
        }

        // ---------------------------------------------------------------------------------------------------------
        // Keys

        /// <summary>
        /// Canonical visited-state key (see the class summary): independent of shelf order and type names, includes
        /// every lock counter.
        /// </summary>
        public StateKey Key(Board board)
        {
            Read(board);
            return CachedKeyFromSnapshot();
        }

        /// <summary><see cref="Key"/> and <see cref="Heuristic"/> from one read of the board.</summary>
        /// <summary>
        /// <see cref="Heuristic"/> of the board last read (by <see cref="Key(Board)"/>, <see cref="CheapKey"/> or
        /// <see cref="PlayKey"/>).
        /// </summary>
        public int HeuristicOfLastRead() => HeuristicFromSnapshot();

        /// <summary>
        /// Exact key that is cheap to build: normal shelves sorted (unless this is the reference search), the board's
        /// own type ids, lock counters included. Equal cheap keys mean equal boards up to shelf order; the canonical
        /// <see cref="Key"/> may merge more.
        /// </summary>
        public StateKey CheapKey(Board board)
        {
            Read(board);
            return CheapFromSnapshot();
        }

        private StateKey CheapFromSnapshot()
        {
            for (int i = 0; i < ShelfCount; i++)
            {
                // Exact packing of front ids (10 bits each), lock counter and pin, mixed with the hidden-layer hash.
                ulong packed = (uint)_front[i * 3] | ((ulong)(uint)_front[i * 3 + 1] << 10)
                               | ((ulong)(uint)_front[i * 3 + 2] << 20) | ((ulong)(uint)Math.Min(_lock[i], 1023) << 30)
                               | (_pinned[i] ? (ulong)(uint)(i + 1) << 40 : 0UL);
                _keyScratch[i] = Hashing.Step(_hiddenHash[i][_hidden[i]], packed);
            }
            if (_canonical) Array.Sort(_keyScratch);
            return CombineFast(KeySeedA ^ 0x2222UL, KeySeedB ^ 0x2222UL);
        }

        // Like Combine with one mix per shelf for the second half.
        private StateKey CombineFast(ulong a, ulong b)
        {
            for (int i = 0; i < ShelfCount; i++)
            {
                a = Hashing.Step(a, _keyScratch[i]);
                b = (b ^ _keyScratch[i]) * 0x9E3779B97F4A7C15UL + (ulong)i;
            }
            return new StateKey(a, Hashing.Mix(b));
        }

        /// <summary>The canonical key without the cache (tests compare the two).</summary>
        internal StateKey UncachedKey(Board board)
        {
            Read(board);
            return KeyFromSnapshot();
        }

        // Most children a search builds are boards it has built before by another move order. Those share the cheap
        // exact key (normal shelves sorted, the board's own type ids), so a direct-mapped cache from cheap key to
        // canonical key skips the relabelling for them. Equal cheap keys mean equal boards, so a hit is always right.
        private StateKey CachedKeyFromSnapshot()
        {
            if (!_canonical) return KeyFromSnapshot();
            var cheap = CheapFromSnapshot();
            int slot = (int)(cheap.A >> (64 - CacheBits));
            if (_cacheFilled[slot] && _cacheCheapA[slot] == cheap.A && _cacheCheapB[slot] == cheap.B)
            {
                return new StateKey(_cacheKeyA[slot], _cacheKeyB[slot]);
            }
            var key = KeyFromSnapshot();
            _cacheFilled[slot] = true;
            _cacheCheapA[slot] = cheap.A;
            _cacheCheapB[slot] = cheap.B;
            _cacheKeyA[slot] = key.A;
            _cacheKeyB[slot] = key.B;
            return key;
        }

        /// <summary>
        /// The simulated player's notion of "the same board" (see the class summary). Lock counters are included; they
        /// follow from the clears made, so this groups boards exactly as the key the stored numbers were made with.
        /// </summary>
        public StateKey PlayKey(Board board)
        {
            Read(board);
            for (int i = 0; i < ShelfCount; i++)
            {
                ulong h = Hashing.Step(Hashing.Step(Hashing.Step(FrontSeed, (ulong)_front[i * 3]),
                    (ulong)_front[i * 3 + 1]), (ulong)_front[i * 3 + 2]);
                h = Hashing.Step(h, _hiddenHash[i][_hidden[i]]);
                if (_playPinned[i]) h = Hashing.Step(h, PinTag + (ulong)i);
                if (_lock[i] > 0) h = Hashing.Step(h, LockTag + (ulong)_lock[i]);
                _keyScratch[i] = h;
            }
            Array.Sort(_keyScratch);
            return Combine(KeySeedA ^ 0x1111UL, KeySeedB);
        }

        /// <summary>Everything about shelf <paramref name="index"/> that can affect the rest of the game.</summary>
        public ulong ShelfSignature(Board board, int index)
        {
            var shelf = board.GetShelf(index);
            GetSorted(TypeId(shelf.GetFrontItem(0)), TypeId(shelf.GetFrontItem(1)), TypeId(shelf.GetFrontItem(2)),
                out int x, out int y, out int z);
            ulong h = Hashing.Step(Hashing.Step(Hashing.Step(FrontSeed, (ulong)x), (ulong)y), (ulong)z);
            h = Hashing.Step(h, _hiddenHash[index][shelf.HiddenLayerCount]);
            if (_pinned[index]) h = Hashing.Step(h, PinTag + (ulong)index);
            int lockLeft = shelf.LockClearsLeft;
            if (lockLeft > 0) h = Hashing.Step(h, LockTag + (ulong)lockLeft);
            return h;
        }

        private StateKey Combine(ulong a, ulong b)
        {
            for (int i = 0; i < ShelfCount; i++)
            {
                a = Hashing.Step(a, _keyScratch[i]);
                b = Hashing.Step(b ^ 0x5bd1e995UL, _keyScratch[i] * 0x9E3779B97F4A7C15UL);
            }
            return new StateKey(a, b);
        }

        private StateKey KeyFromSnapshot()
        {
            int typeCount = _types.Length;
            if (!_canonical)
            {
                for (int t = 0; t <= typeCount; t++) _label[t] = t;
                return Encode(false);
            }

            // Which types are left.
            for (int t = 0; t <= typeCount; t++) _count[t] = 0;
            for (int i = 0; i < ShelfCount; i++)
            {
                int cells = 1 + _hidden[i];
                for (int c = 0; c < cells; c++)
                {
                    CellIds(i, c, out int x, out int y, out int z);
                    _count[x]++;
                    _count[y]++;
                    _count[z]++;
                }
            }

            Refine();

            // Order the types left by colour (insertion sort: a few dozen at most), then find groups of equal colour.
            int present = 0;
            for (int t = 1; t <= typeCount; t++)
            {
                if (_count[t] == 0) continue;
                int k = present++;
                while (k > 0 && _colour[_order[k - 1]] > _colour[t])
                {
                    _order[k] = _order[k - 1];
                    k--;
                }
                _order[k] = t;
            }

            int groups = 0;
            long labellings = 1;
            for (int k = 0; k < present;)
            {
                int end = k + 1;
                while (end < present && _colour[_order[end]] == _colour[_order[k]]) end++;
                _groupStart[groups] = k;
                _groupSize[groups] = end - k;
                for (int n = 2; n <= end - k && labellings <= MaxTieLabellings; n++) labellings *= n;
                groups++;
                k = end;
            }

            _label[0] = 0;
            if (labellings == 1)
            {
                for (int k = 0; k < present; k++) _label[_order[k]] = k + 1;
                return Encode(true);
            }

            if (labellings > MaxTieLabellings)
            {
                // Too many ties to try: the board's own ids (exact, merges less). Tagged so it never meets a canonical key.
                for (int t = 0; t <= typeCount; t++) _label[t] = t;
                var key = Encode(true);
                return new StateKey(key.A ^ FallbackTag, key.B);
            }

            // Try every labelling of the tied groups and keep the smallest key.
            StateKey best = default;
            bool haveBest = false;
            for (long n = 0; n < labellings; n++)
            {
                long rest = n;
                for (int g = 0; g < groups; g++)
                {
                    int size = _groupSize[g];
                    int start = _groupStart[g];
                    long fact = 1;
                    for (int f = 2; f <= size; f++) fact *= f;
                    long index = rest % fact;
                    rest /= fact;
                    Unrank(index, size, _perm);
                    for (int m = 0; m < size; m++) _label[_order[start + m]] = start + _perm[m] + 1;
                }
                var candidate = Encode(true);
                if (!haveBest || candidate.A < best.A || (candidate.A == best.A && candidate.B < best.B))
                {
                    best = candidate;
                    haveBest = true;
                }
            }
            return best;
        }

        // The index-th permutation of 0..size-1 (factorial number system).
        private static void Unrank(long index, int size, int[] into)
        {
            for (int m = 0; m < size; m++) into[m] = m;
            for (int m = 0; m < size; m++)
            {
                long fact = 1;
                for (int f = 2; f < size - m; f++) fact *= f;
                int pick = (int)(index / fact);
                index %= fact;
                int value = into[m + pick];
                for (int s = m + pick; s > m; s--) into[s] = into[s - 1];
                into[m] = value;
            }
        }

        // Label-free description of a cell, packed exactly: shelf (hidden layers left, lock counter, pin), the cell's
        // depth, and its shape (how many empty slots, whether it holds a pair or a triple).
        private int CellShape(int i, int cell, int x, int y, int z)
        {
            int empty = (x == 0 ? 1 : 0) + (y == 0 ? 1 : 0) + (z == 0 ? 1 : 0);
            int shape = x == y && y == z ? 3 : (x == y || y == z) ? 2 : 1;
            int pin = _pinned[i] ? i + 1 : 0;
            return shape | (empty << 2) | (cell << 4) | (Math.Min(_hidden[i], 255) << 10)
                   | (Math.Min(_lock[i], 255) << 18) | (Math.Min(pin, 63) << 26);
        }

        private static ulong Contribution(ulong shape, int count) => Hashing.Mix(shape * 4 + (ulong)count + 0x9E37UL);

        /// <summary>
        /// Colour refinement: a type's colour starts as the multiset of (cell shape, how many of it there) over the
        /// cells it sits in, then (up to twice, while it still splits ties) absorbs the colours of the types it shares a
        /// cell and a shelf with. Colours never depend on type names or shelf order, so renamed or reordered boards give
        /// each type the same colour. Colours only steer the labelling; the key itself is the exact encoding.
        /// </summary>
        private void Refine()
        {
            int typeCount = _types.Length;
            for (int t = 0; t <= typeCount; t++) _colour[t] = 0;

            for (int i = 0; i < ShelfCount; i++)
            {
                int cells = 1 + _hidden[i];
                for (int c = 0; c < cells; c++)
                {
                    CellIds(i, c, out int x, out int y, out int z);
                    AddPerType(x, y, z, (ulong)CellShape(i, c, x, y, z), _colour);
                }
            }

            int present = 0;
            for (int t = 1; t <= typeCount; t++)
            {
                if (_count[t] > 0) present++;
            }
            int distinct = DistinctColours();
            for (int round = 0; round < 2 && distinct < present; round++)
            {
                // _colourNext first holds each type's mixed colour (0 stays 0 for empty slots).
                _colourNext[0] = 0;
                for (int t = 1; t <= typeCount; t++) _colourNext[t] = Hashing.Mix(_colour[t] + 0x2545F4914F6CDD1DUL);

                int cellIndex = 0;
                for (int i = 0; i < ShelfCount; i++)
                {
                    int cells = 1 + _hidden[i];
                    ulong shelfAgg = 0;
                    for (int c = 0; c < cells; c++)
                    {
                        CellIds(i, c, out int x, out int y, out int z);
                        ulong agg = _colourNext[x] + _colourNext[y] + _colourNext[z];
                        _cellAgg[cellIndex + c] = agg;
                        shelfAgg += agg * (ulong)(2 * c + 1);
                    }
                    _shelfAgg[i] = Hashing.Mix(shelfAgg);
                    cellIndex += cells;
                }

                for (int t = 0; t <= typeCount; t++) _colourNext[t] = Hashing.Mix(_colour[t] ^ 0x5851F42D4C957F2DUL);

                cellIndex = 0;
                for (int i = 0; i < ShelfCount; i++)
                {
                    int cells = 1 + _hidden[i];
                    for (int c = 0; c < cells; c++)
                    {
                        CellIds(i, c, out int x, out int y, out int z);
                        ulong shape = (ulong)CellShape(i, c, x, y, z)
                                      ^ (_cellAgg[cellIndex + c] * 0xD6E8FEB86659FD93UL) ^ _shelfAgg[i];
                        AddPerType(x, y, z, shape, _colourNext);
                    }
                    cellIndex += cells;
                }

                for (int t = 0; t <= typeCount; t++) _colour[t] = _colourNext[t];

                // Refinement only ever splits groups; once it stops splitting it never will again.
                int next = DistinctColours();
                if (next == distinct) break;
                distinct = next;
            }
        }

        // Number of distinct colours among the types left.
        private int DistinctColours()
        {
            int n = 0;
            for (int t = 1; t < _count.Length; t++)
            {
                if (_count[t] == 0) continue;
                ulong c = _colour[t];
                int k = n++;
                while (k > 0 && _sortedColours[k - 1] > c)
                {
                    _sortedColours[k] = _sortedColours[k - 1];
                    k--;
                }
                _sortedColours[k] = c;
            }
            int distinct = 0;
            for (int k = 0; k < n; k++)
            {
                if (k == 0 || _sortedColours[k] != _sortedColours[k - 1]) distinct++;
            }
            return distinct;
        }

        // Adds a contribution of (shape, n) to colours[t] once for each distinct type t in a sorted cell, n = how many
        // of t the cell holds.
        private static void AddPerType(int x, int y, int z, ulong shape, ulong[] colours)
        {
            if (x != 0) colours[x] += Contribution(shape, 1 + (y == x ? 1 : 0) + (z == x ? 1 : 0));
            if (y != 0 && y != x) colours[y] += Contribution(shape, 1 + (z == y ? 1 : 0));
            if (z != 0 && z != y) colours[z] += Contribution(shape, 1);
        }

        // Exact encoding of the snapshot under _label: each shelf as (pin, lock, layers left, front, hidden layers),
        // shelves sorted when requested. Each cell is packed exactly and mixed in with one full mix.
        private StateKey Encode(bool sortShelves)
        {
            for (int i = 0; i < ShelfCount; i++)
            {
                ulong head = (uint)Math.Min(_lock[i], 1023) | ((ulong)(uint)_hidden[i] << 10)
                             | (_pinned[i] ? (ulong)(uint)(i + 1) << 20 : 0UL);
                ulong h = Hashing.Mix(FrontSeed ^ head);
                int cells = 1 + _hidden[i];
                for (int c = 0; c < cells; c++)
                {
                    CellIds(i, c, out int x, out int y, out int z);
                    GetSorted(_label[x], _label[y], _label[z], out int a, out int b, out int d);
                    h = Hashing.Mix(h + ((uint)a | ((ulong)(uint)b << 10) | ((ulong)(uint)d << 20) | ((ulong)(uint)c << 30)));
                }
                _keyScratch[i] = h;
            }
            if (sortShelves) Array.Sort(_keyScratch);
            return CombineFast(KeySeedA, KeySeedB);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Heuristic

        /// <summary>
        /// Admissible and consistent lower bound on the moves left. A "cell" is one row of one shelf (the front row or
        /// one hidden layer). Items that are never moved again can only clear together with items from the same cell, and
        /// a type with 3m items left clears in m triples, so at most the m largest cell counts of a type can stay put; every
        /// other item of that type needs at least one move. One move changes one item's cell, so the bound drops by at
        /// most 1 per move. A board that is not won always scores at least 1 (a front row never holds a triple).
        /// </summary>
        public int Heuristic(Board board)
        {
            Read(board);
            return HeuristicFromSnapshot();
        }

        private int HeuristicFromSnapshot()
        {
            _touched.Clear();
            int cellBound = 0;
            for (int i = 0; i < ShelfCount; i++)
            {
                cellBound += AddCell(_front[i * 3], _front[i * 3 + 1], _front[i * 3 + 2]);
                cellBound += _hiddenCellMax[i][_hidden[i]];
                var entries = _hiddenCells[i][_hidden[i]];
                for (int e = 0; e < entries.Length; e++) Count(entries[e] >> 2, entries[e] & 3);
            }

            int items = 0;
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
                items += total;
                _totals[t] = 0;
                _cellsWith[t * 4 + 1] = 0;
                _cellsWith[t * 4 + 2] = 0;
                _cellsWith[t * 4 + 3] = 0;
            }
            // Second bound: items that stay put clear in their own cell, so a cell keeps items of one type at most.
            return Math.Max(h, items - cellBound);
        }

        private void Count(int type, int n)
        {
            if (_totals[type] == 0) _touched.Add(type);
            _totals[type] += n;
            _cellsWith[type * 4 + n]++;
        }

        // Sorted ids x <= y <= z. Returns the largest count of one type in the cell.
        private int AddCell(int x, int y, int z)
        {
            int best = 0;
            if (x != 0)
            {
                int n = 1 + (y == x ? 1 : 0) + (z == x ? 1 : 0);
                Count(x, n);
                best = n;
            }
            if (y != 0 && y != x)
            {
                int n = 1 + (z == y ? 1 : 0);
                Count(y, n);
                if (n > best) best = n;
            }
            if (z != 0 && z != y)
            {
                Count(z, 1);
                if (best == 0) best = 1;
            }
            return best;
        }

        private static int CellMax(int x, int y, int z)
        {
            if (z == 0) return 0;
            if (x == y && y == z) return 3;
            if ((x == y && x != 0) || y == z) return 2;
            return 1;
        }

        private static void AddCellEntries(int x, int y, int z, List<int> into)
        {
            if (x != 0) into.Add(x * 4 + 1 + (y == x ? 1 : 0) + (z == x ? 1 : 0));
            if (y != 0 && y != x) into.Add(y * 4 + 1 + (z == y ? 1 : 0));
            if (z != 0 && z != y) into.Add(z * 4 + 1);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Moves

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
                    if (board.CanPick(from, slot) != MoveResult.Ok) continue;
                    string item = shelf.GetFrontItem(slot);
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
