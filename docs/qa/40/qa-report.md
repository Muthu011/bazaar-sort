# QA report, issue #40 Portrait phone layout and safe areas

Branch `task/40-portrait-layout` (tip a9f1a79, 6 commits on origin/main d695f0a). QA by Claude Opus 5.5, 2026-10-29.

## Test results
| Suite | Result | Time |
| --- | --- | --- |
| EditMode, category `!Performance`, via Unity MCP | 609 / 609 passed, 0 failed, 0 skipped | 58.7 s |
| PlayMode, full, via Unity MCP | 68 / 68 passed | 28.3 s |
| Performance (batch mode, `scripts/run-performance-tests.sh`) | 1 / 1 passed (`Analyse_FourteenShelfLevelWithLocks_UnderTenSeconds`, 2.84 s) | about 47 s |
| Console after runs (errors and warnings) | 0 entries | |

ProjectSettings has no diff against origin/main. Working tree clean before this report.

## Acceptance criteria
1. Portrait only; three Game view presets. MET. ProjectSettings unchanged (already portrait), `PlayerSettings_ArePortraitOnly` passes. `GameViewPresets` (Editor assembly folder, so editor-only) adds 1080x2400, 1080x1920, 720x1600 by reflection; `IndexOf` check before add makes it idempotent; wrapped in try/catch with one warning. Not exercised twice by me, judged by reading.
2. Board in middle band; bars respect safe areas. MET in the simulated insets (PortraitLayoutTests `Board_SitsInTheMiddleBand_...`, HudLayoutTests, screenshots). Caveat: the 16:9 preset is tested with zero insets (full screen).
3. Shelves fill width 2 or 3 per row; 48 dp touch (44 dp floor on 720p). MET for 4 to 8 shelves everywhere, and for 9 to 14 on the 20:9 sizes. NOT MET in one case, see Finding 1. Tile size: see "Design question" below.
4. Settings button opens a small panel with the Haptics toggle. MET. Screenshot `settings_open_1080x2400.png`: title, Haptics On button, Close; readable, centred, dimmed board behind. Persistence still SettingsService / `settings.haptics` (no Services diff).
5. FPS and frame-time text only in dev builds. MET. `DevBuild.IsFpsReadoutEnabled` existing, tested in `DevBuildTests`; new PlayMode tests `FpsReadout_IsBuiltInDevelopmentBuilds_...` and `FpsReadout_IsNotBuilt_WhenTheGuardSaysRelease` (no `Dev Fps` / `Dev Worst Frame` objects).
6. Screenshots of L1, L10, L15 at three sizes. MET: 9 level shots plus settings panel (10 files in `docs/qa/40/`).

## Layout numbers (computed by calling `BoardLayout.Compute` in the editor; "main" = same code with MinColumns=1, which reproduces origin/main since the rest of Compute is unchanged)
Format: columns, tile dp, smallest touch side dp. 1080x2400 @3, full safe area (the 110 px top / 63 px bottom case gives the same for 4 to 12 shelves).

| Shelves | origin/main | branch |
| --- | --- | --- |
| 4 | 1 col, 86.4 dp, touch 96.7 | 2 col (2x2), 45.9 dp, touch 51.4 |
| 5 | 1 col, 68.4 dp, 76.7 | 2 col, 45.9, 51.4 |
| 6 | 1 col, 56.7 dp, 63.5 | 2 col, 45.9, 51.4 |
| 7 | 1 col, 48.4 dp, 54.2 | 2 col, 45.9, 51.4 |
| 8 | 2 col, 45.9, 51.4 | 2 col, 45.9, 51.4 (unchanged) |

With the 110 px / 63 px insets main is 80.1 / 63.5 / 52.6 dp for 4 / 5 / 6 shelves and already 2 col from 7. On 720x1600 full, main 4 to 7 shelves is 86.4 / 68.4 / 56.7 / 48.4 dp, same as 1080x2400.

Branch, all sizes (tile dp / smallest touch dp), columns always 2:

| Size and insets | 4 to 10 shelves | 12 | 13 to 14 |
| --- | --- | --- | --- |
| 1080x2400 full | 45.9 / 51.4 | 45.9 / 51.4 | 45.9 / 51.4 |
| 1080x2400, 110 top, 63 bottom px | 45.9 / 51.4 | 45.9 / 51.4 | 44.9 / 50.3 |
| 1080x1920 full | 45.9 / 51.4 | 45.3 / 50.8 | 38.7 / 48.0 |
| 1080x1920, 24 dp top cutout (72 px) | 45.9 / 51.4 | 43.6 / 48.9 | 37.2 / 42.9 (below 44 floor) |
| 1080x1920, 24 dp top + 21 dp bottom | 45.9 / 51.4 | 42.2 / 48.0 | 36.0 / 40.3 (below 44 floor) |
| 720x1600 full | 45.9 / 51.4 | 45.9 / 51.4 | 45.9 / 51.4 |
| 720x1600, 74 top, 42 bottom px | 45.9 / 51.4 | 45.9 / 51.4 | 44.9 / 50.3 |

Style guide table (docs/style-guide.md) matches these numbers (137.8 px / 46 dp; 134.7 px / 45 dp; 116.1 px / 39 dp, touch 48.0 on 16:9 full). It has no row for the 16:9 with cutout case.

