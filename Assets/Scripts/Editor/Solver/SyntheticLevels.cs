using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Solver
{
    /// <summary>
    /// Deterministic generated boards for solver benchmarks. Not real levels: item ids are "t00", "t01", ... and the
    /// layouts are never shipped.
    /// </summary>
    public static class SyntheticLevels
    {
        /// <summary>
        /// Deals <paramref name="types"/> x 3 items, shuffled with <paramref name="seed"/>, onto
        /// <paramref name="shelves"/> shelves: full hidden layers go behind the first <paramref name="hiddenLayers"/>
        /// shelves (one layer each, wrapping round for more), and the rest are spread over the front rows as evenly as
        /// possible. Redeals (next seed) until no front row starts as a triple.
        /// Returns <c>layers[shelf][layer][slot]</c> for <see cref="Board.FromLayers"/>.
        /// </summary>
        public static string[][][] Deal(int shelves, int types, int hiddenLayers, ulong seed)
        {
            int items = types * GameRules.MatchCount;
            int hiddenItems = hiddenLayers * Shelf.SlotCount;
            int frontItems = items - hiddenItems;
            if (frontItems < 0 || frontItems >= shelves * Shelf.SlotCount)
            {
                throw new ArgumentException("Front rows must hold the remaining items and keep at least one free slot.");
            }

            for (ulong attempt = 0; ; attempt++)
            {
                var rng = new SplitMix64(Hashing.Mix(seed + attempt));
                var bag = new List<string>(items);
                for (int t = 0; t < types; t++)
                {
                    for (int k = 0; k < GameRules.MatchCount; k++) bag.Add("t" + t.ToString("D2"));
                }
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = rng.NextInt(i + 1);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }

                var layout = new List<string[]>[shelves];
                for (int s = 0; s < shelves; s++) layout[s] = new List<string[]> { new string[Shelf.SlotCount] };

                int next = 0;
                for (int h = 0; h < hiddenLayers; h++)
                {
                    var row = new string[Shelf.SlotCount];
                    for (int k = 0; k < Shelf.SlotCount; k++) row[k] = bag[next++];
                    layout[h % shelves].Add(row);
                }

                // Front rows: fill slot 0 of every shelf, then slot 1, then slot 2.
                for (int slot = 0; slot < Shelf.SlotCount && next < items; slot++)
                {
                    for (int s = 0; s < shelves && next < items; s++) layout[s][0][slot] = bag[next++];
                }

                bool frontTriple = false;
                for (int s = 0; s < shelves; s++)
                {
                    var front = layout[s][0];
                    if (front[0] != null && front[0] == front[1] && front[1] == front[2]) frontTriple = true;
                }
                if (frontTriple) continue;

                var result = new string[shelves][][];
                for (int s = 0; s < shelves; s++) result[s] = layout[s].ToArray();
                return result;
            }
        }

        public static Board Build(string[][][] layers)
        {
            return Build(layers, null);
        }

        /// <summary>
        /// Like <see cref="Build(string[][][])"/>, with locked shelves: <paramref name="lockClears"/>[i] is shelf i's
        /// lockClears (0 for a normal shelf); null for none.
        /// </summary>
        public static Board Build(string[][][] layers, IReadOnlyList<int> lockClears)
        {
            if (lockClears != null && lockClears.Count != layers.Length)
            {
                throw new ArgumentException("One lockClears value per shelf.", nameof(lockClears));
            }
            var shelves = new Shelf[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                shelves[i] = new Shelf(layers[i], "s" + (i + 1), lockClears == null ? 0 : lockClears[i]);
            }
            return new Board(shelves);
        }
    }
}
