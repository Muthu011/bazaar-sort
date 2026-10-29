using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Levels
{
    /// <summary>
    /// Checks parsed level data against the schema rules (docs/level-schema.md) that the board relies on.
    /// Collects every error rather than stopping at the first, so a designer can fix a file in one pass.
    ///
    /// Rules checked:
    /// <list type="bullet">
    /// <item><c>schema</c> is 1, <c>id</c> is set, and <c>shelves</c> is present, not empty and at most
    /// <see cref="MaxShelves"/> long.</item>
    /// <item>Every shelf is <c>normal</c> or <c>locked</c> (conveyor shelves are rejected as not supported yet) and
    /// has at least a front row, and every layer has exactly <see cref="Shelf.SlotCount"/> slots.</item>
    /// <item>A locked shelf has <c>lockClears</c> of at least 1; any other shelf has it absent or 0.</item>
    /// <item>A locked shelf can open: its <c>lockClears</c> is no more than the clears the items on the other shelves
    /// could ever make (their item count / 3). A cheap upper bound only; whether the level is really solvable is the
    /// solver's job.</item>
    /// <item>Every object id is in <see cref="ObjectCatalog"/>.</item>
    /// <item>Every object id appears a multiple of <see cref="GameRules.MatchCount"/> times across the level.</item>
    /// <item>At least one front slot on an open (not locked) shelf is empty at start: locked shelves take no moves.</item>
    /// <item>No shelf starts with a ready-made triple in its front row. This includes a shelf whose front row is
    /// empty: the board moves the first non-empty layer behind it forward when it is built, so that layer is the
    /// real starting front row. Triples deeper in hidden layers are fine; they clear when revealed. Locked shelves
    /// follow the same rule: a starting triple there would clear the moment the shelf opens.</item>
    /// </list>
    ///
    /// "At start" means the board as <see cref="Board"/> builds it, i.e. after empty front rows are filled from behind.
    /// Not checked yet, because the board doesn't build them: <c>wrapped</c> positions, <c>role</c>/<c>tutorial</c>
    /// values, and tray sets.
    /// </summary>
    public static class LevelValidator
    {
        /// <summary>
        /// Most shelves a level may have: the cap for the whole game, no per-phone special cases (Muthu, 2026-10-29,
        /// issue #40). The portrait layout keeps item touch areas at 44 dp or more on every supported phone up to here.
        /// </summary>
        public const int MaxShelves = 12;

        /// <summary>
        /// Returns every problem with <paramref name="level"/>; an empty list means the board can be built from it.
        /// <paramref name="sourceName"/> names the level in errors when it has no <c>id</c>.
        /// </summary>
        public static IReadOnlyList<LevelError> Validate(LevelData level, string sourceName = null)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            var errors = new List<LevelError>();
            string levelId = !string.IsNullOrEmpty(level.Id) ? level.Id
                : !string.IsNullOrEmpty(sourceName) ? sourceName
                : LevelParser.NoIdName;

            if (string.IsNullOrEmpty(level.Id))
            {
                errors.Add(new LevelError(LevelErrorCode.MissingLevelId, levelId, "field \"id\" is missing or empty"));
            }

            if (level.Schema == null)
            {
                errors.Add(new LevelError(LevelErrorCode.UnsupportedSchema, levelId,
                    "field \"schema\" is missing; expected 1"));
            }
            else if (level.Schema.Value != 1)
            {
                errors.Add(new LevelError(LevelErrorCode.UnsupportedSchema, levelId,
                    $"schema {level.Schema.Value} is not supported; expected 1"));
            }

            if (level.Shelves == null || level.Shelves.Count == 0)
            {
                string what = level.Shelves == null ? "missing" : "empty";
                errors.Add(new LevelError(LevelErrorCode.MissingShelves, levelId,
                    $"field \"shelves\" is {what}; a level needs at least one shelf"));
                return errors;
            }

            if (level.Shelves.Count > MaxShelves)
            {
                errors.Add(new LevelError(LevelErrorCode.TooManyShelves, levelId,
                    $"{level.Shelves.Count} shelves; a level may have at most {MaxShelves}"));
            }

            // Object counts in order of first appearance, so error order follows the file.
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var countOrder = new List<string>();
            int emptyFrontSlots = 0;
            int emptyLockedFrontSlots = 0;
            bool allFrontsKnown = true;

            for (int s = 0; s < level.Shelves.Count; s++)
            {
                var shelf = level.Shelves[s];
                CheckShelf(levelId, shelf, s, errors, counts, countOrder);
                CheckLockClears(levelId, shelf, s, errors);

                int empty = CheckStartingFront(levelId, shelf, s, errors);
                if (empty < 0) allFrontsKnown = false;
                else if (IsLocked(shelf)) emptyLockedFrontSlots += empty;
                else emptyFrontSlots += empty;
            }

            CheckLocksCanOpen(levelId, level.Shelves, errors);

            // Objects used by Set Trays are exempt from the multiple-of-3 rule; the schema says they must match their
            // tray sets exactly instead. Trays aren't built yet, so that tray-specific count is not checked here: tray
            // objects are simply skipped. Add the tray rule when trays land (World 2, level 21).
            var trayObjects = new HashSet<string>(StringComparer.Ordinal);
            if (level.Trays != null)
            {
                foreach (var tray in level.Trays)
                {
                    if (tray?.Set == null) continue;
                    foreach (var id in tray.Set)
                    {
                        if (id != null) trayObjects.Add(id);
                    }
                }
            }

            foreach (var id in countOrder)
            {
                if (trayObjects.Contains(id)) continue;
                int count = counts[id];
                if (count % GameRules.MatchCount != 0)
                {
                    errors.Add(new LevelError(LevelErrorCode.ItemCountNotMultipleOfThree, levelId,
                        $"object \"{id}\" appears {count} times; every object must appear a multiple of {GameRules.MatchCount} times",
                        objectId: id));
                }
            }

            // If a malformed shelf hides its starting front row, free space can't be judged; the shape errors already
            // reject the level. Otherwise the free slots found so far are a lower bound, so only report when certain.
            if (allFrontsKnown && emptyFrontSlots == 0)
            {
                string detail = emptyLockedFrontSlots == 0
                    ? "no empty front slot at start; at least one front slot must be empty so the first move is possible"
                    : "no empty front slot on an open shelf at start; locked shelves take no moves, so an open shelf needs a free front slot for the first move";
                errors.Add(new LevelError(LevelErrorCode.NoEmptyFrontSlot, levelId, detail));
            }

            return errors;
        }

        /// <summary>Checks kind, layers, slot counts and object ids of one shelf and counts its known objects.</summary>
        private static void CheckShelf(string levelId, ShelfData shelf, int index, List<LevelError> errors,
            Dictionary<string, int> counts, List<string> countOrder)
        {
            if (shelf != null && !ShelfKinds.IsKnown(shelf.Kind))
            {
                errors.Add(new LevelError(LevelErrorCode.UnknownShelfKind, levelId,
                    $"unknown shelf kind \"{shelf.Kind}\"; expected normal, locked or conveyor", index, shelf.Id));
            }
            else if (shelf != null && !ShelfKinds.IsSupported(shelf.Kind))
            {
                errors.Add(new LevelError(LevelErrorCode.UnsupportedShelfKind, levelId,
                    $"shelf kind \"{shelf.Kind}\" is not supported yet (conveyor shelves come after launch); use \"normal\"",
                    index, shelf.Id));
            }

            if (shelf?.Layers == null || shelf.Layers.Count == 0)
            {
                errors.Add(new LevelError(LevelErrorCode.MissingLayers, levelId,
                    "has no layers; a shelf needs at least a front row", index, shelf?.Id));
                return;
            }

            for (int layer = 0; layer < shelf.Layers.Count; layer++)
            {
                var slots = shelf.Layers[layer];
                int slotCount = slots?.Count ?? 0;
                if (slotCount != Shelf.SlotCount)
                {
                    errors.Add(new LevelError(LevelErrorCode.WrongSlotCount, levelId,
                        $"has {slotCount} slots; every layer needs exactly {Shelf.SlotCount}", index, shelf.Id, layer));
                }
                if (slots == null) continue;

                // Still check and count items in a mis-sized layer: they are what the designer wrote.
                for (int slot = 0; slot < slots.Count; slot++)
                {
                    string id = slots[slot];
                    if (id == null) continue;

                    if (!ObjectCatalog.IsKnown(id))
                    {
                        string detail = id.Length == 0
                            ? "empty object id \"\"; use null for an empty slot"
                            : $"unknown object id \"{id}\"";
                        errors.Add(new LevelError(LevelErrorCode.UnknownObjectId, levelId, detail,
                            index, shelf.Id, layer, slot, id));
                        // Unknown ids are not counted: one error per bad slot is enough.
                        continue;
                    }

                    if (counts.TryGetValue(id, out int count))
                    {
                        counts[id] = count + 1;
                    }
                    else
                    {
                        counts.Add(id, 1);
                        countOrder.Add(id);
                    }
                }
            }
        }

        /// <summary>
        /// Finds the shelf's real starting front row (layer 0, or the first non-empty layer behind empty ones, as
        /// <see cref="Board"/> does), reports a ready-made triple in it, and returns its number of empty slots.
        /// Returns -1 when a layer it has to look at is malformed (wrong slot count or missing), so the starting
        /// front row is unknown; the shape error is reported by <see cref="CheckShelf"/>.
        /// </summary>
        private static int CheckStartingFront(string levelId, ShelfData shelf, int index, List<LevelError> errors)
        {
            if (shelf?.Layers == null || shelf.Layers.Count == 0) return -1;

            int frontLayer = 0;
            while (true)
            {
                var layer = shelf.Layers[frontLayer];
                if (layer == null || layer.Count != Shelf.SlotCount) return -1;
                if (!IsEmptyLayer(layer) || frontLayer == shelf.Layers.Count - 1) break;
                frontLayer++;
            }

            var row = shelf.Layers[frontLayer];
            int empty = 0;
            string triple = null;
            for (int i = 0; i < row.Count; i++)
            {
                // Shelf treats "" as an empty slot too; mirror it so this matches the board.
                if (string.IsNullOrEmpty(row[i]))
                {
                    empty++;
                    continue;
                }

                int identical = 0;
                for (int j = 0; j < row.Count; j++)
                {
                    if (string.Equals(row[i], row[j], StringComparison.Ordinal)) identical++;
                }
                if (triple == null && GameRules.IsMatch(identical)) triple = row[i];
            }

            if (triple != null)
            {
                string detail = frontLayer == 0
                    ? $"starting front row is a ready-made triple of \"{triple}\"; it would clear before the first move"
                    : $"front row is empty, so layer {frontLayer} moves forward at start and is a ready-made triple of \"{triple}\"";
                errors.Add(new LevelError(LevelErrorCode.StartingTriple, levelId, detail,
                    index, shelf.Id, frontLayer, objectId: triple));
            }

            return empty;
        }

        private static bool IsLocked(ShelfData shelf) => shelf != null && shelf.Kind == ShelfKinds.Locked;

        /// <summary>Locked shelves need lockClears of at least 1; every other shelf has it absent or 0.</summary>
        private static void CheckLockClears(string levelId, ShelfData shelf, int index, List<LevelError> errors)
        {
            if (shelf == null) return;
            if (IsLocked(shelf))
            {
                if (shelf.LockClears < 1)
                {
                    errors.Add(new LevelError(LevelErrorCode.InvalidLockClears, levelId,
                        $"locked shelf has lockClears {shelf.LockClears}; it must be at least 1", index, shelf.Id));
                }
            }
            else if (shelf.LockClears != 0)
            {
                errors.Add(new LevelError(LevelErrorCode.InvalidLockClears, levelId,
                    $"lockClears is {shelf.LockClears} but the shelf is not locked; leave it out or set it to 0",
                    index, shelf.Id));
            }
        }

        /// <summary>
        /// Every clear counts every lock down by one, and a locked shelf's own items can't clear before it opens, so it
        /// opens at the latest after (items on the other shelves) / 3 clears. A lock that needs more never opens and the
        /// level can't be won. This is a cheap bound, not a solve: a lock within it may still be unreachable in play
        /// (the solver, issue #20, catches that).
        /// </summary>
        private static void CheckLocksCanOpen(string levelId, List<ShelfData> shelves, List<LevelError> errors)
        {
            int total = 0;
            for (int s = 0; s < shelves.Count; s++) total += CountItems(shelves[s]);

            for (int s = 0; s < shelves.Count; s++)
            {
                var shelf = shelves[s];
                if (!IsLocked(shelf) || shelf.LockClears < 1) continue;
                int possible = (total - CountItems(shelf)) / GameRules.MatchCount;
                if (shelf.LockClears > possible)
                {
                    errors.Add(new LevelError(LevelErrorCode.LockNeverOpens, levelId,
                        $"locked shelf needs {shelf.LockClears} clears but the other shelves hold only enough items for {possible}; it would never open",
                        s, shelf.Id));
                }
            }
        }

        /// <summary>Items (any non-empty slot) on a shelf, all layers.</summary>
        private static int CountItems(ShelfData shelf)
        {
            if (shelf?.Layers == null) return 0;
            int count = 0;
            foreach (var layer in shelf.Layers)
            {
                if (layer == null) continue;
                foreach (var id in layer)
                {
                    if (!string.IsNullOrEmpty(id)) count++;
                }
            }
            return count;
        }

        private static bool IsEmptyLayer(List<string> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (!string.IsNullOrEmpty(slots[i])) return false;
            }
            return true;
        }
    }
}
