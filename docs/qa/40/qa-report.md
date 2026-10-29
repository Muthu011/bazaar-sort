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

---

# Round 2 (final), tip 67d32ff

New since round 1: 12-shelf cap (docs, loader `TooManyShelves`, tests), `MinColumns` removed and the column count is the biggest-tile one of 1, 2, 3, style guide and art-director.md updated (256 px import), DevOverlay PlayMode tests pinned to a fixed 1080x2400 texture, screenshots regenerated.

## a. Tests
| Suite | Result | Time |
| --- | --- | --- |
| EditMode `!Performance` (MCP) | 641 / 641 passed (was 609) | 70.2 s |
| PlayMode full (MCP) | 68 / 68 passed | 28.2 s |
| Performance batch (`run-performance-tests.sh`) | 1 / 1 passed, `Analyse_FourteenShelfLevelWithLocks_UnderTenSeconds` 2.03 s | about 36 s |
| Console | 0 errors, 0 warnings (one "Curl error 16 ... unitytls" network line seen earlier, editor noise) | |

DevOverlay independence from Game view size: by reading, the tests now render the camera to a 1080x2400 RenderTexture and call `Bind` and `Layout` with a fixed `PhoneSafeArea` and density 3, so they no longer follow `Screen`. The PlayMode run passed with the Game view at preset index 15 (whatever it was; I did not change it). I did not run it under a second Game view size.

## b. Tests and code diff
- `git diff origin/main -- Assets/Tests/EditMode/BoardLayoutTests.cs` is empty: byte-identical to origin/main (the round-1 edits are reverted).
- `BoardLayout.cs` vs origin/main: only a doc comment change on `Compute` (the biggest-items column rule). `MinColumns` and the Compute loop change are gone; logic identical to main.
- PortraitLayoutTests, removed: `Shelves_TwoOrThreePerRow_AndFillTheWidth` (asserted Columns in 2..3, and for <= 8 shelves that the width is filled), `MinColumns_BelowOne_CountsAsOne_...` (tested the removed setting). Added: `ColumnCount_GivesTheBiggestItems_AmongOneTwoAndThree` (independently recomputes the tile for 1, 2, 3 columns; asserts Columns equals the best, TileSize equals the best, and is not smaller than any of the three), `Shelves_GrowUntilTheyFillTheBandWidthOrHeight` (the old fills-width-or-height check, using the true row width), two new Phones (16:9 with 24 dp cutout, and with cutout plus gesture bar). Range 4..12 via `LevelValidator.MaxShelves`. Judgment: not weakened against the new rule. The "always fills width for <= 8 shelves" check is invalid under one-column boards, and the new column test is stricter on tile size. The thresholds are unchanged: 48 dp up to 8 shelves, `FloorTouchDp` (44 dp) above, now up to 12; the `PlayerSettings_ArePortraitOnly` test is kept.

## c. Layout table (BoardLayout.Compute, called in the editor), tile dp / smallest touch dp
| Setup | 4 | 5 | 6 | 7 | 8 | 9 to 10 | 11 to 12 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1080x2400 full | 1c 86.4 / 96.7 | 1c 68.4 / 76.7 | 1c 56.7 / 63.5 | 1c 48.4 / 54.2 | 2c 45.9 / 51.4 | same | same |
| 1080x2400 110 top, 63 bottom px | 1c 80.1 / 89.8 | 1c 63.5 / 71.1 | 1c 52.6 / 58.9 | 2c 45.9 / 51.4 | same | same | same |
| 1080x1920 full | 1c 69.1 / 77.4 | 1c 54.8 / 61.3 | 2c 45.9 / 51.4 | same | same | same | 2c 45.3 / 50.8 |
| 1080x1920 24 dp top | 1c 66.5 / 74.5 | 1c 52.7 / 59.0 | 2c 45.9 / 51.4 | same | same | same | 2c 43.6 / 48.9 |
| 1080x1920 24 dp top + gesture | 1c 64.2 / 71.9 | 1c 50.9 / 57.0 | 2c 45.9 / 51.4 | same | same | same | 2c 42.2 / 48.0 |
| 720x1600 full | 1c 86.4 / 96.7 | 68.4 / 76.7 | 56.7 / 63.5 | 1c 48.4 / 54.2 | 2c 45.9 / 51.4 | same | same |
| 720x1600 74 top, 42 bottom px | 1c 80.1 / 89.7 | 63.5 / 71.1 | 52.6 / 58.9 | 2c 45.9 / 51.4 | same | same | same |

