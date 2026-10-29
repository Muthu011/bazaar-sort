using System;
using System.Collections.Generic;
using Game.Solver;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Level analyses shared by the tests of one fixture run (issue #38). Only settled answers are kept: an
    /// Inconclusive analysis (a budget ran out, e.g. because the editor was throttled) is returned but not stored, so
    /// the next test that asks re-analyses the level instead of replaying the timeout. The owner clears it at the start
    /// of every run, so nothing carries over between runs in an editor that does not reload code.
    /// </summary>
    internal sealed class LevelAnalysisCache
    {
        private readonly Func<string, LevelAnalysis> _analyse;
        private readonly Dictionary<string, LevelAnalysis> _byPath = new Dictionary<string, LevelAnalysis>();

        public LevelAnalysisCache(Func<string, LevelAnalysis> analyse)
        {
            _analyse = analyse ?? throw new ArgumentNullException(nameof(analyse));
        }

        public int Count => _byPath.Count;

        public LevelAnalysis Get(string path)
        {
            if (_byPath.TryGetValue(path, out var analysis)) return analysis;
            analysis = _analyse(path);
            if (analysis.Outcome != AnalysisOutcome.Inconclusive) _byPath[path] = analysis;
            return analysis;
        }

        public void Clear() => _byPath.Clear();
    }
}