### Design question for Muthu
The 4 to 7 shelf boards (levels 1 to about 6) change from one large column (86 dp tiles at 4 shelves) to 2 columns of about 46 dp tiles: the item picture is 47 percent smaller at 4 shelves. Touch areas stay at least 51 dp. The design rule says "Don't shrink or move shelves or items to make tap targets bigger"; this change is for the portrait fill-the-width goal in the issue, not for touch, but it does shrink the first-time-player boards. Also, on the 1080x2400 screenshots about 40 percent of the middle band is empty above and below a 2x2 board (L1). Needs Muthu's call. The style guide notes the 512 px source art is now about 3.7x its largest display size (also flagged as open there).

## Assertion diff vs origin/main (changed existing tests)
BoardLayoutTests:
- `HitTest_InGapBetweenRows_PicksNearestShelf`: removed `AreEqual(1, Columns)` "one column", replaced by `AreEqual(2, Columns)`. Gap bottom now uses shelf `below = Columns` (shelf 2) instead of shelf 1; the two HitTest asserts expect `0` and `below` (was `0` and `1`). Same intent, not weakened.
- `FourShelves_On16By9_UseOneColumn` renamed `..._UseTwoColumns`: `Columns 1 -> 2`, `Rows 4 -> 2`. Expectation changed with the design change (see design question); not weakened as a check, but it encodes the decision.

HudLayoutTests (rewritten):
- Removed `Toggle_IsAtLeast48dp_AndInsideTheBottomReserve` asserts (toggle x,y >= 48, FitsInReserve, above bottom, right margin). Replaced in `SettingsButton_IsAtLeast48dp_...` by the same four checks on the Settings rect (width, height, `BottomBarFits`, yMin >= 0, xMax right margin) with more cases (742.33, 616, 742, 640, 800, 915).
- Removed `Counter_SitsLeftOfTheToggle_WithoutOverlap`: counter right <= toggle left - gap, same row, and "stays in the right half, clear of the dev fps readout" (counter left >= -w/2 in old coordinates). The first two are kept in the new `BottomBar_...` test (plus boosters). The "right half / clear of fps readout" assertion has no direct replacement; the fps readout moved to the top bar, and the new top bar test checks fps vs level label, which covers the new geometry. Not weakened in substance.
- `TinyReserve_KeepsTheTouchFloor`: `Toggle.y == 48` kept as Settings.height; `ToggleCentre.y == 24` replaced by "lowest piece yMin == 0"; added `BottomBarFits == false`. Equivalent or stronger.
- New: level label in top bar above status line, dev button / fps overlap tests, booster slots >= 48 dp and ordered. No thresholds loosened.
Conclusion: no weakened assertion found.

## Code review (git diff origin/main)
- Core untouched (no files under Assets/Scripts/Core). No change to Packages, manifest, ProjectSettings, asmdefs: no new packages.
- Only singleton is GameServices (the `Instance()` hits are reflection helpers in GameViewPresets, not a singleton).
- BoardLayout/BoardView changes are small; View/UI stay thin. DOTween appears only in UI/View (pre-existing files); the one editor file using it (GameFeelCapture) is not in the diff.
- Settings panel built from code, `SettingsPanelLayout` pure and unit tested.
- Commit add3f44 alone does not compile (author's note); not bisect-safe, tip compiles and passes. Minor.

## Visual review (10 screenshots)
- Bars sit inside the safe area on 1080x2400 and 720x1600 (cutout above the top bar, gesture bar below the bottom bar); board is in the middle band; no overlaps; level label, Levels (dev) button, fps lines, three booster slots, coin counter, gear all separated.
- 1080x1920 shots are full screen with no cutout, so the strictest case (Finding 1) is not shown.
- Minor: "Level N" text is vertically higher than the centre of the Levels button, slightly unbalanced. A small grey sliver at the right edge near y=150 in the 1080x2400 shots (probably a screenshot or canvas edge artifact); not investigated.
- Booster slots are empty placeholders (expected, no booster feature yet).
- Pictures are 46 dp, 39 dp at the smallest; letters and placeholder art only, so readability of real art is not testable.

## Frame time
Not measured by me: I did not enter Play Mode (the editor was in use by tests, and I could not read mcpforunity://editor/state from this session), so the author's ~0.42 ms main-thread figure on L1 and any L15 figure are unverified. The screenshots show "0 fps" because the readout was captured before sampling. No frame time regression evidence either way; the Performance suite (solver only) passes.

## Findings
1. (Blocking against the stated criteria) 16:9 with a 24 dp top cutout at 13 to 14 shelves: smallest touch 42.9 dp (< 44 dp floor); with a 21 dp bottom inset as well, 40.3 dp (12 shelves 48.0). Author flagged and did not test. Levels 1 to 20 use at most 8 shelves, so no current level is affected, but criterion 3 is not met for that configuration and the tests deliberately omit it. Needs a decision: accept (document 16:9 + cutout is unsupported above 12 shelves), or add the case to the test with a documented limit.
2. (Design, Muthu) 4 to 7 shelf boards shrink from 86 / 68 / 57 / 48 dp tiles to 46 dp, and L1 looks small and sparse. Conflicts in spirit with the 2026-10-29 rule "Don't shrink shelves or items". The style guide change does not remove any of Muthu's 2026-10-29 decisions (48 dp / 44 dp rules, max 8 shelves for L1 to 20 stay), but it does record the 512 px source size as an open question.
3. (Minor) Frame time not verified by QA; style guide has no row for the 16:9 + cutout case.
4. (Minor) Level label alignment vs Levels button; edge sliver in screenshots.

RESULT: FAIL

Top issues for the author: (1) the 16:9 + cutout 13 to 14 shelf touch area of 42.9 dp (decide and document or test it); (2) get Muthu's decision on the 2x2 tile shrink for 4 to 7 shelf boards before merge; (3) record a frame-time measurement for L15.
