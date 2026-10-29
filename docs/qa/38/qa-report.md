# QA report: issue #38 (pack-test cache, solver speed)

Branch `task/38-pack-test-cache`, 3 commits on origin/main d695f0a (tip cad2eda). QA by the qa-tester agent, 2026-10-29.
Editor: Unity 6000.6.6f1 via Unity MCP. No screenshots: no screen or level changed (test/solver code only).

## 1. Test runs

| Run | Result | Time |
|---|---|---|
| EditMode `!Performance`, run 1 (after a compile/domain reload) | 593 / 593 passed | 96.7 s |
| EditMode `!Performance`, run 2 (same editor session, no reload) | 593 / 593 passed | 94.9 s |
| PlayMode (all) | 60 / 60 passed | 27.5 s |
| Performance, batch mode (`scripts/run-performance-tests.sh`) | 1 / 1 passed (`Analyse_FourteenShelfLevelWithLocks_UnderTenSeconds`) | 1.52 s (limit 10 s) |

Run 2 proves the per-run behaviour: its per-test durations show `LevelPackTests` re-analysed the levels (L10
`Level_EstWinRateIsInItsBand` 1.96 s, L15 2.28 s; the other L10/L15 tests 0 s because they read the cache within the run).
Total LevelPackTests time in run 2 was 6.0 s, so nothing carried over from run 1 (an old static cache would have made
run 2 near zero for these). New tests (ChildScoringTests x19, LevelAnalysisCacheTests x4) all passed.

## 2. Console
Zero errors and zero warnings after the refresh/compile, after both EditMode runs and after PlayMode. (One transient
`MCP-FOR-UNITY ... WebSocket is not initialised` warning appeared once during the domain reload right after the
refresh; it is the MCP bridge, not project code, and was gone on the later read. One MCP poll also reported a
plugin session disconnect mid-run; the job still finished 593/593.)

## 3. Solver blocks (own harness, built from the branch's Core + Levels + Editor/Solver sources)
`git diff origin/main -- Assets/Levels` is empty. Harness `/private/tmp/claude-501/-Users-muthukv-Desktop-Workspace-unity-bazar-sort/3d268817-091c-4973-9b93-5ef4c5326357/scratchpad/qa38/` (Program.cs: fresh `AnalyzeJson` block JSON compared with the stored block, default options).
All 15 levels: Complete, fresh block equals the stored block at Threads 1 AND Threads 4 (30 / 30 equal).

## 4. Timings (L10 / L15, default options, Threads auto, Mono, whole-level analysis)

| Build | Normal run | `taskpolicy -b` (throttled) |
|---|---|---|
| origin/main (baseline, own build) | 6.01 s / 5.52 s | 30.16 s INCONCLUSIVE / 28.46 s Complete |
| branch, run 1 | 2.17 s / 2.30 s | 13.32 s / 11.47 s |
| branch, run 2 | 2.21 s / 2.34 s | 13.52 s / 11.46 s |

Throttled margin to the unchanged 30 s budget is now about 2.2x (was none: the baseline L10 timed out and L15 was
at 28.5 s). The baseline reproduces the issue's problem. Single-thread whole pack: 15 levels, slowest L15 6.9 s.

## 5. Acceptance criteria
1. Inconclusive never cached, later run re-analyses: MET. `LevelAnalysisCache.Get` stores only `Outcome != Inconclusive`;
   unit test `Get_InconclusiveAnalysis_IsNotCachedAndIsAnalysedAgain` (real timed-out analysis via 0 s limit) passes.
2. Cache clearly per run: MET. Instance field of the fixture plus `[OneTimeSetUp] ClearCacheForThisRun`; test
   `LevelPackTests_StartsEveryRunWithAnEmptyCache`; run 2 above is the end-to-end evidence.
3. Robust against throttling without weakening the 30 s budget: MET via option (b). 2.2x margin throttled. Option (a)
   (batch mode for the pack tests) not done, as stated. `LevelTimeLimitSeconds = 30` and `LevelNodeBudget = 40_000_000` untouched.
4. No change to win-rate targets, budgets or thresholds: MET. The diff does not touch SolverTypes.cs (budgets, seed
   20270112, 1000 plays, MaxPlayMoves 300), DifficultyBands.cs, Assets/Levels, docs, SolverTests.cs, SolverPerformanceTests or Core.
   The 10 s performance limit is unchanged.

