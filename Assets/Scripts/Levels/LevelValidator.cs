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
    /// <item><c>schema</c> is 1, <c>id</c> is set, and <c>shelves</c> is present and not empty.</item>
    /// <item>Every shelf has a known <c>kind</c> and at least a front row, and every layer has exactly
    /// <see cref="Shelf.SlotCount"/> slots.</item>
    /// <item>Every object id is in <see cref="ObjectCatalog"/>.</item>
    /// <item>Every object id appears a multiple of <see cref="GameRules.MatchCount"/> times across the level.</item>
    /// <item>At least one front slot is empty at start.</item>
    /// <item>No shelf starts with a ready-made triple in its front row. This includes a shelf whose front row is
    /// empty: the board moves the first non-empty layer behind it forward when it is built, so that layer is the
    /// real starting front row. Triples deeper in hidden layers are fine; they clear when revealed.</item>
    /// </list>
    ///
    /// "At start" means the board as <see cref="Board"/> builds it, i.e. after empty front rows are filled from behind.
    /// Not checked yet, because the board doesn't build them: locked/conveyor behaviour (such shelves are built as
    /// normal shelves for now), <c>wrapped</c> positions, <c>role</c>/<c>tutorial</c> values, and tray sets.
    /// </summary>
    public static class LevelValidator
    {
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

            // Object counts in order of first appearance, so error order follows the file.
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var countOrder = new List<string>();
            int emptyFrontSlots = 0;
            bool allFrontsKnown = true;

            for (int s = 0; s < level.Shelves.Count; s++)
            {
                var shelf = level.Shelves[s];
                CheckShelf(levelId, shelf, s, errors, counts, countOrder);

                int empty = CheckStartingFront(levelId, shelf, s, errors);
                if (empty < 0) allFrontsKnown = false;
                else emptyFrontSlots += empty;
            }

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
                errors.Add(new LevelError(LevelErrorCode.NoEmptyFrontSlot, levelId,
                    "no empty front slot at start; at least one front slot must be empty so the first move is possible"));
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
