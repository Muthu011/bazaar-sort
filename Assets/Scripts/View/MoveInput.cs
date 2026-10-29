using System;
using Game.Core;

namespace Game.View
{
    /// <summary>
    /// Where a pointer is on the board, as found by the view's hit test. Pure data: the view turns screen pixels into
    /// a target, and <see cref="MoveInput"/> decides what that means using the board's rules.
    /// </summary>
    public readonly struct BoardTarget : IEquatable<BoardTarget>
    {
        private BoardTarget(int shelf, int slot)
        {
            Shelf = shelf;
            Slot = slot;
        }

        /// <summary>Nothing on the board (background, HUD, margins).</summary>
        public static BoardTarget None => new BoardTarget(-1, -1);

        /// <summary>
        /// A shelf, and the front-slot column under the pointer (0..<see cref="Shelf.SlotCount"/>-1), or -1 when the
        /// point is on the shelf but not over any slot column.
        /// </summary>
        public static BoardTarget At(int shelf, int slot) => new BoardTarget(shelf, slot);

        public int Shelf { get; }
        public int Slot { get; }

        /// <summary>True when the pointer is over a shelf.</summary>
        public bool IsShelf => Shelf >= 0;

        public bool Equals(BoardTarget other) => Shelf == other.Shelf && Slot == other.Slot;
        public override bool Equals(object obj) => obj is BoardTarget other && Equals(other);
        public override int GetHashCode() => Shelf * 31 + Slot;
        public override string ToString() => IsShelf ? $"Shelf {Shelf} slot {Slot}" : "None";
    }

    /// <summary>What a pointer gesture did, for the view to show.</summary>
    public enum InputOutcomeKind
    {
        /// <summary>Nothing happened (tap on empty space with nothing selected, drag from an empty slot, ...).</summary>
        Ignored = 0,
        /// <summary>An item is now selected (highlight it). Any previous selection is replaced.</summary>
        Selected,
        /// <summary>The selection was cancelled (remove the highlight). No move, no shake.</summary>
        Deselected,
        /// <summary>A move was made through <see cref="Board.TryMove(int,int,int,out MoveResult)"/>.</summary>
        Moved,
        /// <summary>
        /// The move was refused by <see cref="Board.CanMove"/>, or the item can't be picked up
        /// (<see cref="Board.CanPick"/>, e.g. a locked shelf; ToShelf is then -1). Shake the item; snap back if dragged.
        /// </summary>
        Rejected,
        /// <summary>A drag of an item began (lift it and follow the pointer).</summary>
        DragStarted,
        /// <summary>A drag ended without a move and without a mistake (put the item back, no shake).</summary>
        ReturnedHome,
    }

    /// <summary>
    /// Result of one <see cref="MoveInput"/> call. <see cref="Shelf"/>/<see cref="Slot"/> name the item concerned
    /// (selected, dragged, moved or shaken). <see cref="Result"/> is the board's verdict for Moved and Rejected.
    /// </summary>
    public readonly struct InputOutcome
    {
        public InputOutcome(InputOutcomeKind kind, int shelf, int slot, int toShelf, MoveResult result)
        {
            Kind = kind;
            Shelf = shelf;
            Slot = slot;
            ToShelf = toShelf;
            Result = result;
        }

        public static InputOutcome Ignored => new InputOutcome(InputOutcomeKind.Ignored, -1, -1, -1, MoveResult.Ok);

        public InputOutcomeKind Kind { get; }
        public int Shelf { get; }
        public int Slot { get; }

        /// <summary>Target shelf for Moved and Rejected, otherwise -1.</summary>
        public int ToShelf { get; }

        public MoveResult Result { get; }

        public override string ToString() => $"{Kind} ({Shelf},{Slot}) -> {ToShelf} [{Result}]";
    }

    /// <summary>
    /// Narrows which moves the player may make right now, on top of the board's own rules (the L1 tutorial lets only
    /// the shown move through, issue #57). Asked by <see cref="MoveInput"/>; never consulted by the board.
    /// </summary>
    public interface IMoveGate
    {
        /// <summary>True when the item at (<paramref name="shelf"/>, <paramref name="slot"/>) may be picked up.</summary>
        bool AllowsPick(int shelf, int slot);

