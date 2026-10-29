using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Every level under Assets/Levels must be proven solvable, unique and in its win-rate band, and its stored
    /// <c>solver</c> block must match a fresh solve (run "Bazaar Sort/Run Solver on All Levels" after changing a level).
    /// Bands: <see cref="DifficultyBands"/>. Each level is analysed once per test run.
    /// </summary>
    public class LevelPackTests
    {
        private static readonly Dictionary<string, LevelAnalysis> Cache = new Dictionary<string, LevelAnalysis>();

        private static IEnumerable<string> LevelFiles() => SolverRunner.FindLevelFiles();

        private static LevelAnalysis Analyse(string path)
        {
            if (!Cache.TryGetValue(path, out var analysis))
            {
                analysis = LevelAnalyzer.AnalyzeJson(File.ReadAllText(path), Path.GetFileName(path));
                Cache[path] = analysis;
            }
            return analysis;
        }

        [Test]
        public void Pack_HasLevels()
        {
            CollectionAssert.IsNotEmpty(LevelFiles());
        }

        /// <summary>12 shelves is the cap for the whole game (Muthu, 2026-10-29, issue #40).</summary>
        [TestCaseSource(nameof(LevelFiles))]
        public void Level_HasAtMostTwelveShelves(string path)
        {
            var load = Game.Levels.LevelLoader.Load(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            Assert.IsTrue(load.Success, "level loads");
            Assert.LessOrEqual(load.Board.ShelfCount, 12, "at most 12 shelves per level");
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void Level_IsSolvable(string path)
        {
            var analysis = Analyse(path);
            Assert.IsNull(analysis.Problem, analysis.Problem);
            Assert.AreEqual(Solvability.Solvable, analysis.Solvability, analysis.ToString());
            Assert.IsTrue(analysis.Complete, "analysis incomplete: " + analysis);
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void Level_EstWinRateIsInItsBand(string path)
        {
            var analysis = Analyse(path);
            Assert.IsTrue(analysis.HasBand, $"no win-rate band for index {analysis.Index}, role '{analysis.Role}'");
            Assert.IsTrue(analysis.InBand,
                $"estWinRate {SolverBlock.Round(analysis.Plays.WinRate):0.000} is outside {analysis.Band}. {analysis}");
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void Level_StoredSolverBlockMatchesFreshSolve(string path)
        {
            var stored = SolverBlockWriter.Read(File.ReadAllText(path));
            Assert.IsNotNull(stored, "no solver block; run Bazaar Sort/Run Solver on All Levels");
            var fresh = Analyse(path).ToBlock();
            Assert.IsNotNull(fresh, "analysis incomplete");
            Assert.AreEqual(fresh.ToJson(), stored.ToJson(), "stale solver block; run Bazaar Sort/Run Solver on All Levels");
        }

        [Test]
        public void Pack_HasNoDuplicateLayouts()
        {
            var duplicates = LevelFiles()
                .Select(path => (path, hash: Analyse(path).Hash))
                .GroupBy(x => x.hash)
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.path))}")
                .ToList();
            CollectionAssert.IsEmpty(duplicates);
        }
    }
}
