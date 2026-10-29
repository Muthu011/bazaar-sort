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
    /// Bands: <see cref="DifficultyBands"/>. Each level is analysed once per test run (an Inconclusive analysis is redone).
    /// </summary>
    public class LevelPackTests
    {
        // Per run, not static: the editor keeps statics between runs until a domain reload, and an Inconclusive
        // (budget-hit) analysis is never stored, so a timeout is re-analysed instead of replayed (issue #38).
        internal readonly LevelAnalysisCache Cache = new LevelAnalysisCache(
            path => LevelAnalyzer.AnalyzeJson(File.ReadAllText(path), Path.GetFileName(path)));

        [OneTimeSetUp]
        public void ClearCacheForThisRun() => Cache.Clear();

        private static IEnumerable<string> LevelFiles() => SolverRunner.FindLevelFiles();

        private LevelAnalysis Analyse(string path) => Cache.Get(path);

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
