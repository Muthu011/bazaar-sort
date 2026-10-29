# QA report: #41 Fail state (branch task/41-fail-state)

## Checks
| Check | Result | Evidence |
|---|---|---|
| Edit Mode tests | PASS | 678/678 run by QA through Unity MCP |
| Play Mode tests | PASS | 70/70 run by QA through Unity MCP |
| Unity console after refresh and runs | PASS | 0 errors, 0 warnings |
| Core has no View or interaction state, no UnityEngine | PASS | Only match for UnityEngine in Assets/Scripts/Core is a comment in GameRules.cs. Core diff adds Board.Failed, IsFailed, Rescue and IBoosterRescue/NoBoosterRescue only. |
| No tests weakened | PASS | Diff of existing tests vs origin/main: only renames (Stuck to Fail, IsStuckVisible to IsFailVisible, etc.). Same assertions, none removed or loosened. Core Stuck tests untouched. |
| No new packages | PASS | No change under Packages/ or ProjectSettings/ |
| Levels | N/A | No levels changed |
| Frame time (16.6 ms) | NOT MEASURED | Needs a player or profiled Play session on a device. Change adds one interface call per stuck move and one hidden button; no per-frame work added. |
| Build size | NOT MEASURED | No build run. Expected negligible (about 1 small C# file, no assets besides 2 PNGs under docs/). |
| Screenshots | PASS | docs/qa/41/fail_panel_1080x1920.png and fail_panel_1080x2400.png reviewed |

## Acceptance criteria
- Core raises Failed when no legal move and no booster can help: MET. Board.Failed raised straight after Stuck when Rescue.CanRescue is false; 13 FailStateTests cover it (rescue yes/no, win, clear on last slot, locked shelves, clone, copy state).
- Fail screen "Out of space!" with Retry, Continue hidden: MET. Screenshots show title and Retry; Continue is built hidden and wired to nothing (IsContinueVisible is false).
- No interstitial or penalty after a fail, rule in docs/game-design.md: MET. "Fail (issue #41)" section added; the Play Mode test checks no cost. No ad or save code is present in the diff.
- Edit Mode tests for fail detection; Play Mode test drives a designed fail: MET. FailStateTests (13), FailStatePlayModeTests (2, real pointer input).

## Screenshots and player view
Both resolutions: card is centred and inside the safe area, text does not overflow, Retry is a large target (well over 48 px). The card covers the middle shelf, which is acceptable for a modal. Tiles are still letter placeholders, so objects are not judged here.

## Findings
Blocking: none.

Non-blocking:
1. Frame time and build size not measured (see above). Muthu or a device run should confirm before release.
2. Failed is only raised after a move. A board that is stuck at load never raises Failed (this was already true of Stuck, and ConstructionDoesNotRaiseFailed_EvenIfInitiallyStuck pins it). Levels pass the solver, so it is not reachable today.
3. If #42's rescue returns true at the stuck moment and the player then uses a booster that still leaves the board stuck without a move, Failed will not be raised until the next successful move. #42 must re-check Failed after a booster is used.
4. The Continue button has no test that pins its hidden state beyond IsContinueVisible; Slice 6 should add a test when it is wired.

## Re-verification: fail card review changes (commits 12b094d, 2ff3937; PR #51)
Run by QA after `git pull` (already up to date).

| Check | Result | Evidence |
|---|---|---|
| Edit Mode tests | PASS | 683/683 (was 678; +5 lead-button layout tests) |
| Play Mode tests | PASS | 71/71 (was 70; +1) |
| Console | PASS | 0 errors, 0 warnings |
| No assertion weakened | PASS | Diff of Assets/Tests against c3bc683 has no removed lines, additions only |
| Layouts without a lead button unchanged | PASS | `leadButtonHeightDp` defaults to 0 and `leadButton` to false, so LeadButton equals Button; test WithoutLeadButton_EveryButtonIsTheSameSize covers 1 and 2 buttons. Win and More-levels cards use the unchanged path. |
| Preview hook not reachable from player code | PASS | `ShowContinueForPreview` is `internal`. Callers: GameSceneBuilder (Editor assembly, via InternalsVisibleTo Assembly-CSharp-Editor) and FailStatePlayModeTests only. No runtime caller; the game keeps Continue hidden. |
| No new assets, fonts or packages | PASS | No changes to Packages/, ProjectSettings/ or any font/asset file. The video icon and play triangle are drawn in code (PlaceholderSprites). Only new file is one PNG screenshot. |
| Screenshots (3) | PASS | See below |

Screenshots:
- fail_panel_1080x1920.png and fail_panel_1080x2400.png: Continue hidden; Retry alone, marigold primary, card sized for one button, unchanged from the first review.
- fail_panel_with_continue_1080x1920.png: "Free Extra Slot" is the taller marigold main button on top, with a video icon (rounded rect with play triangle) left of the label; Retry is the smaller teal secondary button below. Icon and label are centred as a group, nothing overflows, the card sits inside the safe area, both buttons are well over 48 px. Retry (white on teal) is readable.

New non-blocking notes:
5. The 1080x2400 screenshot is not regenerated with Continue shown; only 1080x1920 is. Layout tests cover all phone sizes, so this is minor.
6. `InternalsVisibleTo("Assembly-CSharp-Editor")` widens internals to the editor assembly for the whole UI assembly. Acceptable for QA tooling, but note it for later.
7. The label "Free Extra Slot" is placeholder wording to be confirmed in Slice 6 (noted in code and docs/game-design.md). It promises an extra slot, so Slice 6 must actually grant one.

Blocking: none.

RESULT: PASS
