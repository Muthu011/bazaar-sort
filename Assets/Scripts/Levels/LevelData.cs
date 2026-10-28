using System.Collections.Generic;

namespace Game.Levels
{
    /// <summary>
    /// One level file (docs/level-schema.md, v1) as plain data, before validation.
    /// <see cref="LevelParser"/> fills it from JSON; <see cref="LevelValidator"/> checks it; <see cref="LevelLoader"/>
    /// turns it into a <see cref="Game.Core.Board"/>. A field missing from the file (or JSON null) is null here, so the
    /// validator can say so. The <c>solver</c> block is written by the solver tool and not read at load time.
    /// </summary>
    public sealed class LevelData
    {
        /// <summary>Schema version; only 1 is supported.</summary>
        public int? Schema { get; set; }

        /// <summary>Level id, e.g. "w1_l001". Used in every validation error.</summary>
        public string Id { get; set; }

        public int? World { get; set; }
        public int? Index { get; set; }

        /// <summary>easy | medium | hard | breather | boss. Not checked yet.</summary>
        public string Role { get; set; }

        /// <summary>Optional tutorial key: match3 | layers | undo | extra_slot. Not checked yet.</summary>
        public string Tutorial { get; set; }

        /// <summary>Null means unlimited.</summary>
        public int? MoveLimit { get; set; }

        /// <summary>Null means no timer.</summary>
        public int? TimeLimitSec { get; set; }

        /// <summary>Null when the file has no <c>shelves</c> field.</summary>
        public List<ShelfData> Shelves { get; set; }

        /// <summary>Set Trays (from level 21). Empty when the file has none.</summary>
        public List<TrayData> Trays { get; set; } = new List<TrayData>();

        /// <summary>"shelf:layer:slot" positions hidden until a neighbour clears. Not used by the board yet.</summary>
        public List<string> Wrapped { get; set; } = new List<string>();
    }

    /// <summary>One entry of <c>shelves</c> in the level file.</summary>
    public sealed class ShelfData
    {
        public string Id { get; set; }

        /// <summary>normal | locked | conveyor (see <see cref="ShelfKinds"/>). A missing kind reads as normal.</summary>
        public string Kind { get; set; } = ShelfKinds.Normal;

        /// <summary>Locked shelves: clears needed to open.</summary>
        public int LockClears { get; set; }

        /// <summary>Layers front-to-back: <c>Layers[0]</c> is the front row; null is an empty slot.</summary>
        public List<List<string>> Layers { get; set; }
    }

    /// <summary>One entry of <c>trays</c> in the level file.</summary>
    public sealed class TrayData
    {
        public string Id { get; set; }
        public List<string> Set { get; set; } = new List<string>();
    }

    /// <summary>Values of <see cref="ShelfData.Kind"/> that schema v1 allows.</summary>
    public static class ShelfKinds
    {
        public const string Normal = "normal";
        public const string Locked = "locked";
        public const string Conveyor = "conveyor";

        public static bool IsKnown(string kind)
        {
            return kind == Normal || kind == Locked || kind == Conveyor;
        }

        /// <summary>
        /// True for kinds the board can build today. Only normal shelves for now: locked shelves arrive in Slice 2,
        /// conveyor shelves after launch.
        /// </summary>
        public static bool IsSupported(string kind)
        {
            return kind == Normal;
        }
    }
}
