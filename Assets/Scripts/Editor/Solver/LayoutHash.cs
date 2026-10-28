using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Levels;

namespace Game.Solver
{
    /// <summary>
    /// Canonical duplicate hash of a level layout (<c>solver.hash</c>).
    ///
    /// Invariant under: shelf order (mirroring or any other reordering), item order inside one row (left-right
    /// mirroring; the rules never look at slot positions), and renaming object types (swapping which object plays which
    /// role). Sensitive to: every row of every shelf including hidden layers and their order front-to-back, empty slots,
    /// each shelf's kind and lockClears. Not included: move or time limits, tutorial, role, trays and wrapped positions
    /// (the solver refuses levels that use trays or wrapped items until the board models them).
    ///
    /// Method: object types get labels by colour refinement (a type is described by the shelves and depths it sits at,
    /// shelves by the types they hold, repeated until stable); types the refinement cannot tell apart are tried in every
    /// order, and the lexicographically smallest serialisation wins. The canonical text is hashed with SHA-256 and the
    /// first 16 hex digits (64 bits) are kept.
    /// </summary>
    public static class LayoutHash
    {
        private const string Version = "bazaar-sort/layout/v1";

        /// <summary>Above this many candidate type orders the hash falls back to one order and is marked inexact.</summary>
        public const int MaxOrders = 40320;

        public static string Compute(LevelData level)
        {
            return Compute(level, out _);
        }

        /// <param name="exact">False only when tied types had more than <see cref="MaxOrders"/> orders to try.</param>
        public static string Compute(LevelData level, out bool exact)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (level.Shelves == null) throw new ArgumentException("Level has no shelves.", nameof(level));
            var canonical = CanonicalText(level, out exact);
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Version + "\n" + canonical));
                var hex = new StringBuilder(16);
                for (int i = 0; i < 8; i++) hex.Append(bytes[i].ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>The text that is hashed; exposed for tests and debugging.</summary>
        public static string CanonicalText(LevelData level, out bool exact)
        {
            var shelves = level.Shelves;
            var types = shelves.SelectMany(s => s.Layers).SelectMany(l => l)
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var typeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < types.Count; i++) typeIndex[types[i]] = i;

            var baseKeys = shelves.Select(s => $"{s.Kind ?? ShelfKinds.Normal}:{s.LockClears}:{s.Layers.Count}").ToArray();

            // Colour refinement.
            var typeColour = new int[types.Count];
            var shelfColour = new int[shelves.Count];
            int typeClasses = types.Count == 0 ? 0 : 1;
            int shelfClasses = 0;
            for (int round = 0; round <= types.Count + shelves.Count + 1; round++)
            {
                var shelfSigs = new string[shelves.Count];
                for (int s = 0; s < shelves.Count; s++)
                {
                    shelfSigs[s] = baseKeys[s] + "|" + string.Join("/", shelves[s].Layers.Select(layer =>
                        string.Join(",", layer.Select(x => string.IsNullOrEmpty(x) ? -1 : typeColour[typeIndex[x]])
                            .OrderBy(c => c))));
                }
                shelfColour = Rank(shelfSigs, out int newShelfClasses);

                var typeSigs = new string[types.Count];
                for (int t = 0; t < types.Count; t++)
                {
                    var occurrences = new List<string>();
                    for (int s = 0; s < shelves.Count; s++)
                    {
                        for (int d = 0; d < shelves[s].Layers.Count; d++)
                        {
                            int count = shelves[s].Layers[d].Count(x => x == types[t]);
                            if (count > 0) occurrences.Add($"{shelfColour[s]}.{d}.{count}");
                        }
                    }
                    occurrences.Sort(StringComparer.Ordinal);
                    typeSigs[t] = typeColour[t] + "|" + string.Join(";", occurrences);
                }
                var nextTypeColour = Rank(typeSigs, out int newTypeClasses);

                bool stable = newTypeClasses == typeClasses && newShelfClasses == shelfClasses;
                typeColour = nextTypeColour;
                typeClasses = newTypeClasses;
                shelfClasses = newShelfClasses;
                if (stable) break;
            }

            // Group types by colour; every order inside each group is a candidate.
            var groups = Enumerable.Range(0, types.Count).GroupBy(t => typeColour[t]).OrderBy(g => g.Key)
                .Select(g => g.ToArray()).ToList();
            long orders = 1;
            foreach (var g in groups)
            {
                for (int k = 2; k <= g.Length; k++)
                {
                    orders *= k;
                    if (orders > MaxOrders) break;
                }
                if (orders > MaxOrders) break;
            }
            exact = orders <= MaxOrders;

            string best = null;
            var label = new int[types.Count];
            foreach (var order in Orders(groups, exact))
            {
                for (int i = 0; i < order.Length; i++) label[order[i]] = i;
                var text = Serialise(shelves, baseKeys, typeIndex, label);
                if (best == null || string.CompareOrdinal(text, best) < 0) best = text;
            }
            return best ?? Serialise(shelves, baseKeys, typeIndex, label);
        }

        private static string Serialise(List<ShelfData> shelves, string[] baseKeys, Dictionary<string, int> typeIndex,
            int[] label)
        {
            var parts = new string[shelves.Count];
            for (int s = 0; s < shelves.Count; s++)
            {
                parts[s] = baseKeys[s] + "|" + string.Join("/", shelves[s].Layers.Select(layer =>
                    string.Join(",", layer.Select(x => string.IsNullOrEmpty(x) ? "." : "t" + label[typeIndex[x]].ToString("D2"))
                        .OrderBy(x => x, StringComparer.Ordinal))));
            }
            Array.Sort(parts, StringComparer.Ordinal);
            return string.Join(";", parts);
        }

        private static int[] Rank(string[] sigs, out int classes)
        {
            var distinct = sigs.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            classes = distinct.Count;
            var rank = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < distinct.Count; i++) rank[distinct[i]] = i;
            return sigs.Select(x => rank[x]).ToArray();
        }

        // Concatenations of one permutation per group (all of them when exhaustive, else just the given order).
        private static IEnumerable<int[]> Orders(List<int[]> groups, bool exhaustive)
        {
            if (!exhaustive)
            {
                yield return groups.SelectMany(g => g).ToArray();
                yield break;
            }

            var perGroup = groups.Select(g => Permutations(g).ToList()).ToList();
            var choice = new int[groups.Count];
            while (true)
            {
                yield return perGroup.SelectMany((perms, i) => perms[choice[i]]).ToArray();
                int k = groups.Count - 1;
                while (k >= 0 && ++choice[k] == perGroup[k].Count)
                {
                    choice[k] = 0;
                    k--;
                }
                if (k < 0) yield break;
            }
        }

        private static IEnumerable<int[]> Permutations(int[] items)
        {
            if (items.Length <= 1)
            {
                yield return items;
                yield break;
            }
            for (int i = 0; i < items.Length; i++)
            {
                var rest = items.Where((_, j) => j != i).ToArray();
                foreach (var tail in Permutations(rest))
                {
                    var result = new int[items.Length];
                    result[0] = items[i];
                    Array.Copy(tail, 0, result, 1, tail.Length);
                    yield return result;
                }
            }
        }
    }
}
