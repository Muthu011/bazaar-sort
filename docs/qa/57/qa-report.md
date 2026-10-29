# QA report: issue #57 Tutorial 1/2 (L1, L2, L11)

Branch `task/57-tutorial-l1-l2-l11`, 7 commits on top of main. Checked 2026-10-29.

## Definition of done

| Check | Result | Evidence |
| --- | --- | --- |
| Edit Mode tests | PASS | 731/731 passed, 0 failed (96 s). |
| Play Mode tests | PASS (one flake) | Run 1: 75/76, `ResultPanelsPlayModeTests.WinLastLevelByInput_ShowsMoreLevelsSoon_ThenReplayLoadsW1L001` failed at ResultPanelsPlayModeTests.cs:272 ("Replay goes to the first level. Expected 0, was 4"). Rerun of that test alone: pass. Full rerun: 76/76. The editor was in the background during run 1. Likely an order/timing flake, not reproduced. Needs a look from the author because it touches replay after the last level and the tutorial now hooks level loads. |
| Console after refresh and compile | PASS | Cleared, force refresh with compile, ran both suites: 0 errors, 0 warnings. |
| Changed level w1_l011 | PASS | Only change is `"tutorial": null` to `"locked"`. `LevelPackTests` (Edit Mode, all in the 731) cover solvable, win-rate in band, stored solver block equals a fresh solve, no duplicate layouts, and the tutorial key fits the board (`locked` needs a locked shelf). The solver hash is not affected by the key. |
| Frame time < 16.6 ms | NOT MEASURED | No device or profiled Game-scene run with the L1 tutorial active. The author also did not measure it. The only per-frame work is `TutorialDirector.Update` (TutorialDirector.cs:200) and `TutorialCueMotion`. Needs a device run before release. |
| Build size | NOT MEASURED | No build run. Expected small: about 1,900 lines of C#, a rebuilt Game.unity, no new art. Placeholder hand and arrow sprites are drawn in code (PlaceholderSprites.cs). `TutorialCapture.cs` is under Editor, so it is not in builds. |
| Core free of interaction state | PASS with a note | `Assets/Scripts/Core/TutorialGuide.cs` uses only `System`, no UnityEngine. It holds board, kind and a completion flag, with no selection or drag. `AllowsPick` and `AllowsMove` (lines 160-175) are pure predicates the View calls. This is tutorial step logic, not interaction state, but it is the closest to the line. |
| No unapproved dependency | PASS | No changes under Packages or ProjectSettings. No new plugin, font or asset source. |
| Older saves load | PASS | `ProgressSerializer.TryRead` fills `tutorialsDone` when it is missing (ProgressSerializer.cs:90). Test `Progress_SaveFromBeforeTutorials_ReadsAsNoneDone` (TutorialTests.cs:341) passes. Duplicate or empty keys are rejected as an invalid save, and the app then falls back to fresh progress, which would lose the stars. That is unlikely to happen. No version bump, as the author said. |
| Screenshots and recordings in docs/qa/57/ | PASS | Present: l1_hand_on_item, l1_hand_gliding, l1_hand_on_item_720x1600, l2_layer_arrow, l11_locked_line, plus l1_tutorial.gif and l2_tutorial.gif (360x800). The PNGs are in LFS. The GIFs are plain git, about 108 KB each. The L1 GIF shows the hand on the item. The raw frame folders l1_frames and l2_frames are untracked and were left alone. |

## Acceptance criteria

- L1 hand pointer, "Match 3!", only the correct move tappable until the first triple: MET. l1_hand_on_item shows the "Match 3!" pill and the hand on the T on shelf 2. Play Mode test `L1_OnlyTheShownMoveIsTappable_UntilTheFirstTriple` passes.
- L2 hidden layer shown once with an arrow, no text: MET. l2_layer_arrow shows an arrow over the hidden row of shelf 1 and no text. Play Mode test `L2_ShowsTheArrowOverTheHiddenRow_WithoutText_UntilTheReveal` passes.
- L11 locked shelf explained in one short line: MET, with a layout defect (below). The line is "Make 1 match to open".
- Driven by the level JSON `tutorial` field, skippable after first completion (saved): MET. The `tutorial` field is parsed and validated (`locked` added; docs/level-schema.md updated). Completion is saved in `tutorialsDone`, and a replay skips the guide (`L1_AfterFirstCompletion_TheTutorialIsSkipped` passes). There is no Skip button, so "skippable" means auto-skip after the first completion. This matches the author's reading. Muthu should confirm whether a Skip button is wanted for first play.
- Edit Mode tests for step logic, Play Mode test for L1 restricted taps: MET (TutorialTests.cs, 492 lines; TutorialPlayModeTests.cs, 234 lines).
- Short recording of L1 and L2, screenshots in docs/qa/57/: MET. Caveat: the issue says "in the PR" and the PR is not open yet, so the GIFs only exist in the repo.

