using System.Diagnostics;
using System.IO;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Issue #31: one budget (node limit + wall clock) covers every phase of a level analysis, and running out of it
    /// gives an Inconclusive analysis quickly instead of a hang.
    /// </summary>
    public class SolverBudgetTests
    {
        // The draft level that hung the solver for 15+ minutes (8 shelves, 2 locked). Not a shipped level.
        private const string HangingDraft = "Assets/Tests/EditMode/LevelSamples/solver_locked_draft_31.json";

        [Test]
        public void Defaults_AreThirtySecondsPerLevel_AndUnchangedSeedAndPlays()
        {
            var options = SolverOptions.Default;
            Assert.AreEqual(30.0, options.LevelTimeLimitSeconds);
            Assert.Greater(options.LevelNodeBudget, 0);
            Assert.AreEqual(20270112UL, options.Seed);
            Assert.AreEqual(1000, options.Plays);
        }

        [Test]
        public void Analysis_LevelNodeBudgetRunsOut_IsInconclusive()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(8, 9, 2, 7));

            var analysis = LevelAnalyzer.AnalyzeBoard(board, null, new SolverOptions { LevelNodeBudget = 50 });

            Assert.AreEqual(AnalysisOutcome.Inconclusive, analysis.Outcome);
            Assert.IsFalse(analysis.Complete);
            Assert.IsNull(analysis.ToBlock());
            StringAssert.Contains("node budget", analysis.InconclusiveReason);
            Assert.AreEqual(Solvability.Unknown, analysis.Solvability);
        }

        [Test]
        public void Analysis_LevelTimeLimitRunsOut_IsInconclusiveAndReturnsAtOnce()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(8, 9, 2, 7));
            var stopwatch = Stopwatch.StartNew();

            var analysis = LevelAnalyzer.AnalyzeBoard(board, null, new SolverOptions { LevelTimeLimitSeconds = 0 });

            Assert.AreEqual(AnalysisOutcome.Inconclusive, analysis.Outcome);
            StringAssert.Contains("time limit", analysis.InconclusiveReason);
            Assert.Less(stopwatch.Elapsed.TotalSeconds, 5);
        }

        [Test]
        public void Analysis_BudgetRunsOutDuringDeadEndSearch_StopsThere()
        {
            // The shortest solution of the draft is found in well under a second; the dead-end searches behind the
            // simulated plays are what used to run for ever. With a 3 s level budget the analysis must stop in the
            // play/dead-end phase, report Inconclusive with the reason, and not take much longer than the budget.
            var stopwatch = Stopwatch.StartNew();
            var analysis = LevelAnalyzer.AnalyzeJson(File.ReadAllText(HangingDraft), "solver_locked_draft_31.json",
                new SolverOptions { LevelTimeLimitSeconds = 3 });
            double seconds = stopwatch.Elapsed.TotalSeconds;
            TestContext.WriteLine($"{analysis} ({seconds:0.00} s)");

            Assert.IsNull(analysis.Problem, analysis.Problem);
            Assert.AreEqual(Solvability.Solvable, analysis.Solvability, "the shortest-solution phase should finish");
            Assert.AreEqual(AnalysisOutcome.Inconclusive, analysis.Outcome);
            StringAssert.Contains("time limit", analysis.InconclusiveReason);
            Assert.IsNotNull(analysis.Plays);
            Assert.Less(analysis.Plays.PlaysRun, analysis.Plays.Plays);
            Assert.Less(seconds, 10);
        }

        [Test]
        public void Analysis_HangingLockedDraft_FinishesWithinTheDefaultBudget()
        {
            var stopwatch = Stopwatch.StartNew();
            var analysis = LevelAnalyzer.AnalyzeJson(File.ReadAllText(HangingDraft), "solver_locked_draft_31.json");
            double seconds = stopwatch.Elapsed.TotalSeconds;
            string report = $"locked draft (issue #31): {analysis.Outcome}; {analysis} (measured {seconds:0.00} s)";
            UnityEngine.Debug.Log(report);
            TestContext.WriteLine(report);

            Assert.IsNull(analysis.Problem, analysis.Problem);
            Assert.AreNotEqual(AnalysisOutcome.NotAnalysed, analysis.Outcome);
            if (analysis.Outcome == AnalysisOutcome.Inconclusive) Assert.IsNotNull(analysis.InconclusiveReason);
            Assert.Less(seconds, SolverOptions.Default.LevelTimeLimitSeconds + 10);
        }

        [Test]
        public void ShortestSolver_LevelBudgetRunsOut_ReportsUnknown()
        {
            var board = SyntheticLevels.Build(SyntheticLevels.Deal(8, 9, 2, 7));

            var result = ShortestSolver.Solve(board, null, new SolverOptions { LevelNodeBudget = 20 });

            Assert.AreEqual(Solvability.Unknown, result.Solvability);
            StringAssert.Contains("node budget", result.Reason);
        }

        [Test]
        public void Budget_OnceExhausted_StaysExhausted()
        {
            var budget = new SearchBudget(3, 60);
            Assert.IsTrue(budget.Charge());
            Assert.IsTrue(budget.Charge(2));
            Assert.IsFalse(budget.Charge());
            Assert.IsTrue(budget.Exhausted);
            Assert.IsFalse(budget.CheckClock());
            StringAssert.Contains("node budget", budget.Reason);
        }
    }
}
