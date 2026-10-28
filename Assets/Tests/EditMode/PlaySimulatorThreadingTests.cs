using System.IO;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Issue #31: the simulated plays run on several threads; the stored numbers must not depend on how many.
    /// </summary>
    public class PlaySimulatorThreadingTests
    {
        private static void AssertSameNumbers(LevelAnalysis one, LevelAnalysis many)
        {
            Assert.AreEqual(AnalysisOutcome.Complete, one.Outcome, one.InconclusiveReason);
            Assert.AreEqual(AnalysisOutcome.Complete, many.Outcome, many.InconclusiveReason);
            Assert.AreEqual(one.ToBlock().ToJson(), many.ToBlock().ToJson());
            Assert.AreEqual(one.Plays.Wins, many.Plays.Wins);
            Assert.AreEqual(one.Plays.LossesStuck, many.Plays.LossesStuck);
            Assert.AreEqual(one.Plays.LossesCircling, many.Plays.LossesCircling);
            Assert.AreEqual(one.Plays.LossesTooLong, many.Plays.LossesTooLong);
            Assert.AreEqual(one.Plays.AliveStates, many.Plays.AliveStates);
            Assert.AreEqual(one.Plays.DeadStates, many.Plays.DeadStates);
            Assert.AreEqual(one.Plays.UnknownStates, many.Plays.UnknownStates);
        }

        [Test]
        public void Analysis_OneThreadAndSeveral_GiveTheSameNumbers_LockedLevel()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(10, 12, 3, 5), new[] { 0, 0, 0, 0, 0, 0, 0, 0, 2, 1 });

            var one = LevelAnalyzer.AnalyzeBoard(board, null, new SolverOptions { Threads = 1 });
            var many = LevelAnalyzer.AnalyzeBoard(board, null, new SolverOptions { Threads = 3 });

            AssertSameNumbers(one, many);
        }

        [Test]
        public void Analysis_OneThreadAndSeveral_GiveTheSameNumbers_ShippedLevel()
        {
            string json = File.ReadAllText("Assets/Levels/World1/w1_l013.json");

            var one = LevelAnalyzer.AnalyzeJson(json, "w1_l013.json", new SolverOptions { Threads = 1 });
            var many = LevelAnalyzer.AnalyzeJson(json, "w1_l013.json", new SolverOptions { Threads = 4 });

            AssertSameNumbers(one, many);
        }
    }
}