        /// <summary>True when that item may be dropped on <paramref name="toShelf"/> (its own shelf is a cancel).</summary>
        bool AllowsMove(int fromShelf, int fromSlot, int toShelf);
    }

    /// <summary>
    /// The tap-to-pick, tap-to-drop and drag state machine. Lives in the View/input layer because selection and drag
    /// are interaction state, not game rules (Core holds rules only). Pure C# with no UnityEngine calls, so it is Edit
    /// Mode tested. No rules of its own: whether a move is allowed is
    /// always <see cref="Board.CanMove"/>, and moves go through <see cref="Board.TryMove(int,int,int,out MoveResult)"/>.
    ///
    /// Whether an item may be picked up at all is <see cref="Board.CanPick"/> (a locked shelf's items can't be).
    ///
    /// Tap, nothing selected: an item selects it, or is Rejected (shake) when the board won't let it be picked;
    /// anything else is ignored.
    /// Tap, item selected:
    /// <list type="bullet">
    /// <item>the selected item again, an empty part of its own shelf, or off the board: deselect (cancel);</item>
    /// <item>another item on the same shelf: select that item instead;</item>
    /// <item>another shelf (anywhere on it): try the move. Ok moves; any refusal is Rejected (shake) and the selection
    /// is cleared, as when a dragged item snaps back.</item>
    /// </list>
    /// Drag: starts only on an item that may be picked (clearing any selection); on any other item it is ignored, so
    /// the release counts as a tap and gives the same shake. Dropping on another shelf tries the move; a refusal is
    /// Rejected (snap back with shake). Dropping on its own shelf (<see cref="MoveResult.SameShelf"/>, a cancel) or
    /// off the board returns home without a shake.
    ///
    /// With a <see cref="Gate"/> (a tutorial step), what the gate refuses is not tappable: picking a refused item is
    /// Ignored (no selection, no shake, no drag), tapping a refused shelf with an item selected is Ignored and keeps the
    /// selection, and dropping on a refused shelf returns home without a shake. Cancels (own shelf, off the board) are
    /// never refused.
    /// </summary>
    public sealed class MoveInput
    {
        private readonly Board _board;
        private int _shelf = -1;
        private int _slot = -1;
        private bool _dragging;

        public MoveInput(Board board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
        }

        public Board Board => _board;

        /// <summary>Optional extra limit on moves (tutorial); null lets every move the board allows through.</summary>
        public IMoveGate Gate { get; set; }

        /// <summary>True when an item is selected by tap (not while dragging).</summary>
        public bool HasSelection => _shelf >= 0 && !_dragging;

        /// <summary>True while an item is being dragged.</summary>
        public bool IsDragging => _dragging;

        /// <summary>Shelf of the selected or dragged item, or -1.</summary>
        public int ActiveShelf => _shelf;

        /// <summary>Slot of the selected or dragged item, or -1.</summary>
        public int ActiveSlot => _slot;

        /// <summary>Handles a tap (press and release without dragging) at <paramref name="target"/>.</summary>
        public InputOutcome Tap(BoardTarget target)
        {
            if (_dragging) return InputOutcome.Ignored;

            if (!HasSelection)
            {
                if (!HasItem(target) || !GatePick(target.Shelf, target.Slot)) return InputOutcome.Ignored;
                var verdict = _board.CanPick(target.Shelf, target.Slot);
                if (verdict != MoveResult.Ok)
                {
                    return new InputOutcome(InputOutcomeKind.Rejected, target.Shelf, target.Slot, -1, verdict);
                }
                return Select(target.Shelf, target.Slot);
            }

            int fromShelf = _shelf;
            int fromSlot = _slot;

            if (!target.IsShelf) return Deselect();

            if (target.Shelf == fromShelf)
            {
                if (target.Slot != fromSlot && HasItem(target) && GatePick(target.Shelf, target.Slot))
                {
                    return Select(target.Shelf, target.Slot);
                }
                return Deselect();
            }

            if (!GateMove(fromShelf, fromSlot, target.Shelf)) return InputOutcome.Ignored;

            Clear();
            return TryMoveTo(fromShelf, fromSlot, target.Shelf);
        }

