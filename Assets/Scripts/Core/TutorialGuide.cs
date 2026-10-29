using System;

namespace Game.Core
{
    /// <summary>Which rule a level teaches (the level JSON <c>tutorial</c> field, docs/level-schema.md).</summary>
    public enum TutorialKind
    {
        /// <summary>No tutorial.</summary>
        None = 0,
        /// <summary>L1: three identical items on a shelf clear. Hand pointer, "Match 3!", only the shown move works.</summary>
        Match3,
        /// <summary>L2: a shelf hides a layer behind its front row. An arrow at the hidden row, no text.</summary>
        Layers,
        /// <summary>L11: a locked shelf opens after a number of clears anywhere. One short line at the shelf.</summary>
        Locked,
        /// <summary>L5: Undo (issue #58, needs boosters). Parsed, but no guide is built yet.</summary>
        Undo,
        /// <summary>L7: Extra Slot (issue #58, needs boosters). Parsed, but no guide is built yet.</summary>
        ExtraSlot,
    }

    /// <summary>What the view should draw for the current tutorial step.</summary>
    public enum TutorialCue
    {
        /// <summary>Nothing (no tutorial, not built yet, or finished).</summary>
        None = 0,
        /// <summary>A hand from <see cref="TutorialGuide.Move"/>'s item to its target shelf, and "Match 3!".</summary>
        HandPointer,
        /// <summary>An arrow at the hidden row of <see cref="TutorialGuide.FocusShelf"/>.</summary>
        LayerArrow,
        /// <summary>One short line at the locked shelf <see cref="TutorialGuide.FocusShelf"/>.</summary>
        LockedLine,
    }

    /// <summary>One move: the item at (<see cref="FromShelf"/>, <see cref="FromSlot"/>) onto <see cref="ToShelf"/>.</summary>
    public readonly struct GuidedMove : IEquatable<GuidedMove>
    {
        public GuidedMove(int fromShelf, int fromSlot, int toShelf)
        {
            FromShelf = fromShelf;
            FromSlot = fromSlot;
            ToShelf = toShelf;
        }

        public static GuidedMove None => new GuidedMove(-1, -1, -1);

        public int FromShelf { get; }
        public int FromSlot { get; }
        public int ToShelf { get; }

        public bool IsValid => FromShelf >= 0 && FromSlot >= 0 && ToShelf >= 0;

        public bool Equals(GuidedMove other) =>
            FromShelf == other.FromShelf && FromSlot == other.FromSlot && ToShelf == other.ToShelf;

        public override bool Equals(object obj) => obj is GuidedMove other && Equals(other);
        public override int GetHashCode() => (FromShelf * 31 + FromSlot) * 31 + ToShelf;
        public override string ToString() => IsValid ? $"({FromShelf},{FromSlot}) -> {ToShelf}" : "None";
    }

    /// <summary>
    /// The step logic of the first-play tutorials (issue #57): what to point at and, on <see cref="TutorialKind.Match3"/>,
    /// which single move is allowed until the first triple. Pure C#, Edit Mode tested. It reads the board and listens to
    /// its events; it never moves anything. Whether a tutorial was already completed (and so is skipped) is the save's
    /// business, not this class's; the view gates input with <see cref="AllowsPick"/> and <see cref="AllowsMove"/>.
    ///
    /// Steps:
    /// <list type="bullet">
    /// <item>Match3: <see cref="Move"/> is the first move (shelf order, then slot order) that completes a triple on its
    /// target shelf. Only that item may be picked and only that shelf may receive it. Complete on the first clear.</item>
    /// <item>Layers: <see cref="FocusShelf"/> is the first open shelf with a hidden layer. Nothing is restricted.
    /// Complete when any hidden layer is revealed.</item>
    /// <item>Locked: <see cref="FocusShelf"/> is the first locked shelf. Nothing is restricted. Complete when any locked
    /// shelf opens.</item>
    /// <item>Every guide also completes when the board is won. A guide whose step can't apply to the board (no
    /// single-move triple, no hidden layer, no locked shelf) starts complete, so it never traps the player.</item>
    /// <item>Undo and ExtraSlot have no guide yet (issue #58): <see cref="IsSupported"/> is false, the cue is None and
    /// they never complete.</item>
    /// </list>
    /// </summary>
    public sealed class TutorialGuide : IDisposable
    {
        private readonly Board _board;
        private bool _complete;
        private bool _subscribed;

        public TutorialGuide(Board board, TutorialKind kind)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            Kind = kind;
            Move = GuidedMove.None;
            FocusShelf = -1;

            if (!IsSupportedKind(kind)) return;

            switch (kind)
            {
                case TutorialKind.Match3:
                    Move = FindTripleMove(board);
                    if (!Move.IsValid) _complete = true;
                    break;
                case TutorialKind.Layers:
                    FocusShelf = FindHiddenLayerShelf(board);
                    if (FocusShelf < 0) _complete = true;
                    break;
                case TutorialKind.Locked:
                    FocusShelf = FindLockedShelf(board);
                    if (FocusShelf < 0) _complete = true;
                    break;
            }
            if (board.IsWon) _complete = true;
            if (_complete) return;

            _board.Cleared += OnCleared;
            _board.LayerRevealed += OnLayerRevealed;
            _board.ShelfUnlocked += OnShelfUnlocked;
            _board.Won += OnWon;
            _subscribed = true;
        }

