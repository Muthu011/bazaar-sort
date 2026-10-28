using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Game.Solver
{
    /// <summary>The <c>solver</c> block of a level file (docs/level-schema.md). Rates are kept to 3 decimals.</summary>
    public sealed class SolverBlock : IEquatable<SolverBlock>
    {
        public SolverBlock(bool solvable, int minMoves, double deadEndRate, double estWinRate, string hash)
        {
            Solvable = solvable;
            MinMoves = minMoves;
            DeadEndRate = Round(deadEndRate);
            EstWinRate = Round(estWinRate);
            Hash = hash;
        }

        public bool Solvable { get; }
        public int MinMoves { get; }
        public double DeadEndRate { get; }
        public double EstWinRate { get; }
        public string Hash { get; }

        public static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

        public bool Equals(SolverBlock other)
        {
            return other != null && Solvable == other.Solvable && MinMoves == other.MinMoves
                && Math.Abs(DeadEndRate - other.DeadEndRate) < 1e-9 && Math.Abs(EstWinRate - other.EstWinRate) < 1e-9
                && string.Equals(Hash, other.Hash, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as SolverBlock);

        public override int GetHashCode() => (Hash ?? string.Empty).Length ^ MinMoves;

        /// <summary>One-line JSON, in the same style as the shelf lines of the level files.</summary>
        public string ToJson()
        {
            return "{ \"solvable\": " + (Solvable ? "true" : "false")
                + ", \"minMoves\": " + MinMoves.ToString(CultureInfo.InvariantCulture)
                + ", \"deadEndRate\": " + FormatRate(DeadEndRate)
                + ", \"estWinRate\": " + FormatRate(EstWinRate)
                + ", \"hash\": \"" + Hash + "\" }";
        }

        public override string ToString() => ToJson();

        private static string FormatRate(double value) => value.ToString("0.0##", CultureInfo.InvariantCulture);
    }

    /// <summary>Reads and writes only the <c>solver</c> block of a level file, leaving every other byte as it was.</summary>
    public static class SolverBlockWriter
    {
        // The solver block holds only scalars, so it never contains nested braces.
        private static readonly Regex Existing = new Regex("\"solver\"\\s*:\\s*(\\{[^{}]*\\}|null)", RegexOptions.CultureInvariant);

        /// <summary>The stored block, or null when the file has none.</summary>
        public static SolverBlock Read(string json)
        {
            var root = JObject.Parse(json);
            if (!(root["solver"] is JObject solver)) return null;
            return new SolverBlock(
                solver.Value<bool?>("solvable") ?? false,
                solver.Value<int?>("minMoves") ?? -1,
                solver.Value<double?>("deadEndRate") ?? -1,
                solver.Value<double?>("estWinRate") ?? -1,
                solver.Value<string>("hash"));
        }

        /// <summary>
        /// Returns <paramref name="json"/> with its <c>solver</c> block replaced, or appended as the last field when
        /// missing. Throws if anything other than the solver block would change.
        /// </summary>
        public static string Write(string json, SolverBlock block)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (block == null) throw new ArgumentNullException(nameof(block));

            var replacement = "\"solver\": " + block.ToJson();
            string result;
            var matches = Existing.Matches(json);
            if (matches.Count > 1) throw new FormatException("More than one \"solver\" field.");
            if (matches.Count == 1)
            {
                result = json.Substring(0, matches[0].Index) + replacement
                    + json.Substring(matches[0].Index + matches[0].Length);
            }
            else
            {
                int close = json.LastIndexOf('}');
                if (close < 0) throw new FormatException("Not a JSON object.");
                int last = close - 1;
                while (last >= 0 && char.IsWhiteSpace(json[last])) last--;
                string newline = json.Contains("\r\n") ? "\r\n" : "\n";
                string separator = json[last] == '{' ? string.Empty : ",";
                result = json.Substring(0, last + 1) + separator + newline + "  " + replacement
                    + json.Substring(last + 1);
            }

            Verify(json, result, block);
            return result;
        }

        private static void Verify(string before, string after, SolverBlock block)
        {
            var a = JObject.Parse(before);
            var b = JObject.Parse(after);
            a.Remove("solver");
            b.Remove("solver");
            if (!JToken.DeepEquals(a, b)) throw new InvalidOperationException("Writing the solver block changed other fields.");
            if (!block.Equals(Read(after))) throw new InvalidOperationException("The written solver block does not read back.");
        }
    }
}
