# QA report: issue #5 Input: tap to pick and drop, plus drag

Branch: task/5-input-tap-drag (HEAD d0dde7f), range 7d15af2..d0dde7f.

## Checklist
1. Tests (independently run after forced refresh and compile): Edit Mode 198/198 passed, Play Mode 14/14 passed, 0 failures.
2. Console after refresh: 0 errors, 0 warnings from the project (one transient MCP-FOR-UNITY "WebSocket is not initialised" warning during the domain reload, tooling not project code; a re-read after the runs was empty).
3. Levels: no level data changed. N/A.
4. Screenshots: docs/qa/5/w1_l001_selected_1080x1920.png and _1080x2400.png viewed. The selected item (idli "I" on shelf 2) is lifted with an orange highlight frame; layout is clean, no overflow, tiles about 207 px wide. They show the selected state only. I did not capture a drag-in-flight or shake image (the shake is covered by tests only).
5. Acceptance criteria:
   - Tap select, tap shelf moves, tap elsewhere cancels: MET. Tests TapItem_HighlightsIt_TapElsewhereCancels, TapInvalidShelf_ShakesAndDeselects, and the tap-tap full win; MoveInputTests cover the state machine.
   - Drag onto shelf moves; invalid drop snaps back with shake: MET. Drag_TileFollowsPointer_ThenInvalidDropSnapsBackWithShake asserts board unchanged, tile glides home, shake occurs. Dropping on own shelf returns home without a shake (a sensible design choice, noted).
   - Touch targets at least 48 px, mouse works: MET. HitTargets_AreAtLeast48Px_ForEveryShelfCount asserts >= 48 for shelf drop and slot pick widths and heights across shelf counts and resolutions (real assertions, not tautologies). Mouse tests use a simulated Mouse; Touch_TapTapAndDrag_MoveItems uses a simulated Touchscreen. Not verified on a real device.
   - Play Mode test drives a full win of w1_l001 through input: MET. FullWin_W1L001_ByTapTap and FullWin_W1L001_ByDrag use InputTestFixture; they move the mouse, press and release the left button frame by frame, and then assert _board.IsWon and the view's won text. The helper functions (Click, MouseDrag) contain no Board calls; the board is only read for assertions.
6. Player-facing notes: letter placeholder tiles still stand in for art (known from #4). The 48 px minimum is in layout pixels at the reference resolutions; actual physical size on a device depends on DPI (not verified).

## Specific checks requested
- No rules in Input/View: View never calls Board.CanMove or TryMove directly; the only hits are in Core/MoveInput.cs. PointerGesture, TileMotion and BoardInput are geometry, timing and Input System reads. OK.
- MoveInput placement (note, not a failure): it lives in Core, is pure C# with no UnityEngine, and delegates all validity to Board.CanMove and Board.TryMove. It holds UI interaction state (selection/drag), not game rules, so arguably it belongs in View/UI per the layout line "View: MonoBehaviours, animation, input". Keeping it in Core gives plain EditMode tests and does not violate "no UnityEngine in Core". It does not break a rule. Suggest Muthu decide; moving it would be a small, mechanical change.
- Tests not weakened: of all test-file changes in the range, the only removed line is one asmdef reference-list line, reshaped to add Unity.InputSystem entries (PlayMode asmdef). BoardLayoutTests.cs: +71 lines, 0 removed. All other test files are new.
- Dependencies: Packages/ and ProjectSettings/ have no diff against main. com.unity.inputsystem is listed in docs/approved-dependencies.md ("Unity Input System", Input). The test framework assembly ships with that package. No new assets.
- Frame time: I could not independently measure it. The profiler query outside an active Play Mode session returned meaningless idle values (main thread 0.001 ms; CPU frame 100 ms is the idle editor throttle). The author's 1.1 to 1.3 ms is unverified by me; code inspection shows Update does a cheap pointer poll and per-frame glide math on one or two tiles. Editor timing is not representative of a low-end Android device.
- Build size: not measured by QA; no Android build was made. Change adds ~600 lines of C#, no new art, and the Input System was already installed, so the effect is expected to be negligible. The author should still state a figure in the PR.
- PNG screenshots are stored through Git LFS, as per the repo rules.

## Open items (none blocking)
1. Record an on-device-like frame time during real Play Mode input, and a build size figure, in the PR body.
2. Add a screenshot of an in-flight drag and of the shake frame if practical.
3. Decide on MoveInput location (Core vs View/UI).

RESULT: PASS
