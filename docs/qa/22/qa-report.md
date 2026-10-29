# QA report: issue #22 Progress save and level select (branch task/22-progress-save)

## Test runs (Unity MCP)
| Run | Result | Time |
|---|---|---|
| EditMode `!Performance`, after forced recompile/domain reload | 565/565 passed | 60.5 s |
| EditMode `!Performance`, second run | 565/565 passed | 51.7 s |
| PlayMode (all) | 60/60 passed | 27.3 s |
| `LevelPackTests` alone after a forced reload | 44/44 passed | 10.7 s |

The author's w1_l010 solver-budget flake (Inconclusive, 30 s) did NOT reproduce in any of my runs (3 runs that include LevelPackTests). No failures to analyse.
Independence from the branch: `git diff origin/main --stat -- Assets/Scripts/Core Assets/Scripts/Editor/Solver Assets/Levels` shows only the new Assets/Levels/LevelList.asset(+.meta); no Core, solver or level JSON change. If the flake recurs it is pre-existing load/throttling sensitivity (see #31 QA), not caused by this branch.

## Console
After refresh and all runs: 0 errors, 0 warnings (one transient MCP WebSocket message during the first reload, tooling only).

## Diff review
- Core untouched. Save code is in `Assets/Scripts/Services/Progress/` behind `IProgressStore` / `IProgressService`, fake `Services/Fakes/FakeProgressStore.cs`. No new singleton (GameServices only). No Packages or ProjectSettings changes. JsonUtility is built in.
- Atomic write: temp file written and flushed, then File.Move (no existing file) or File.Replace, fallback delete+move on PlatformNotSupported; Load recovers from the .tmp when main is missing; corrupt/unknown version is moved to .unreadable and fresh progress is returned. Logic correct. Minor: other exception types (e.g. a SecurityException) are not caught; low risk.
- Tests (ProgressSaveTests, ProgressRulesTests, ProgressPlayModeTests) cover load, save round trip, missing, corrupt (moved aside, one warning), newer version, failing saves, continue and more-levels.
- DevBuildTests, old to new (no thresholds lowered):
  - Buttons_AreAtLeastTouchFloor_AndInsideTheTopReserve -> Toggle_... same 48 dp floor on x and y, same reserve fit, same safe-area start, plus a new left-edge check; same 4 screen sizes.
  - Row_FitsTheSafeWidth_WithoutOverlap (row width, gaps, centred) -> removed because the row no longer exists; replaced by Grid_CellsAreAtLeastTouchFloor_AndTheCardFitsTheSafeArea (6 cases incl. 20 and 1 levels: cell >= 48, action buttons >= 48, cell for every level, card fits safe area, side margins) and Grid_CellsAndButtonsDoNotOverlap_AndStayInsideTheCard (pairwise no-overlap incl. title, inside card; 360x640 and 320x568 only).
  - ShortReserve test kept (renamed fields). Added: 5 columns/3 rows, CellState tests.
- DevOverlayPlayModeTests, old to new: the per-button "above playfield" and "no playfield overlap" assertions on level buttons were removed, since the level cells now live in a card that deliberately covers the board while open (board input is locked, asserted). The same two assertions now apply to the always-visible Levels toggle. 48 dp (width and height) is now asserted for every level cell, toggle, Reset and Close, plus safe-area bounds on all four edges (yMin added), at 1920 and 2400 heights. Old L3-click test kept in the open-then-click form. Net: not a weakening; one gap is that the open card is not asserted clear of the playfield (by design).

## Release gating
`DevBuild.IsLevelSelectEnabled(isDevBuild)` guards `DevOverlay.TryInstall` (returns null in release), and the overlay destroys itself if found in a release player. Test `ReleaseBuild_NeverInstallsTheOverlay` passes (PlayMode) plus the EditMode guard tests.

## Frame time and build size
Not met conclusively. No existing test asserts 16.6 ms (GameFeel frame test only measures and logs). Editor screenshots show 6 fps / max 201 ms but that is a throttled, unfocused editor, so not representative. The change adds small code (about 2.5k lines incl. tests) and a 1.4 KB asset; no packages. Frame time on a device remains unmeasured (finding).

## Screenshots (viewed)
All five in docs/qa/22/ exist and show what they claim: continue at level 4 after 3 wins (Levels button visible), Levels grid with 1-3 starred, 4 current (orange), 5-14 locked, Reset progress and Close; "More levels coming soon" with Replay; closed 1080x2400 view. Player-facing notes: dev-only fps text is red/low in editor; the 14-cell grid has an empty 15th slot until PR #36 lands; tiny star glyphs in cells are small but dev-only.

## Acceptance criteria
1. Saves unlocked level and stars, survives restart: met (file store round trip tests; stars always 3 per Muthu). Not tested on a real device restart.
2. Continues at next unplayed level: met (ProgressPlayModeTests, screenshot).
3. Dev builds have level select for all levels, release hides it: met (grid has a cell per level file; release gating test).
4. "More levels coming soon" with Replay after last level: met (screenshot, PlayMode test).
5. Save code in Services not Core, EditMode tests for load, save, corrupt: met.
DoD item 5 (frame time under 16.6 ms): not verified.

## Top issues for the author
1. Frame time not measured or asserted; add a device or profiler measurement.
2. Open dev card not asserted clear of playfield (design choice; state it in PR).
3. w1_l010 solver-budget flake did not reproduce here but LevelPackTests static cache replays a timeout until reload; producer to escalate separately.

RESULT: PASS
