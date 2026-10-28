# QA report: PR #33, issue #23, World 1 levels 6 to 14

Branch `task/23-levels-w1-l006-l015`. L15 is out of scope (split to #32).

## Test runs (Unity MCP run_tests + get_test_job, all under the 300 s limit)

| Job | Scope | Total | Passed | Failed | Skipped | Duration |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | EditMode LevelPackTests + World1Pack2Tests | 62 | 62 | 0 | 0 | 60.7 s |
| 2 | EditMode full suite | 478 | 478 | 0 | 0 | 14.8 s |
| 3 | PlayMode full suite | 47 | 47 | 0 | 0 | 26.1 s |

Job 1 covers: every level L1 to L14 solvable, inside its band, stored solver block not stale, no duplicate hashes.

## Console
After all runs: 0 errors, 0 warnings.

## Per-level results (from each JSON solver block; matches pack-2.md)

| Level | Role | Shelves | Locks | Min moves | Win rate | Dead ends | Band | Result |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| w1_l006 | breather | 6 | 0 | 11 | 0.992 | 0.000 | >= 0.90 | pass |
| w1_l007 | medium | 7 | 0 | 21 | 0.932 | 0.001 | >= 0.75 (near-loss) | pass |
| w1_l008 | hard | 7 | 0 | 22 | 0.908 | 0.001 | >= 0.90 | pass |
| w1_l009 | hard | 8 | 0 | 29 | 0.901 | 0.001 | >= 0.90 | pass |
| w1_l010 | boss | 8 | 0 | 29 | 0.926 | 0.001 | >= 0.75 (boss) | pass |
| w1_l011 | easy | 6 | 1 | 11 | 0.940 | 0.002 | 0.85 to 1.00 | pass |
| w1_l012 | easy | 6 | 1 | 15 | 0.943 | 0.001 | 0.85 to 1.00 | pass |
| w1_l013 | medium | 7 | 2 | 23 | 0.788 | 0.002 | 0.70 to 0.85 | pass |
| w1_l014 | medium | 7 | 2 | 25 | 0.796 | 0.002 | 0.70 to 0.85 | pass |

## File checks
- `git diff origin/main -- Assets/Levels/World1/w1_l00[1-5].json`: empty. L1 to L5 unchanged.
- Object ids in L6 to L14: all appear in docs/game-design.md (substring check).
- Shelf count max 8 (L9, L10). No locked shelf in L6 to L10; locks in L11 to L14 (1, 1, 2, 2).
- pack-2.md min moves, win rate and dead-end rate match every JSON solver block.
- Win-rate targets (L1-6 and L8-9 >= 0.90, L7 and L10 >= 0.75, L11+ normal 0.70-0.85, hard/boss 0.35-0.50) stated in docs/game-design.md (lines 59 to 61), .claude/agents/level-designer.md (line 11) and docs/level-schema.md (lines 67 to 68, phrased as levels 1 to 10 at 0.90 except 7 and 10 at 0.75).

## Notes
- Several "hard"/"boss" roles in L8 to L10 sit at 0.90+ by the onboarding rule; this is the decided design, not a defect.
- Frame time / build size: not applicable, no runtime change (level data, tests, docs only).
- Screenshots: not captured; no UI or screen changed.

RESULT: PASS