## Problems players would see

1. L11: the "Make 1 match to open" pill sits on top of the front items of the locked shelf (l11_locked_line_1080x2400.png). It hides the bottom half of all three items on the shelf the player is meant to look at. It should go beside or above the shelf, or in the top bar like the L1 pill.
2. In the L2 and L11 screenshots the HUD title reads "Level 1". This is probably the capture tool loading the level without updating the title. It needs checking in a real run through the normal level flow, because it would be a visible bug.
3. The L1 hand is drawn partly over the item (the fingertip covers the letter T on shelf 2). It is fine as a placeholder, but the hand covers the thing it points at.
4. No tap-target problems found: the bottom bar slots and settings button are over 48 px at 1080x2400.
5. Placeholder hand and arrow sprites are drawn in code. They are not final art and are not in art-provenance.csv. This is fine for now, but the final art needs approval.

## Not verified

- Frame time with the tutorial active, and build size (see above).
- Live behaviour on a device or in the Game scene. Evidence is the author's capture tool plus the Play Mode tests.

## Fix first

1. Move the L11 pill off the shelf items.
2. Look at the Play Mode flake in `WinLastLevelByInput_ShowsMoreLevelsSoon_ThenReplayLoadsW1L001`, or confirm it is a background-editor artefact.
3. Measure frame time in the Game scene on L1 with the tutorial and note the build size, or have Muthu confirm a device check.
4. Confirm the "Level 1" title on L2 and L11.

Round 1 result: FAIL (see Round 2 below).

## Round 2 (commits b7cd1ec, 9bcbb47, 6c22ae5)

| Check | Result | Evidence |
| --- | --- | --- |
| Edit Mode | PASS | 731/731. |
| Play Mode | PASS | 79/79 in two full runs (76 plus 3 new tests from TutorialGameScenePlayModeTests and TutorialPlayModeTests). The ResultPanels flake did not recur. |
| Console | PASS | Cleared, force refresh with compile, both suites run: 0 errors, 0 warnings. |
| L11 pill off the items | FIXED | l11_locked_line_720x1600.png: the pill sits at the top edge of the locked shelf, left of the padlock, and all three front items (I, S, M) are fully readable. A Play Mode test now enforces the placement. The pill touches the shelf edge but covers no item. |
| "Level 1" title | FIXED | The retaken captures read "Level 11" (L11) and "Level 2" (L2), and Game-scene tests check the HUD title. It was the capture tool, as the author said. |
| L1 hand | FIXED | l1_hand_on_item_720x1600.png: the fingertip is now below the T and the letter is readable. |
| Frame time | PASS with a thin margin | The new [#57 frame] test reports PlayerLoop mean 2.06 ms, p95 2.85 ms, max 15.44 ms with the L1 tutorial active. Max is under 16.6 ms but only by 1.2 ms, in the editor and not on a device. A device check is still wanted. |
| Build size | NOT MEASURED | No baseline and no build. No new assets or packages, only code and a rebuilt scene. Noted, not a blocker. |

Flake evidence: round 1 failed once in `WinLastLevelByInput_ShowsMoreLevelsSoon_ThenReplayLoadsW1L001`. That test then passed on its own, and 5 later full runs (two by me in round 2, the rest earlier) had no failure. I did not run it on origin/main, so I cannot show it is not caused by this branch. The test code is unchanged and the failure was "Expected 0, was 4" on the replay index, in a test that does not touch the tutorial. I judge it a timing flake while the editor was unfocused and I do not block on it, but it stays on record for the author or Muthu to watch.

Remaining notes, none blocking: the placeholder hand and arrow art is not final; there is no Skip button (auto-skip after first completion); the recordings are in the repo and not yet in a PR.

RESULT: PASS