## 6. Diff review
- Core (`Assets/Scripts/Core`) untouched. SolverTests (A*-vs-BFS reference tests) unchanged. Reference mode
  (`ReferenceMode`) bypasses all new logic: no prediction, no heuristic, one round with threshold int.MaxValue, so the BFS
  reference remains an independent search.
- ChildScoringTests really compare against Core: `SearchContext.Apply` clones and uses `Board.TryMove`; for every move
  of 6 random walks x 200 steps on all 15 levels and 4 synthetic boards (incl. 1-3 locked shelves) they assert
  `ChildHeuristic`, `ChildCheapKey`, `PredictChildHeuristic`, `PredictedChildHasLegalMove` equal the values read from the
  real child board, and that clears return -1. They also assert that scoring and prediction were exercised (>0).
- Partial Expansion A*: sound. Each round pushes children with f in (previous, threshold]; the parent is re-queued at the
  minimum f it put off, so every child is pushed before any state with higher f is expanded. Correctness does not need
  consistency (children with f <= previous were pushed or deduplicated in an earlier round). A won child is detected in the
  parent's first round (f = depth+1 <= parent f since h >= 1 for a non-won board). Stale re-queues are dropped when a shallower copy is closed.
  Node budget now counts first-round expansions, time check counts rounds (every 256), budgets unchanged in value.
- LivenessFacts (ConcurrentDictionary, add-only, TryAdd): facts are properties of the board, so sharing only saves work.
  Oracle budget exhaustion yields "unknown", which makes the analysis Inconclusive rather than a different Complete result,
  so no silent nondeterminism; Threads 1 vs 4 equality (section 3) confirms. Note: whether an analysis finishes
  within the oracle's per-search node budget can depend on thread timing, as before.
- BoardCache now reuses boards and `Clear()`s per search; documented that a returned board is valid only until the next Get.

## 7. Design-principle softening (solver predicts plain-move children instead of always asking Core)
Assessment: LOW to MODERATE risk, acceptable. The prediction duplicates one Core rule set (a plain move: the item leaves
one front row for another, no triple, no layer reveal). If Core rules change (for example item stacking, slot counts,
lock semantics) the prediction can drift. Failure is mostly LOUD:
- Shortest search: every predicted child that is kept is built by Core `TryMove` and compared; a mismatch throws
  `InvalidOperationException("Solver predicted heuristic ...")`.
- Dead-end oracle: a predicted node is rebuilt by Core when expanded and `CheckPrediction` throws on a mismatch of cheap
  key, heuristic or legal-move status.
- ChildScoringTests fail in CI for any drift on all stored levels plus synthetic locked boards.
Residual SILENT gaps: a predicted child that is only discarded (put off, or considered already pushed) or a predicted
"stuck" child in the oracle is never built, so a drifted prediction affecting just those could, in principle, skip a child
without an assertion (worst case: a longer-than-minimal solution, or a wrong dead-end verdict). Only ChildScoringTests
cover that; they are broad, so the exposure is small. Suggestion (not blocking): a rare sampled cross-check (for example every 1000th
predicted-but-skipped child built and compared), or a note in docs that changing Core move rules requires running ChildScoringTests.

## 8. Findings (none blocking)
- Minor: `PlaySimulator.cs` doc comment line is now overlong (cosmetic).
- Minor: `LevelPackTests.Cache` is `internal` instance state and the test `LevelPackTests_StartsEveryRunWithAnEmptyCache`
  instantiates the fixture directly; fine, but note NUnit already makes a fresh fixture per run, so the OneTimeSetUp clear is belt-and-braces (which is fine and what the issue asked for).
- Throttled analysis is 11.5-13.5 s; if the pack grows with harder levels the 30 s budget will again be the limit.
- Option (a), batch-mode pack tests, remains available if throttling gets worse.
- The author's claimed timings reproduce: L10 6.0 -> 2.2 s, L15 5.5 -> 2.3 s normal; throttled L10 13.3-13.5 s (author 11.7 s), L15 11.5 s (author 9.6-10.4 s), all within budget.

RESULT: PASS
