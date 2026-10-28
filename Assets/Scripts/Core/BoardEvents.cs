namespace Game.Core
{
    /// <summary>Why <see cref="Board.TryMove(int,int,int,out MoveResult)"/> accepted or rejected a move.</summary>
    public enum MoveResult
    {
        Ok = 0,
        /// <summary>fromShelf or toShelf is outside 0..ShelfCount-1.</summary>
        InvalidShelf,
        /// <summary>fromSlot is outside 0..Shelf.SlotCount-1.</summary>
        InvalidSlot,
        /// <summary>The chosen front slot has no item.</summary>
        EmptySlot,
        /// <summary>
        /// Target shelf is the source shelf. A cancel, not a mistake: put the item back, no shake, no move counted
        /// (see <see cref="Board.CanMove"/>).
        /// </summary>
        SameShelf,
        /// <summary>Target shelf has no free front slot.</summary>
        TargetFull,
        /// <summary>The board is already won.</summary>
        GameOver,
        /// <summary>The source shelf is locked: its items can't be picked up until it opens.</summary>
        SourceLocked,
        /// <summary>The target shelf is locked: nothing can be put on it until it opens.</summary>
        TargetLocked,
    }

    /// <summary>An item moved from one front slot to another. Raised before any clear it causes.</summary>
    public readonly struct MoveInfo
    {
        public MoveInfo(string itemId, int fromShelf, int fromSlot, int toShelf, int toSlot)
        {
            ItemId = itemId;
            FromShelf = fromShelf;
            FromSlot = fromSlot;
            ToShelf = toShelf;
            ToSlot = toSlot;
        }

        public string ItemId { get; }
        public int FromShelf { get; }
        public int FromSlot { get; }
        public int ToShelf { get; }
        public int ToSlot { get; }
    }

    /// <summary>A triple cleared from a shelf's front row.</summary>
    public readonly struct ClearInfo
    {
        public ClearInfo(int shelfIndex, string itemId)
        {
            ShelfIndex = shelfIndex;
            ItemId = itemId;
        }

        public int ShelfIndex { get; }
        public string ItemId { get; }
    }

    /// <summary>A hidden layer slid forward into a shelf's empty front row.</summary>
    public readonly struct LayerRevealInfo
    {
        public LayerRevealInfo(int shelfIndex, int hiddenLayersRemaining)
        {
            ShelfIndex = shelfIndex;
            HiddenLayersRemaining = hiddenLayersRemaining;
        }

        public int ShelfIndex { get; }
        public int HiddenLayersRemaining { get; }
    }
}
