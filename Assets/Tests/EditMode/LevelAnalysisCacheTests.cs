using Game.Core;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>The per-run analysis cache of <see cref="LevelPackTests"/> (issue #38).</summary>
    public class LevelAnalysisCacheTests
    {
        private static Board SmallBoard() => Board.FromLayers(new[]
        {
            new[] { new[] { "a", "b", "a" } },
            new[] { new[] { "b", "a", null } },
            new[] { new[] { "b", null, null } },
        });

        private static LevelAnalysis Complete() => LevelAnalyzer.AnalyzeBoard(SmallBoard(), null, null, "small");

        // A time limit of 0 runs out at once: the analysis a throttled editor gets.
        private static LevelAnalysis TimedOut() =>
            LevelAnalyzer.AnalyzeBoard(SmallBoard(), null, new SolverOptions { LevelTimeLimitSeconds = 0 }, "small");

        [Test]
        public void Get_CompleteAnalysis_IsAnalysedOnce()
        {
            int calls = 0;
            var cache = new LevelAnalysisCache(_ => { calls++; return Complete(); });
            Assert.AreEqual(AnalysisOutcome.Complete, cache.Get("x").Outcome);
            Assert.AreEqual(AnalysisOutcome.Complete, cache.Get("x").Outcome);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, cache.Count);
        }

        [Test]
        public void Get_InconclusiveAnalysis_IsNotCachedAndIsAnalysedAgain()
        {
            int calls = 0;
            var cache = new LevelAnalysisCache(_ => ++calls == 1 ? TimedOut() : Complete());
            Assert.AreEqual(AnalysisOutcome.Inconclusive, cache.Get("x").Outcome);
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(AnalysisOutcome.Complete, cache.Get("x").Outcome, "the timeout must not be replayed");
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Clear_ForgetsEveryAnalysis()
        {
            int calls = 0;
            var cache = new LevelAnalysisCache(_ => { calls++; return Complete(); });
            cache.Get("x");
            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            cache.Get("x");
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void LevelPackTests_StartsEveryRunWithAnEmptyCache()
        {
            var fixture = new LevelPackTests();
            fixture.Cache.Get(SolverRunner.FindLevelFiles()[0]);
            Assume.That(fixture.Cache.Count, Is.EqualTo(1));
            fixture.ClearCacheForThisRun();
            Assert.AreEqual(0, fixture.Cache.Count);
        }
    }
}