        /// <summary>
        /// Starts dragging the item at <paramref name="target"/>. Ignored (and any selection kept) when there is no
        /// item there.
        /// </summary>
        public InputOutcome BeginDrag(BoardTarget target)
        {
            if (_dragging || !HasItem(target) || !GatePick(target.Shelf, target.Slot)) return InputOutcome.Ignored;
            if (_board.CanPick(target.Shelf, target.Slot) != MoveResult.Ok) return InputOutcome.Ignored;

            _shelf = target.Shelf;
            _slot = target.Slot;
            _dragging = true;
            return new InputOutcome(InputOutcomeKind.DragStarted, _shelf, _slot, -1, MoveResult.Ok);
        }

        /// <summary>Drops the dragged item at <paramref name="target"/>.</summary>
        public InputOutcome Drop(BoardTarget target)
        {
            if (!_dragging) return InputOutcome.Ignored;

            int fromShelf = _shelf;
            int fromSlot = _slot;
            Clear();

            if (!target.IsShelf) return ReturnHome(fromShelf, fromSlot);

            var verdict = _board.CanMove(fromShelf, fromSlot, target.Shelf);
            if (verdict == MoveResult.SameShelf) return ReturnHome(fromShelf, fromSlot);
            if (!GateMove(fromShelf, fromSlot, target.Shelf)) return ReturnHome(fromShelf, fromSlot);
            return TryMoveTo(fromShelf, fromSlot, target.Shelf);
        }

        /// <summary>The pointer was lost mid-drag (touch cancelled, app paused): put the item back.</summary>
        public InputOutcome CancelDrag()
        {
            if (!_dragging) return InputOutcome.Ignored;
            int fromShelf = _shelf;
            int fromSlot = _slot;
            Clear();
            return ReturnHome(fromShelf, fromSlot);
        }

        /// <summary>Forgets any selection or drag without reporting it (board rebound, relayout).</summary>
        public void Reset() => Clear();

        private bool HasItem(BoardTarget target)
        {
            if (_board.IsWon) return false;
            if (!target.IsShelf || target.Shelf >= _board.ShelfCount) return false;
            if (target.Slot < 0 || target.Slot >= Shelf.SlotCount) return false;
            return _board.GetShelf(target.Shelf).GetFrontItem(target.Slot) != null;
        }

        private bool GatePick(int shelf, int slot) => Gate == null || Gate.AllowsPick(shelf, slot);

        private bool GateMove(int fromShelf, int fromSlot, int toShelf) =>
            Gate == null || Gate.AllowsMove(fromShelf, fromSlot, toShelf);

        private InputOutcome Select(int shelf, int slot)
        {
            _shelf = shelf;
            _slot = slot;
            _dragging = false;
            return new InputOutcome(InputOutcomeKind.Selected, shelf, slot, -1, MoveResult.Ok);
        }

        private InputOutcome Deselect()
        {
            int shelf = _shelf;
            int slot = _slot;
            Clear();
            return new InputOutcome(InputOutcomeKind.Deselected, shelf, slot, -1, MoveResult.Ok);
        }

        private static InputOutcome ReturnHome(int shelf, int slot)
        {
            return new InputOutcome(InputOutcomeKind.ReturnedHome, shelf, slot, -1, MoveResult.Ok);
        }

        private InputOutcome TryMoveTo(int fromShelf, int fromSlot, int toShelf)
        {
            bool moved = _board.TryMove(fromShelf, fromSlot, toShelf, out var result);
            var kind = moved ? InputOutcomeKind.Moved : InputOutcomeKind.Rejected;
            return new InputOutcome(kind, fromShelf, fromSlot, toShelf, result);
        }

        private void Clear()
        {
            _shelf = -1;
            _slot = -1;
            _dragging = false;
        }
    }
}
