using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Game.Core
{
    /// <summary>
    /// The puzzle board for one level: a list of shelves and the rules for moving, clearing and revealing.
    /// Pure C#; the view subscribes to the events below to animate.
    ///
    /// Rules (Slice 1):
    /// <list type="bullet">
    /// <item>A move takes the item in a front slot of one shelf and puts it in the leftmost free front slot of a
    /// different shelf.</item>
    /// <item>When <see cref="GameRules.MatchCount"/> identical items sit in a shelf's front row, they clear.</item>
    /// <item>When a shelf's front row is empty (after a clear, or because its last item was moved away) and a hidden
    /// layer remains, the next layer slides forward. A revealed layer that is itself a triple clears straight away,
    /// and that can reveal the layer after it, and so on until the shelf settles.</item>
    /// <item>Won: no items remain anywhere on the board. Stuck: items remain but no legal move exists.
    /// Both are evaluated only once the whole move has settled (all clears and reveals done), never mid-move.</item>
    /// </list>
    ///
    /// Locked shelves (issue #19):
    /// <list type="bullet">
    /// <item>A locked shelf takes no moves in or out: its items can't be picked up (<see cref="MoveResult.SourceLocked"/>)
    /// and nothing can be dropped on it (<see cref="MoveResult.TargetLocked"/>). Its items stay on show.</item>
    /// <item>Every triple clear anywhere on the board, including chained clears after a reveal, counts every locked
    /// shelf down by one. The count is board-wide: a clear on a neighbouring shelf counts the same as any other.</item>
    /// <item>When a shelf's count reaches 0 it opens, <see cref="ShelfUnlocked"/> is raised once for it, and it acts
    /// like a normal shelf from then on.</item>
    /// <item>While locked, a shelf's contents never change (no move reaches it, so it can't clear or reveal). Its
    /// starting front row is never a triple (the loader rejects that, as for every shelf), so opening never clears
    /// anything by itself.</item>
    /// <item>Locked shelves never offer a legal move, so Stuck counts only open shelves. With no legal move no clear can
    /// happen, so no lock can open either: Stuck is final. Won still needs every item cleared, locked ones included.</item>
    /// </list>
    ///
    /// Event order for one move: Moved, then source shelf settle (LayerRevealed / Cleared ...), then target shelf
    /// settle (Cleared, LayerRevealed, Cleared ...), then at most one of Won or Stuck. Each Cleared is raised after the
    /// lock counts have taken that clear into account, and is followed straight away by ShelfUnlocked for every shelf it
    /// opened, in shelf order.
    /// </summary>
    public sealed class Board
    {
        private readonly Shelf[] _shelves;
        private int _itemCount;
        private bool _wonRaised;
        private bool _stuckRaised;

        // Shelves opened by the latest clear, waiting for their ShelfUnlocked event. A clear can open several shelves.
        private int _pendingUnlocks;
        private readonly bool[] _justOpened;

        /// <summary>Raised after an item lands in its new slot.</summary>
        public event Action<MoveInfo> Moved;

        /// <summary>Raised when a triple clears from a front row.</summary>
        public event Action<ClearInfo> Cleared;

        /// <summary>
        /// Raised once per locked shelf, with its index, right after the Cleared that brings its count to 0. The shelf
        /// is open (takes moves) from then on.
        /// </summary>
        public event Action<int> ShelfUnlocked;

        /// <summary>Raised when a hidden layer slides into an empty front row.</summary>
        public event Action<LayerRevealInfo> LayerRevealed;

        /// <summary>Raised once, after the move that clears the last item has settled.</summary>
        public event Action Won;

        /// <summary>Raised once, after a move settles into a state with items left but no legal move.</summary>
        public event Action Stuck;

        /// <summary>
        /// Builds a board from shelves (see <see cref="Shelf(IReadOnlyList{IReadOnlyList{string}}, string)"/> for
        /// building a shelf from level data).
        ///
        /// Construction does not raise events. If a shelf starts with an empty front row and hidden layers behind,
        /// the next non-empty layer is moved forward silently so the board starts in a settled state.
        ///
        /// A level must never start with a ready-made triple in a front row. The level loader rejects such levels,
        /// so the board trusts its input; in the editor and development builds it throws
        /// <see cref="InvalidOperationException"/> as a safety net.
        /// </summary>
        public Board(IEnumerable<Shelf> shelves)
        {
            if (shelves == null) throw new ArgumentNullException(nameof(shelves));

            var list = new List<Shelf>();
            foreach (var shelf in shelves)
            {
                if (shelf == null) throw new ArgumentException("Shelves must not be null.", nameof(shelves));
                if (list.Contains(shelf)) throw new ArgumentException("A shelf instance can only be used once.", nameof(shelves));
                list.Add(shelf);
            }
            if (list.Count == 0) throw new ArgumentException("A board needs at least one shelf.", nameof(shelves));

            _shelves = list.ToArray();
            _justOpened = new bool[_shelves.Length];
            for (int i = 0; i < _shelves.Length; i++)
            {
                while (_shelves[i].IsFrontEmpty && _shelves[i].HiddenLayerCount > 0)
                {
                    _shelves[i].RevealNextLayer();
                }
                _itemCount += _shelves[i].TotalItemCount;
            }
            AssertNoStartingMatch();
        }

        /// <summary>Convenience overload.</summary>
        public Board(params Shelf[] shelves) : this((IEnumerable<Shelf>)shelves)
        {
        }

        /// <summary>
        /// Builds a board straight from level data: <c>layers[shelf][layer][slot]</c>, with layer 0 the front row
        /// and null for an empty slot (the shape of <c>shelves[].layers</c> in the level JSON).
        /// </summary>
        public static Board FromLayers(IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> layers)
        {
            if (layers == null) throw new ArgumentNullException(nameof(layers));
            var shelves = new Shelf[layers.Count];
            for (int i = 0; i < layers.Count; i++)
            {
                shelves[i] = new Shelf(layers[i]);
            }
            return new Board(shelves);
        }

        /// <summary>
        /// An independent copy of the current, settled state, for search tools such as the level solver. The copy has
        /// no event subscribers and has not raised Won or Stuck; moves on either board never affect the other.
        /// </summary>
        public Board Clone()
        {
            var shelves = new Shelf[_shelves.Length];
            for (int i = 0; i < _shelves.Length; i++)
            {
                shelves[i] = _shelves[i].Clone();
            }
            return new Board(shelves, _itemCount);
        }

        // For Clone: the shelves are fresh copies of a settled board, so the checks and settling of the public
        // constructor are not needed.
        private Board(Shelf[] clonedShelves, int itemCount)
        {
            _shelves = clonedShelves;
            _justOpened = new bool[clonedShelves.Length];
            _itemCount = itemCount;
        }

        /// <summary>
        /// Overwrites this board's state with <paramref name="source"/>'s, without allocating. Both boards must come
        /// from one board by <see cref="Clone"/> (directly or through other clones). Like a fresh clone, the result has
        /// not raised Won or Stuck; event subscribers are kept. For search tools that try many moves from one board.
        /// </summary>
        public void CopyStateFrom(Board source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source._shelves.Length != _shelves.Length)
            {
                throw new ArgumentException("Boards must be clones of the same board.", nameof(source));
            }
            for (int i = 0; i < _shelves.Length; i++)
            {
                _shelves[i].CopyStateFrom(source._shelves[i]);
                _justOpened[i] = false;
            }
            _itemCount = source._itemCount;
            _wonRaised = false;
            _stuckRaised = false;
            _pendingUnlocks = 0;
        }

        public int ShelfCount => _shelves.Length;

        public Shelf GetShelf(int index)
        {
            if (index < 0 || index >= _shelves.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _shelves[index];
        }

        /// <summary>Items left on the board, front rows and hidden layers.</summary>
        public int ItemCount => _itemCount;

        /// <summary>True when no items remain.</summary>
        public bool IsWon => _itemCount == 0;

        /// <summary>True when items remain but no legal move exists.</summary>
        public bool IsStuck => !IsWon && !HasLegalMove();

        /// <summary>
        /// True when at least one open (not locked) shelf has a front item and some other open shelf has a free front
        /// slot.
        /// </summary>
        public bool HasLegalMove()
        {
            for (int from = 0; from < _shelves.Length; from++)
            {
                if (_shelves[from].IsLocked || _shelves[from].IsFrontEmpty) continue;
                for (int to = 0; to < _shelves.Length; to++)
                {
                    if (to != from && !_shelves[to].IsLocked && _shelves[to].HasFreeFrontSlot) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks whether the front item at (<paramref name="shelf"/>, <paramref name="slot"/>) may be picked up to
        /// start a move. Rejections, in order: board already won, shelf out of range, slot out of range, empty slot,
        /// shelf locked. The input layer asks this before selecting or dragging, so it never adds rules of its own.
        /// </summary>
        public MoveResult CanPick(int shelf, int slot)
        {
            if (IsWon) return MoveResult.GameOver;
            if (shelf < 0 || shelf >= _shelves.Length) return MoveResult.InvalidShelf;
            if (slot < 0 || slot >= Shelf.SlotCount) return MoveResult.InvalidSlot;
            if (_shelves[shelf].GetFrontItem(slot) == null) return MoveResult.EmptySlot;
            if (_shelves[shelf].IsLocked) return MoveResult.SourceLocked;
            return MoveResult.Ok;
        }

        /// <summary>
        /// Checks a move without performing it. Rejections, in order of precedence: board already won, shelf index
        /// out of range, slot index out of range, empty source slot, source shelf locked, target equals source shelf,
        /// target shelf locked, target front row full.
        ///
        /// The issue's "same slot" rule: an item may not be moved to the shelf it is already on. Any three identical
        /// items on a shelf clear wherever they sit, so such a move would be a no-op. <see cref="MoveResult.SameShelf"/>
        /// is a cancel, not a mistake: the view puts the item back without a shake and no move is counted. It takes
        /// precedence over <see cref="MoveResult.TargetFull"/>, so dropping back onto a full shelf is also a cancel.
        /// </summary>
        public MoveResult CanMove(int fromShelf, int fromSlot, int toShelf)
        {
            if (IsWon) return MoveResult.GameOver;
            if (fromShelf < 0 || fromShelf >= _shelves.Length) return MoveResult.InvalidShelf;
            if (toShelf < 0 || toShelf >= _shelves.Length) return MoveResult.InvalidShelf;
            if (fromSlot < 0 || fromSlot >= Shelf.SlotCount) return MoveResult.InvalidSlot;
            if (_shelves[fromShelf].GetFrontItem(fromSlot) == null) return MoveResult.EmptySlot;
            if (_shelves[fromShelf].IsLocked) return MoveResult.SourceLocked;
            if (fromShelf == toShelf) return MoveResult.SameShelf;
            if (_shelves[toShelf].IsLocked) return MoveResult.TargetLocked;
            if (!_shelves[toShelf].HasFreeFrontSlot) return MoveResult.TargetFull;
            return MoveResult.Ok;
        }

        /// <inheritdoc cref="TryMove(int,int,int,out MoveResult)"/>
        public bool TryMove(int fromShelf, int fromSlot, int toShelf)
        {
            return TryMove(fromShelf, fromSlot, toShelf, out _);
        }

        /// <summary>
        /// Moves the front item at (<paramref name="fromShelf"/>, <paramref name="fromSlot"/>) into the leftmost
        /// free front slot of <paramref name="toShelf"/>, then resolves clears and reveals and checks for Won/Stuck.
        /// Returns false and changes nothing when the move is illegal (see <see cref="CanMove"/>).
        /// </summary>
        public bool TryMove(int fromShelf, int fromSlot, int toShelf, out MoveResult result)
        {
            result = CanMove(fromShelf, fromSlot, toShelf);
            if (result != MoveResult.Ok) return false;

            var source = _shelves[fromShelf];
            var target = _shelves[toShelf];
            string item = source.GetFrontItem(fromSlot);
            int toSlot = target.FirstFreeFrontSlot;

            source.SetFrontItem(fromSlot, null);
            target.SetFrontItem(toSlot, item);
            Moved?.Invoke(new MoveInfo(item, fromShelf, fromSlot, toShelf, toSlot));

            Settle(fromShelf);
            Settle(toShelf);

            if (IsWon)
            {
                if (!_wonRaised)
                {
                    _wonRaised = true;
                    Won?.Invoke();
                }
            }
            else if (!HasLegalMove())
            {
                if (!_stuckRaised)
                {
                    _stuckRaised = true;
                    Stuck?.Invoke();
                }
            }

            return true;
        }

        /// <summary>Safety net for level data the loader should have rejected. Stripped from release builds.</summary>
        [Conditional("UNITY_ASSERTIONS")]
        private void AssertNoStartingMatch()
        {
            for (int i = 0; i < _shelves.Length; i++)
            {
                string match = _shelves[i].FindFrontMatch();
                if (match != null)
                {
                    throw new InvalidOperationException(
                        $"Shelf {i} starts with a ready-made triple of '{match}'. Level validation should reject this level.");
                }
            }
        }

        /// <summary>Counts one clear against every locked shelf and remembers which ones it opened.</summary>
        private void CountDownLocks()
        {
            for (int i = 0; i < _shelves.Length; i++)
            {
                if (!_shelves[i].CountDownLock()) continue;
                _justOpened[i] = true;
                _pendingUnlocks++;
            }
        }

        /// <summary>Raises ShelfUnlocked for the shelves the latest clear opened, in shelf order.</summary>
        private void RaiseUnlocks()
        {
            if (_pendingUnlocks == 0) return;
            for (int i = 0; i < _shelves.Length; i++)
            {
                if (!_justOpened[i]) continue;
                _justOpened[i] = false;
                _pendingUnlocks--;
                ShelfUnlocked?.Invoke(i);
            }
        }

        /// <summary>Clears triples and reveals layers on one shelf until nothing more changes.</summary>
        private void Settle(int shelfIndex)
        {
            var shelf = _shelves[shelfIndex];
            while (true)
            {
                string cleared = shelf.TryClearFrontMatch();
                if (cleared != null)
                {
                    _itemCount -= GameRules.MatchCount;
                    CountDownLocks();
                    Cleared?.Invoke(new ClearInfo(shelfIndex, cleared));
                    RaiseUnlocks();
                    continue;
                }

                if (shelf.IsFrontEmpty && shelf.HiddenLayerCount > 0)
                {
                    shelf.RevealNextLayer();
                    LayerRevealed?.Invoke(new LayerRevealInfo(shelfIndex, shelf.HiddenLayerCount));
                    continue;
                }

                return;
            }
        }
    }
}