        /// <summary>Raised once, when the step is done (see the class summary). Not raised for a guide that starts complete.</summary>
        public event Action Completed;

        public TutorialKind Kind { get; }

        public Board Board => _board;

        /// <summary>True for the kinds that have a guide (Match3, Layers, Locked).</summary>
        public bool IsSupported => IsSupportedKind(Kind);

        /// <summary>True once the step is done, or from the start when it can't apply to this board.</summary>
        public bool IsComplete => _complete && IsSupported;

        /// <summary>True while the cue should be on screen.</summary>
        public bool IsActive => IsSupported && !_complete;

        /// <summary>What the view should draw now.</summary>
        public TutorialCue Cue
        {
            get
            {
                if (!IsActive) return TutorialCue.None;
                switch (Kind)
                {
                    case TutorialKind.Match3: return TutorialCue.HandPointer;
                    case TutorialKind.Layers: return TutorialCue.LayerArrow;
                    case TutorialKind.Locked: return TutorialCue.LockedLine;
                    default: return TutorialCue.None;
                }
            }
        }

        /// <summary>The one allowed move on Match3 (None otherwise).</summary>
        public GuidedMove Move { get; }

        /// <summary>The shelf the Layers arrow or the Locked line points at, or -1.</summary>
        public int FocusShelf { get; }

        /// <summary>True when the item at (<paramref name="shelf"/>, <paramref name="slot"/>) may be picked up now.</summary>
        public bool AllowsPick(int shelf, int slot)
        {
            if (!Restricts) return true;
            return shelf == Move.FromShelf && slot == Move.FromSlot;
        }

        /// <summary>True when that item may be dropped on <paramref name="toShelf"/> now (its own shelf is always allowed: a cancel).</summary>
        public bool AllowsMove(int fromShelf, int fromSlot, int toShelf)
        {
            if (!Restricts) return true;
            if (!AllowsPick(fromShelf, fromSlot)) return false;
            return toShelf == fromShelf || toShelf == Move.ToShelf;
        }

        /// <summary>True while input is narrowed to <see cref="Move"/>.</summary>
        public bool Restricts => IsActive && Kind == TutorialKind.Match3;

        /// <summary>Stops listening to the board (level unloaded). Does not complete the step.</summary>
        public void Dispose() => Unsubscribe();

        /// <summary>
        /// The first move, in shelf then slot order, that completes a triple: an item onto an open shelf whose front row
        /// holds two of that item and one free slot. <see cref="GuidedMove.None"/> when there is none.
        /// </summary>
        public static GuidedMove FindTripleMove(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            int need = GameRules.MatchCount - 1;
            for (int to = 0; to < board.ShelfCount; to++)
            {
                if (!board.CanReceive(to)) continue;
                var target = board.GetShelf(to);
                if (target.FrontItemCount != need) continue;
                string item = PairItem(target, need);
                if (item == null) continue;

                for (int from = 0; from < board.ShelfCount; from++)
                {
                    if (from == to) continue;
                    var source = board.GetShelf(from);
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        if (!string.Equals(source.GetFrontItem(slot), item, StringComparison.Ordinal)) continue;
                        if (board.CanMove(from, slot, to) == MoveResult.Ok) return new GuidedMove(from, slot, to);
                    }
                }
            }
            return GuidedMove.None;
        }

        private static bool IsSupportedKind(TutorialKind kind) =>
            kind == TutorialKind.Match3 || kind == TutorialKind.Layers || kind == TutorialKind.Locked;

        // The item when every item in the front row is the same one (and there are `count` of them), else null.
        private static string PairItem(Shelf shelf, int count)
        {
            string item = null;
            int seen = 0;
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                string id = shelf.GetFrontItem(slot);
                if (id == null) continue;
                if (item == null) item = id;
                else if (!string.Equals(item, id, StringComparison.Ordinal)) return null;
                seen++;
            }
            return seen == count ? item : null;
        }

        private static int FindHiddenLayerShelf(Board board)
        {
            for (int i = 0; i < board.ShelfCount; i++)
            {
                var shelf = board.GetShelf(i);
                if (!shelf.IsLocked && shelf.HiddenLayerCount > 0) return i;
            }
            return -1;
        }

        private static int FindLockedShelf(Board board)
        {
            for (int i = 0; i < board.ShelfCount; i++)
            {
                if (board.GetShelf(i).IsLocked) return i;
            }
            return -1;
        }

        private void OnCleared(ClearInfo info)
        {
            if (Kind == TutorialKind.Match3) Complete();
        }

        private void OnLayerRevealed(LayerRevealInfo info)
        {
            if (Kind == TutorialKind.Layers) Complete();
        }

        private void OnShelfUnlocked(int shelf)
        {
            if (Kind == TutorialKind.Locked) Complete();
        }

        private void OnWon() => Complete();

        private void Complete()
        {
            if (_complete) return;
            _complete = true;
            Unsubscribe();
            Completed?.Invoke();
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            _board.Cleared -= OnCleared;
            _board.LayerRevealed -= OnLayerRevealed;
            _board.ShelfUnlocked -= OnShelfUnlocked;
            _board.Won -= OnWon;
        }
    }
}
