using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Levels;

namespace Game.Solver
{
    /// <summary>Everything the solver knows about one level.</summary>
    public sealed class LevelAnalysis
    {
        public string Id { get; internal set; }
        public string SourceName { get; internal set; }
        public int Index { get; internal set; }
        public string Role { get; internal set; }

        /// <summary>Why the level could not be analysed (load errors, unmodelled features); null when it was.</summary>
        public string Problem { get; internal set; }

        public SolveResult Shortest { get; internal set; }
        public PlayStats Plays { get; internal set; }
        public string Hash { get; internal set; }
        public bool HashExact { get; internal set; } = true;
        public double Seconds { get; internal set; }

        public bool HasBand { get; internal set; }
        public WinRateBand Band { get; internal set; }

        public Solvability Solvability => Shortest?.Solvability ?? Solvability.Unknown;

        /// <summary>
        /// True when every number is proven: no load problem, solvability known, dead-end rate fully classified, exact
        /// hash. Only complete analyses are written to level files.
        /// </summary>
        public bool Complete => Problem == null && Shortest != null && Shortest.Solvability != Solvability.Unknown
            && Plays != null && Plays.DeadEndComplete && HashExact;

        public bool InBand => HasBand && Plays != null && Band.Contains(SolverBlock.Round(Plays.WinRate));

        /// <summary>The block to store, or null when the analysis is not complete.</summary>
        public SolverBlock ToBlock()
        {
            if (!Complete) return null;
            return new SolverBlock(Solvability == Solvability.Solvable, Shortest.MinMoves, Plays.DeadEndRate,
                Plays.WinRate, Hash);
        }

        public override string ToString()
        {
            if (Problem != null) return $"{Id ?? SourceName}: NOT ANALYSED: {Problem}";
            string solvable = Solvability == Solvability.Unknown ? $"unknown ({Shortest?.Reason})" : Solvability.ToString();
            string band = HasBand ? $"{Band} {(InBand ? "ok" : "OUT OF BAND")}" : $"no band for role '{Role}'";
            string dead = Plays == null ? "-" : Plays.DeadEndComplete ? $"{SolverBlock.Round(Plays.DeadEndRate):0.000}"
                : $"{SolverBlock.Round(Plays.DeadEndRate):0.000} ({Plays.UnknownStates} states unknown)";
            return $"{Id}: {solvable}, minMoves {Shortest?.MinMoves}, deadEndRate {dead}, " +
                   $"estWinRate {SolverBlock.Round(Plays?.WinRate ?? 0):0.000} ({Plays?.Wins}/{Plays?.Plays}; " +
                   $"stuck {Plays?.LossesStuck}, circling {Plays?.LossesCircling}, too long {Plays?.LossesTooLong}), " +
                   $"band {band}, hash {Hash}, {Seconds:0.00} s";
        }
    }

    /// <summary>Runs the shortest-solution search, the simulated plays and the layout hash for one level.</summary>
    public static class LevelAnalyzer
    {
        public static LevelAnalysis AnalyzeJson(string json, string sourceName = null, SolverOptions options = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var analysis = new LevelAnalysis { SourceName = sourceName };
            var load = LevelLoader.Load(json, sourceName);
            if (load.Level != null)
            {
                analysis.Id = load.Level.Id;
                analysis.Index = load.Level.Index ?? 0;
                analysis.Role = load.Level.Role;
            }
            if (!load.Success)
            {
                analysis.Problem = "level does not load: " + string.Join("; ", load.Errors.Select(e => e.Message));
                return analysis;
            }

            var level = load.Level;
            if (level.Trays.Count > 0 || level.Wrapped.Count > 0)
            {
                analysis.Problem = "trays and wrapped items are not modelled by the board yet, so the solver can't rate this level";
                return analysis;
            }

            analysis.Hash = LayoutHash.Compute(level, out bool exact);
            analysis.HashExact = exact;
            analysis.HasBand = DifficultyBands.TryGetBand(analysis.Index, analysis.Role, out var band);
            analysis.Band = band;

            var pinned = level.Shelves.Select(s => (s.Kind ?? ShelfKinds.Normal) != ShelfKinds.Normal).ToList();
            Run(analysis, load.Board, pinned, options ?? SolverOptions.Default);
            analysis.Seconds = stopwatch.Elapsed.TotalSeconds;
            return analysis;
        }

        /// <summary>Analyses a board built in code (tests, benchmarks). No hash or band.</summary>
        public static LevelAnalysis AnalyzeBoard(Board board, IReadOnlyList<bool> pinnedShelves = null,
            SolverOptions options = null, string id = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var analysis = new LevelAnalysis { Id = id };
            Run(analysis, board, pinnedShelves, options ?? SolverOptions.Default);
            analysis.Seconds = stopwatch.Elapsed.TotalSeconds;
            return analysis;
        }

        private static void Run(LevelAnalysis analysis, Board board, IReadOnlyList<bool> pinned, SolverOptions options)
        {
            var context = new SearchContext(board, pinned);
            analysis.Shortest = ShortestSolver.Solve(context, options);

            var oracle = new LivenessOracle(context, options.LivenessNodeBudget);
            if (analysis.Shortest.Solvability == Solvability.Solvable)
            {
                // Every state on a winning line is alive.
                var replay = board;
                oracle.MarkAlive(context.Key(replay));
                foreach (var move in analysis.Shortest.Solution)
                {
                    replay = SearchContext.Apply(replay, move);
                    if (!replay.IsWon) oracle.MarkAlive(context.Key(replay));
                }
            }
            analysis.Plays = PlaySimulator.Run(context, options, oracle);
        }
    }
}
