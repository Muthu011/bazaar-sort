using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Solver
{
    /// <summary>One level file processed by <see cref="SolverRunner.RunOnAll"/>.</summary>
    public sealed class LevelFileResult
    {
        public string Path { get; internal set; }
        public LevelAnalysis Analysis { get; internal set; }

        /// <summary>True when the file's solver block was written (it was missing or differed).</summary>
        public bool Written { get; internal set; }
    }

    /// <summary>
    /// Analyses every level JSON under a folder and writes each one's <c>solver</c> block (only that block; see
    /// <see cref="SolverBlockWriter"/>). Levels whose analysis is incomplete are reported and left untouched.
    /// The editor menu "Bazaar Sort/Run Solver on All Levels" calls this.
    /// </summary>
    public static class SolverRunner
    {
        public const string LevelsRoot = "Assets/Levels";

        /// <summary>Level files under <paramref name="root"/>, in ordinal path order, with forward slashes.</summary>
        public static IReadOnlyList<string> FindLevelFiles(string root = LevelsRoot)
        {
            if (!Directory.Exists(root)) return Array.Empty<string>();
            return Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        public static IReadOnlyList<LevelFileResult> RunOnAll(string root = LevelsRoot, bool write = true,
            SolverOptions options = null)
        {
            var results = new List<LevelFileResult>();
            foreach (var path in FindLevelFiles(root))
            {
                string json = File.ReadAllText(path);
                var analysis = LevelAnalyzer.AnalyzeJson(json, Path.GetFileName(path), options);
                var result = new LevelFileResult { Path = path, Analysis = analysis };
                var block = analysis.ToBlock();
                if (write && block != null && !block.Equals(SolverBlockWriter.Read(json)))
                {
                    File.WriteAllText(path, SolverBlockWriter.Write(json, block), new UTF8Encoding(false));
                    result.Written = true;
                }
                results.Add(result);
            }
            return results;
        }

        /// <summary>A plain-text report, one line per level plus duplicate hashes.</summary>
        public static string Summary(IReadOnlyList<LevelFileResult> results)
        {
            var text = new StringBuilder();
            text.AppendLine($"Bazaar Sort solver: {results.Count} level(s), seed {SolverOptions.DefaultSeed}, " +
                            $"{SolverOptions.DefaultPlays} plays each");
            foreach (var r in results)
            {
                text.AppendLine((r.Written ? "[written] " : "[unchanged] ") + r.Analysis);
            }
            foreach (var group in results.Where(r => r.Analysis.Hash != null).GroupBy(r => r.Analysis.Hash)
                         .Where(g => g.Count() > 1))
            {
                text.AppendLine($"DUPLICATE layout {group.Key}: {string.Join(", ", group.Select(r => r.Path))}");
            }
            return text.ToString();
        }
    }
}
