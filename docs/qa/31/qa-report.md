# QA report: issue #31 solver budget fix (branch task/31-solver-locked-levels)

## Timings: 14-shelf locked analysis (Analyse_FourteenShelfLevelWithLocks_UnderTenSeconds)

| Run | Context | Test time | Solver-reported | Unity pid 53589 priority (ps pri) |
|---|---|---|---|---|
| 1 | alone (with Solve_FourteenShelf test), via run_tests | 3.45 s | 3.40 s, 1,802,046 boards | before 63, after 63 |
| 2 | alone, repeated | 3.41 s | 3.38 s, 1,801,289 boards | before 63, after 63 |
| 3 | inside full EditMode suite | 16.83 s (assert saw 16.66) | 16.66 s, 1,798,465 boards, A* 3.81 s | 4 during/after the run (63 when idle-polled earlier) |

Idle editor sat at pri 4 before the first run; it was 63 whenever MCP had just activated it. During the full suite it was 4.
Same work (about 1.8M boards) took 3.4 s alone and 16.7 s in the suite: 4.9x slower. The 10 s threshold fails in the exact scenario that the Definition of Done uses (full suite).
The author's throttling explanation is consistent with the priority readings but not proven; either way the criterion-4 test is not reliable in the full-suite run.

## Test counts

| Suite | Total | Passed | Failed | Duration |
|---|---|---|---|---|
| EditMode (full) | 502 | 501 | 1 | 129.4 s |
| PlayMode (full) | 47 | 47 | 0 | 25.8 s |
| EditMode LevelPackTests | 44 | 44 | 0 | 18.8 s |

Only failure: Game.Tests.EditMode.SolverPerformanceTests.Analyse_FourteenShelfLevelWithLocks_UnderTenSeconds, "Expected: less than 10.0d But was: 16.66".

## Acceptance criteria

1. Hard budget (40M boards / 30 s, all phases, Inconclusive): met by code (SearchBudget shared by ShortestSolver, oracle, PlaySimulator; AnalysisOutcome.Inconclusive with reason). SolverBudgetTests pass.
2. Hash includes lock counters: met (StateKeyTests pass).
3. Hash ignores shelf order and type identity: met (StateKeyTests pass).
4. 14-shelf level under 10 s proved by Edit Mode test: NOT MET reliably. Passes 3.4 s alone (twice), fails 16.7 s in full suite.
5. Edit Mode tests for budget, lock counters, order, renaming: met, all pass.
6. DifficultyBands, policy, seed 20270112, 1000 plays, 5-minute limit unchanged: met. Diff does not touch DifficultyBands; Defaults test asserts seed and plays; SolverTypes only adds fields. Per-play seeding unchanged; "seen" now uses PlayKey, documented as unchanged behaviour.
7. LevelPackTests L1-L14, stored solver blocks unchanged: met. 44/44 pass, `git diff origin/main -- Assets/Levels` is empty.

## Diff review

- Core: Board.Clone uses a private ctor that skips settle checks (same result); new Board.CopyStateFrom and internal Shelf.CopyStateFrom. Pure game state, no solver or interaction state. Tests: BoardCloneTests (CopyStateFrom matches/independent, rejects unrelated board).
- Runtime use: no non-Editor script calls Board.Clone or CopyStateFrom (grep of Assets/Scripts outside Editor). No gameplay allocation change. Existing PlayMode suite (47) passes anyway.
- Threading: each worker has its own context sibling, oracle, scratch board, results; shared state is nextPlay (Interlocked) and SearchBudget (Interlocked/lock). Merge is after Join; unknown never overwrites known. Results are thread-count independent per design. Caveat: board counts and the point where the node budget trips vary with thread count, so Inconclusive near the limit could differ between runs. Not a determinism problem for normal levels.
- Packages: none changed. Docs: level-schema.md updated.
- Build size: editor-only code plus small Core change, effectively no change. Frame time: no runtime path changed.

## Console

No errors or warnings after the runs (read_console error/warning: 0 entries).

Note: Unity modified ProjectSettings/ProjectSettings.asset during the PlayMode run; reverted with git checkout before commit.

## Findings

1. (blocking) Analyse_FourteenShelfLevelWithLocks_UnderTenSeconds fails in the full EditMode suite (16.7 s) though it takes 3.4 s alone. The 4-thread play phase and load from the rest of the suite are the likely sensitivity. The author must make criterion 4 reliably pass in the full suite (reduce work or make the test robust without loosening the 10 s threshold), or Muthu must decide.
2. (minor) Node counts differ slightly per run (1.798M to 1.802M) because of threads; fine.

RESULT: FAIL
Top issues for the author: fix the full-suite timing of the 14-shelf locked analysis test (16.7 s vs 10 s limit).
