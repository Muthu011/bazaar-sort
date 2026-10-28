using System;
using System.Collections.Generic;

namespace Game.Levels
{
    /// <summary>
    /// The object ids a level may use, from the world lists in docs/game-design.md, in snake_case.
    /// Level validation rejects any id not listed here (case-sensitive). Add a world's ids when its levels are made.
    /// </summary>
    public static class ObjectCatalog
    {
        /// <summary>World 1, "Amma's Kitchen" (levels 1-20).</summary>
        public static readonly IReadOnlyList<string> World1 = Array.AsReadOnly(new[]
        {
            "tumbler",          // filter coffee tumbler (dabara set)
            "pressure_cooker",
            "idli_stand",
            "tawa",
            "masala_dabba",
            "steel_tiffin",
            "coconut_scraper",
            "brass_kudam",
            "rolling_pin",      // belan
            "chai_kettle",
        });

        private static readonly HashSet<string> _known = new HashSet<string>(World1, StringComparer.Ordinal);

        /// <summary>True when <paramref name="objectId"/> is a catalogued object id.</summary>
        public static bool IsKnown(string objectId)
        {
            return objectId != null && _known.Contains(objectId);
        }
    }
}
