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

## Round 3 (HEAD feeb050)

Scope: commits e9cc01c, 301c112, feeb050 since b7aedc3. All runs through Unity MCP `run_tests`/`get_test_job` plus `scripts/run-performance-tests.sh`; no sleep loops, every job well under 5 minutes. No code, test, threshold or level changed by QA.

### Criteria

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| 1a | Full EditMode, category `!Performance` | MET | 507/507 passed, 61.9 s |
| 1b | `Performance` alone | MET | exactly 1 test (`SolverPerformanceTests.Analyse_FourteenShelfLevelWithLocks_UnderTenSeconds`), passed, 1.72 s in editor (1.65 s analysis, 4099 A* states). So total 508 = 507 + 1 |
| 1c | Full PlayMode | MET | 47/47 passed, 25.9 s |
| 2 | Batch mode, run twice | MET | run 1: Passed, test time 1.895 s (analysis 1.86 s); run 2: Passed, 1.616 s (analysis 1.58 s). Both well under 10 s. Script printed result lines and no failure (the `exit=` echo was empty only because of a zsh PIPESTATUS quirk in my wrapper; the XML says Passed) |
| 3 | LevelPackTests | MET | 44/44 passed, 10.9 s. `git diff origin/main --stat -- Assets/Levels` is empty (stored blocks unchanged) |
| 4 | Diff review | MET, see below | |
| 5 | L10 analysis time | NOTE | `Level_EstWinRateIsInItsBand(w1_l010.json)` took 6.84 s in the editor (the next slowest is l014 at 1.11 s, l009 0.62 s). Budget is 30 s, so about 4.4x headroom unfocused-editor; the author's near-30 s figure under throttling is a real risk only if the editor is throttled |
| 6 | Console | MET | 0 errors, 0 warnings after all runs |

### Diff review (b7aedc3..HEAD)
- Not touched by the diff: SolverOptions defaults (30 s / 40M budget, 1000 plays, seed 20270112, play policy), difficulty bands, the 10 s limit in the test (only a `[Category("Performance")]` attribute and a comment were added). No `[Ignore]`, no loosened assertion.
- SolverTests: diff is 36 insertions, 0 deletions. The new `DeadEndOracle_AgreesWithTheShortestSolutionSearch_AlongRandomPlays` (2 cases) compares `LivenessOracle.IsAlive` against a fresh, independent `ShortestSolver.Solve` at every step of random plays (asserts alive>0 and dead>0 so it is not vacuous). Nothing was removed or weakened.
- Core: `Board.CanReceive(shelf)` is a pure rule (not locked and free front slot); `HasLegalMove` reuses it; `BoardCanReceiveTests` checks agreement with `CanMove` exhaustively over a played game. No interaction state in Core.
- Threading: `PlaySimulatorThreadingTests` (1 vs several threads, locked and shipped level) pass and exercise both the sequential path and the new A*-thread-alongside-plays path. The winning line is published via a volatile field and only saves dead-end searches. Observed note: the "boards" counter varies between runs (1210994, 1210748, 1210414) because A* and the plays race on the shared budget, but all analysis results (minMoves 31, estWinRate 0.382, 382/1000) are identical every run.
- Script `scripts/run-performance-tests.sh` safety: empty arg falls back to `$TMPDIR/bazaar-sort-perf` (safe). Arg equal to the repo path makes rsync source and destination the same directory (effectively a no-op; `.git` excluded). BUT there is no guard against an ancestor or other unrelated directory: `run-performance-tests.sh /` or `.../unity` (the parent of the repo) would run `rsync -a --delete` into it and delete everything else there. Also a relative arg is resolved from the caller's cwd. Not blocking (needs an explicit bad argument) but should be hardened.

### Findings
1. (Low, hardening) Script has no guard on the copy-dir argument: refuse empty/`/`/the repo/any ancestor of the repo, require the dir to be new or to contain a marker file created by the script before `--delete`.
2. (Info) L10 is the slowest level at 6.84 s in the editor; watch it if the machine is loaded (budget 30 s).
3. (Info) Unity rewrote `ProjectSettings/ProjectSettings.asset` during the runs; reverted with `git checkout --` before the commit.
4. (Info) Performance test is not part of the editor suite (`!Performance`); CI/the author must run the batch script to cover the 10 s limit.

RESULT: PASS
