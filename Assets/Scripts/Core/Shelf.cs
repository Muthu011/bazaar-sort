using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// One shelf: a front row of <see cref="SlotCount"/> slots plus a stack of hidden layers behind it.
    /// Item ids are plain strings (snake_case object ids from the level JSON); <c>null</c> means an empty slot.
    /// Only <see cref="Board"/> mutates a shelf; everything public here is read-only.
    ///
    /// A shelf may be locked (level kind <c>locked</c>): it then takes no moves in or out until the board has made
    /// <see cref="LockClears"/> triple clears, then it opens and acts like a normal shelf. See <see cref="Board"/>.
    /// </summary>
    public sealed class Shelf
    {
        /// <summary>Slots per layer (front row and every hidden layer). Matches level schema v1.</summary>
        public const int SlotCount = 3;

        // Not readonly so Clone can give the copy its own front row.
        private string[] _front = new string[SlotCount];
        // Hidden layers in reveal order: _hidden[_nextHidden] is the layer directly behind the front row.
        private readonly List<string[]> _hidden = new List<string[]>();
        private int _nextHidden;
        private int _lockClearsLeft;

        /// <summary>
        /// Builds a shelf from level data. <paramref name="layers"/>[0] is the front row, [1] the layer directly
        /// behind it, and so on, exactly like <c>shelves[i].layers</c> in the level JSON. Each layer must have
        /// exactly <see cref="SlotCount"/> entries; <c>null</c> (or empty string) is an empty slot.
        /// </summary>
        /// <param name="layers">Layers front-to-back. Must contain at least one layer (the front row).</param>
        /// <param name="id">Optional shelf id from the level file (e.g. "s1").</param>
        /// <param name="lockClears">
        /// 0 for a normal shelf. For a locked shelf, the number of triple clears the board must make before it opens
        /// (<c>lockClears</c> in the level JSON).
        /// </param>
        public Shelf(IReadOnlyList<IReadOnlyList<string>> layers, string id = null, int lockClears = 0)
        {
            if (layers == null) throw new ArgumentNullException(nameof(layers));
            if (layers.Count == 0) throw new ArgumentException("A shelf needs at least a front row.", nameof(layers));
            if (lockClears < 0) throw new ArgumentOutOfRangeException(nameof(lockClears), "lockClears must not be negative.");

            Id = id;
            LockClears = lockClears;
            _lockClearsLeft = lockClears;
            for (int layer = 0; layer < layers.Count; layer++)
            {
                var source = layers[layer];
                if (source == null || source.Count != SlotCount)
                {
                    throw new ArgumentException(
                        $"Shelf '{id}' layer {layer} must have exactly {SlotCount} slots.", nameof(layers));
                }

                var target = layer == 0 ? _front : new string[SlotCount];
                for (int slot = 0; slot < SlotCount; slot++)
                {
                    target[slot] = string.IsNullOrEmpty(source[slot]) ? null : source[slot];
                }

                if (layer > 0) _hidden.Add(target);
            }
        }

        /// <summary>Convenience overload: <c>new Shelf(new[] {"a", "a", null}, new[] {"b", "b", "b"})</c>.</summary>
        public Shelf(params string[][] layers) : this((IReadOnlyList<IReadOnlyList<string>>)layers)
        {
        }

        /// <summary>
        /// A locked shelf that opens after <paramref name="lockClears"/> board clears (must be at least 1).
        /// Convenience for tests and tools: <c>Shelf.Locked(2, new[] {"a", "b", null})</c>.
        /// </summary>
        public static Shelf Locked(int lockClears, params string[][] layers)
        {
            if (lockClears < 1) throw new ArgumentOutOfRangeException(nameof(lockClears), "A locked shelf needs lockClears of at least 1.");
            return new Shelf((IReadOnlyList<IReadOnlyList<string>>)layers, null, lockClears);
        }

        /// <summary>Shelf id from the level file, or null.</summary>
        public string Id { get; }

        /// <summary>Clears the board needed to open this shelf at the start (0 for a normal shelf).</summary>
        public int LockClears { get; }

        /// <summary>Clears still needed before this shelf opens; 0 once open (or for a normal shelf).</summary>
        public int LockClearsLeft => _lockClearsLeft;

        /// <summary>True while the shelf is locked: no item may leave it and none may be put on it.</summary>
        public bool IsLocked => _lockClearsLeft > 0;

        /// <summary>Number of hidden layers still behind the front row.</summary>
        public int HiddenLayerCount => _hidden.Count - _nextHidden;

        /// <summary>Item in front slot <paramref name="slot"/>, or null if empty.</summary>
        public string GetFrontItem(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return _front[slot];
        }

        /// <summary>
        /// Item in a hidden layer. <paramref name="depth"/> 0 is the layer directly behind the front row.
        /// The view can use this to draw the dimmed layer behind.
        /// </summary>
        public string GetHiddenItem(int depth, int slot)
        {
            if (depth < 0 || depth >= HiddenLayerCount) throw new ArgumentOutOfRangeException(nameof(depth));
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return _hidden[_nextHidden + depth][slot];
        }

        /// <summary>Number of items in the front row.</summary>
        public int FrontItemCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < SlotCount; i++)
                {
                    if (_front[i] != null) count++;
                }
                return count;
            }
        }

        /// <summary>True when the front row has at least one empty slot.</summary>
        public bool HasFreeFrontSlot => FrontItemCount < SlotCount;

        /// <summary>True when the front row has no items.</summary>
        public bool IsFrontEmpty => FrontItemCount == 0;

        /// <summary>Total items on this shelf, front row and hidden layers.</summary>
        public int TotalItemCount
        {
            get
            {
                int count = FrontItemCount;
                for (int layer = _nextHidden; layer < _hidden.Count; layer++)
                {
                    for (int slot = 0; slot < SlotCount; slot++)
                    {
                        if (_hidden[layer][slot] != null) count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Index of the leftmost empty front slot, or -1 if the front row is full.</summary>
        public int FirstFreeFrontSlot
        {
            get
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    if (_front[i] == null) return i;
                }
                return -1;
            }
        }

        /// <summary>
        /// Counts one board clear against the lock. Returns true when this call opened the shelf; false when it was
        /// already open or is still locked.
        /// </summary>
        internal bool CountDownLock()
        {
            if (_lockClearsLeft == 0) return false;
            _lockClearsLeft--;
            return _lockClearsLeft == 0;
        }

        internal void SetFrontItem(int slot, string item)
        {
            _front[slot] = item;
        }

        /// <summary>
        /// If <see cref="GameRules.MatchCount"/> identical items sit in the front row, removes them and returns
        /// the item id; otherwise returns null and changes nothing.
        /// </summary>
        internal string TryClearFrontMatch()
        {
            string match = FindFrontMatch();
            if (match == null) return null;

            for (int j = 0; j < SlotCount; j++)
            {
                if (string.Equals(_front[j], match, StringComparison.Ordinal)) _front[j] = null;
            }
            return match;
        }

        /// <summary>
        /// The id of the item that forms a <see cref="GameRules.MatchCount"/> match in the front row, or null.
        /// Changes nothing.
        /// </summary>
        internal string FindFrontMatch()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                string candidate = _front[i];
                if (candidate == null) continue;

                int identical = 0;
                for (int j = 0; j < SlotCount; j++)
                {
                    if (string.Equals(_front[j], candidate, StringComparison.Ordinal)) identical++;
                }

                if (GameRules.IsMatch(identical)) return candidate;
            }
            return null;
        }

        /// <summary>
        /// Copy for <see cref="Board.Clone"/>. MemberwiseClone copies every value field (so new per-shelf state is
        /// carried over without touching this method); only the front row is mutable, so it gets its own array.
        /// Hidden layers are never written after construction, so the copy shares them.
        /// </summary>
        internal Shelf Clone()
        {
            var copy = (Shelf)MemberwiseClone();
            copy._front = (string[])_front.Clone();
            return copy;
        }

        /// <summary>
        /// Moves the next hidden layer into the front row. Only valid when the front row is empty and a hidden
        /// layer remains.
        /// </summary>
        internal void RevealNextLayer()
        {
            var next = _hidden[_nextHidden];
            for (int slot = 0; slot < SlotCount; slot++)
            {
                _front[slot] = next[slot];
            }
            _nextHidden++;
        }
    }
}