Confirmed: 4-shelf board is one column again (86.4 dp at 1080x2400); the 16:9 + 24 dp cutout at 12 shelves is 48.9 dp touch (48.0 with the gesture bar), so at least 44 dp everywhere; no setup is below 48.0 dp at any shelf count 4 to 12. This table equals origin/main for 4 to 8 shelves (the round-1 "main" numbers). The style guide table (4 to 12) matches these numbers (spot-checked 8 cells).

## d. Cap
- `LevelValidator.MaxShelves = 12`; 13 shelves gives `TooManyShelves` with message "13 shelves; a level may have at most 12". Tests: `Validate_TwelveShelves_IsAllowed`, `Validate_ThirteenShelves_IsRejectedWithAClearError`, `Load_ThirteenShelves_FailsInTheLoader`, and `LevelPackTests.Level_HasAtMostTwelveShelves` (15 cases). All pass.
- Shipped levels w1_l001 to l015 have 4,4,4,5,6,6,7,7,8,8,6,6,7,7,8 shelves: all at most 8.
- The synthetic 14-shelf solver performance test is untouched (it builds boards directly, bypassing the validator) and passes.

## e. Docs
- `docs/game-design.md` says 4-12 shelves, 12 cap, column rule; `docs/level-schema.md` and `.claude/agents/level-designer.md` state the cap; `.claude/agents/art-director.md` and `docs/style-guide.md` say 256 px max import with 512 px masters kept in the art source folder, consistent with decision 3.
- grep of docs, .claude, CLAUDE.md, Assets/Scripts for "4-14", "13-14", "up to 14", "2 or 3 per row", "3.7x": no leftovers (the only "per row" hits are the new column rule and DevLevelSelectLayout's own button grid). The style guide discloses the 1% case (a 4-shelf tile of 259 px vs a 256 px import).
- Muthu's earlier 2026-10-29 decisions (48 / 44 dp rules, levels 1 to 20 at most 8 shelves, win-rate targets) are not undone. Minor wording nit: game-design.md still says "The 4-12 shelf layout is fixed. Don't shrink or move shelves..." next to the column rule; consistent with the decisions as given.

## f. Frame time (own reading)
The editor was in Edit Mode (checked with `EditorApplication.isPlaying` = false; scene Game.unity). I entered Play Mode, loaded L15 with `LevelBootstrap.LoadLevel(14)` (8 shelves, 2 columns, one lock), read `get_frame_timing` twice, and stopped Play Mode. The Game view was not changed. Readings (editor, unfocused, so heavily throttled: wall frame time 100 to 200 ms, which is the throttle, not the game): main thread 2.28 ms and 1.41 ms, render thread 1.43 ms and 1.93 ms, GPU 0.69 ms and 0.70 ms. This agrees with the author (main 2.2 to 2.4, render 1.7 to 1.9, GPU 1.1; direct timing 0.133 ms). The per-frame CPU and GPU work is about 2 ms, well under 16.6 ms; this is editor, not device, and not a sustained multi-second capture, so a low-end Android device check remains for later. Judgment: evidence is enough for the in-editor criterion.

## g. Screenshots (regenerated, 10 files; I viewed L1 1080x2400, L1 720x1600, L15 1080x1920 and L15 in round 1 form; layouts follow the column table)
- L1 (4 shelves): one column, large tiles, at 1080x2400 and 720x1600; bars inside cutout and gesture bar; level label, Levels button, fps lines, boosters, coins and gear do not overlap; settings panel unchanged (readable).
- L15 (8 shelves): two columns, lock badge visible, bars clear.
- Observations (minor): in the L1 one-column shots the bottom shelf ends about 12 dp above the booster slots and the top shelf about 12 dp under the Levels button, so the board nearly touches the bars. By my estimate the lowest shelf's touch area (half the row gap, about 45 px) reaches into the top of the booster slots by roughly 10 px; boosters are empty placeholders now, so no effect yet, but check once boosters are tappable. 1080x1920 shots are full screen. Fps lines read "0 fps" (captured before sampling).

## Findings
No blocking findings. Round-1 findings 1 and 2 are resolved by Muthu's decisions (12-shelf cap with 44 dp or more met; biggest-items column rule). Open, minor: (1) the one-column bottom shelf's touch area vs the booster slots once boosters are live; (2) frame time is not measured on a device; (3) add3f44 alone still does not compile (history only).

RESULT: PASS
