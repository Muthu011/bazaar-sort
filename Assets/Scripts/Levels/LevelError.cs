using System.Text;

namespace Game.Levels
{
    /// <summary>What kind of problem a <see cref="LevelError"/> reports. Tests and tools match on this.</summary>
    public enum LevelErrorCode
    {
        /// <summary>The text is not valid JSON (syntax error, duplicate key, trailing content).</summary>
        MalformedJson,
        /// <summary>A known field has the wrong JSON type (e.g. a slot holds a number).</summary>
        WrongType,
        /// <summary><c>schema</c> is missing or not 1.</summary>
        UnsupportedSchema,
        /// <summary><c>id</c> is missing or empty.</summary>
        MissingLevelId,
        /// <summary><c>shelves</c> is missing or empty.</summary>
        MissingShelves,
        /// <summary>A shelf has no <c>layers</c>, or an empty list of them.</summary>
        MissingLayers,
        /// <summary>A shelf's <c>kind</c> is not normal, locked or conveyor.</summary>
        UnknownShelfKind,
        /// <summary>A shelf's <c>kind</c> is in the schema but not built yet (conveyor).</summary>
        UnsupportedShelfKind,
        /// <summary>A layer does not have exactly <see cref="Game.Core.Shelf.SlotCount"/> slots.</summary>
        WrongSlotCount,
        /// <summary>An object id appears a number of times that is not a multiple of <see cref="Game.Core.GameRules.MatchCount"/>.</summary>
        ItemCountNotMultipleOfThree,
        /// <summary>An object id is not in <see cref="ObjectCatalog"/>.</summary>
        UnknownObjectId,
        /// <summary>No front slot is empty at start, so the first move is impossible.</summary>
        NoEmptyFrontSlot,
        /// <summary>A shelf's starting front row is already a triple.</summary>
        StartingTriple,
        /// <summary>
        /// <c>lockClears</c> is below 1 on a locked shelf, or set (not 0) on a shelf that isn't locked.
        /// </summary>
        InvalidLockClears,
        /// <summary>
        /// A locked shelf needs more clears than the rest of the board can ever make, so it never opens and the level
        /// can't be won.
        /// </summary>
        LockNeverOpens,
        /// <summary>
        /// More than <see cref="LevelValidator.MaxShelves"/> shelves: 12 is the cap for the whole game (Muthu, 2026-10-29),
        /// so every board keeps its touch areas on every phone.
        /// </summary>
        TooManyShelves,
        /// <summary><c>tutorial</c> is not null and not one of match3, layers, locked, undo, extra_slot.</summary>
        UnknownTutorial,
        /// <summary>
        /// The tutorial can't apply to this board: <c>layers</c> without a hidden layer on an open shelf, or
        /// <c>locked</c> without a locked shelf.
        /// </summary>
        TutorialDoesNotFit,
    }

    /// <summary>
    /// One problem found while parsing or validating a level. <see cref="Message"/> names the level, where the problem
    /// is (shelf index and id, layer, slot, as far as they apply), what is wrong and, for problems found while reading
    /// the JSON, the line and position in the file, e.g.
    /// <c>w1_l001: shelf 2 ("s3"), layer 0, slot 1: unknown object id "tumblr"</c> or
    /// <c>w1_l001: shelf 0 ("s1"), layer 1, slot 2: must be an object id string or null, found number 7 (line 9, position 37)</c>.
    /// </summary>
    public sealed class LevelError
    {
        public LevelError(LevelErrorCode code, string levelId, string detail,
            int? shelfIndex = null, string shelfId = null, int? layer = null, int? slot = null, string objectId = null,
            int? line = null, int? position = null)
        {
            Code = code;
            LevelId = levelId;
            Detail = detail;
            ShelfIndex = shelfIndex;
            ShelfId = shelfId;
            Layer = layer;
            Slot = slot;
            ObjectId = objectId;
            Line = line;
            Position = position;
            Message = Format();
        }

        public LevelErrorCode Code { get; }

        /// <summary>Level id from the file, or the source name / "(no id)" when the file has none.</summary>
        public string LevelId { get; }

        /// <summary>The "what is wrong" part of <see cref="Message"/>, without the location.</summary>
        public string Detail { get; }

        /// <summary>0-based index into <c>shelves</c>, or null when the error is not about one shelf.</summary>
        public int? ShelfIndex { get; }

        public string ShelfId { get; }

        /// <summary>0-based layer index (0 is the front row), or null.</summary>
        public int? Layer { get; }

        /// <summary>0-based slot index, or null.</summary>
        public int? Slot { get; }

        /// <summary>The object id the error is about, or null.</summary>
        public string ObjectId { get; }

        /// <summary>1-based line in the JSON text, for errors found while reading it; otherwise null.</summary>
        public int? Line { get; }

        /// <summary>
        /// Position within <see cref="Line"/> as reported by Newtonsoft's reader, or null: for a wrongly typed value,
        /// the column where the value ends; for a syntax error, the number of characters read on the line before it.
        /// </summary>
        public int? Position { get; }

        /// <summary>Full, human-readable message.</summary>
        public string Message { get; }

        public override string ToString() => Message;

        private string Format()
        {
            var sb = new StringBuilder();
            sb.Append(LevelId).Append(": ");

            bool hasLocation = false;
            if (ShelfIndex.HasValue)
            {
                sb.Append("shelf ").Append(ShelfIndex.Value);
                if (!string.IsNullOrEmpty(ShelfId)) sb.Append(" (\"").Append(ShelfId).Append("\")");
                hasLocation = true;
            }
            if (Layer.HasValue)
            {
                sb.Append(hasLocation ? ", " : string.Empty).Append("layer ").Append(Layer.Value);
                hasLocation = true;
            }
            if (Slot.HasValue)
            {
                sb.Append(hasLocation ? ", " : string.Empty).Append("slot ").Append(Slot.Value);
                hasLocation = true;
            }
            if (hasLocation) sb.Append(": ");

            sb.Append(Detail);
            if (Line.HasValue)
            {
                sb.Append(" (line ").Append(Line.Value);
                if (Position.HasValue) sb.Append(", position ").Append(Position.Value);
                sb.Append(')');
            }
            return sb.ToString();
        }
    }
}
