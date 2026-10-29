using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Game.Levels
{
    /// <summary>
    /// Play order of the level files, from their names alone: <c>w&lt;world&gt;_l&lt;index&gt;.json</c>, by world then
    /// index (so w1_l015 follows w1_l014, and w2_l001 follows the last World 1 level). Files that don't follow the
    /// pattern are not levels in the game. Pure, Edit Mode tested; the editor's level list sync uses it.
    /// </summary>
    public static class LevelOrder
    {
        private static readonly Regex Pattern = new Regex(@"^w(\d+)_l(\d+)$", RegexOptions.CultureInvariant);

        /// <summary>Reads world and index from a level file name or path ("w1_l007.json" gives 1 and 7).</summary>
        public static bool TryParse(string fileNameOrPath, out int world, out int index)
        {
            world = index = 0;
            if (string.IsNullOrEmpty(fileNameOrPath)) return false;
            var match = Pattern.Match(Path.GetFileNameWithoutExtension(fileNameOrPath));
            return match.Success
                && int.TryParse(match.Groups[1].Value, out world)
                && int.TryParse(match.Groups[2].Value, out index)
                && world > 0 && index > 0;
        }

        /// <summary>The level files among <paramref name="paths"/>, in play order. Ties (same world and index) by path.</summary>
        public static List<string> Sort(IEnumerable<string> paths)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            var levels = new List<(string path, int world, int index)>();
            foreach (var path in paths)
            {
                if (path == null || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                if (TryParse(path, out int world, out int index)) levels.Add((path, world, index));
            }
            levels.Sort((a, b) =>
            {
                int c = a.world.CompareTo(b.world);
                if (c == 0) c = a.index.CompareTo(b.index);
                return c != 0 ? c : string.CompareOrdinal(a.path, b.path);
            });
            var sorted = new List<string>(levels.Count);
            foreach (var level in levels) sorted.Add(level.path);
            return sorted;
        }
    }
}
