# QA report: issue #32, level w1_l015 (hard, locked shelf)

Branch `task/32-level-w1-l015` (3 commits on origin/main 473ecfe, not pushed). Level data only; no screen changed, so screenshots, frame time and build size are not applicable.

## Acceptance criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| w1_l015.json: id/world/index/role hard, World 1 ids only, <= 8 shelves, >= 1 locked shelf, type counts multiples of 3 | Met | id w1_l015, world 1, index 15, role hard. 8 shelves; s8 locked (lockClears 3). 10 types, exact match to the World 1 list in docs/game-design.md: six types x 6, four types x 3, 48 items, all multiples of 3. No religious items. |
| Solver block correct, estWinRate 0.35-0.50, analysis Complete within 30 s, no duplicate hash | Met | `Level_StoredSolverBlockMatchesFreshSolve`, `Level_EstWinRateIsInItsBand`, `Level_IsSolvable`, `Pack_HasNoDuplicateLayouts` all pass for w1_l015. estWinRate 0.446, minMoves 31, deadEndRate 0.004, hash e0d9cd74b0367006. Cold analysis 15.4 s (see timings). |
| World1Pack2Tests extended to L15 incl. pack rule (lock in >= 4 levels after L11) | Met | Diff: Roles table, shelf-cap cases, sawtooth loop and lock-used loop extended to L15; new `PackRule_LockedShelfIsUsedInAtLeastFourLevelsAfterL11` passes. No assertion removed or loosened. |
| pack-2.md covers L6-L15 with an L15 row matching the JSON | Met | Heading and intro updated; row: 8 shelves, 1 lock, 2 hidden layers, 48 items, 10 types, 31 min moves, 0.446, 0.004. All match JSON. |

## Test results (Unity MCP)

- LevelPackTests + World1Pack2Tests: 67/67 passed.
- Full EditMode `!Performance`: 512/512 passed, 41.1 s.
- Full PlayMode: 47/47 passed, 25.9 s.
- Performance category (batch mode, scripts/run-performance-tests.sh): passed, 46 s wall; 14-shelf locked analysis 2.39 s.
- Console after refresh and runs: 0 errors, 0 warnings.

## Diff check

`git diff origin/main --stat`: pack-2.md, w1_l015.json, w1_l015.json.meta, World1Pack2Tests.cs only. L1-L14 unchanged. Test diff only extends coverage (adds L15, adds one test); nothing deleted or weakened. Unity touched ProjectSettings/ProjectSettings.asset during the batch run; reverted with git checkout.

## Timings and risk (item 5)

The first run showed about 0 s per test because LevelPackTests keeps a static analysis cache that was warm. To get cold numbers I forced a recompile (domain reload clears the cache) and ran the two band tests alone:

- w1_l015 `Level_EstWinRateIsInItsBand`: 15.4 s (cold, includes the analysis)
- w1_l010 `Level_EstWinRateIsInItsBand`: 11.0 s (cold)

This editor had just finished a 46 s batch run and a recompile, so it was not idle. The author's earlier L10 figure of 6.8 s was lower, and the author measured L15 at 13.7-22.3 s under contention. The 30 s budget is about 2x the typical cold time for L15 and 1.35x its worst contended time (22.3 s). Risk of Inconclusive is low in a normal run and moderate if the editor is heavily throttled (for example several Unity or batch instances at once). The analysis is paid once per domain reload and cached, so only the first L15 test pays it. Not a blocker; worth watching.

## Findings

- None blocking. Observation only: L15 is the slowest level to analyse, and the 30 s budget leaves limited headroom if CI runs on slow or shared machines. If it flakes, simplify the level or raise the budget through a new issue, not by weakening the test.

RESULT: PASS
