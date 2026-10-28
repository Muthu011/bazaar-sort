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
- No rules in Input/View: View never calls Board.CanMove or TryMove directly; the only hits were in MoveInput.cs (then in Core, now in View). PointerGesture, TileMotion and BoardInput are geometry, timing and Input System reads. OK.
- MoveInput placement (superseded, see Round 2: it now lives in Assets/Scripts/View, namespace Game.View, per the owner's decision). Round 1 note: it lived in Core, is pure C# with no UnityEngine, and delegates all validity to Board.CanMove and Board.TryMove. It holds UI interaction state (selection/drag), not game rules, so arguably it belongs in View/UI per the layout line "View: MonoBehaviours, animation, input". Keeping it in Core gives plain EditMode tests and does not violate "no UnityEngine in Core". It does not break a rule. Suggest Muthu decide; moving it would be a small, mechanical change.
- Tests not weakened: of all test-file changes in the range, the only removed line is one asmdef reference-list line, reshaped to add Unity.InputSystem entries (PlayMode asmdef). BoardLayoutTests.cs: +71 lines, 0 removed. All other test files are new.
- Dependencies: Packages/ and ProjectSettings/ have no diff against main. com.unity.inputsystem is listed in docs/approved-dependencies.md ("Unity Input System", Input). The test framework assembly ships with that package. No new assets.
- Frame time: I could not independently measure it. The profiler query outside an active Play Mode session returned meaningless idle values (main thread 0.001 ms; CPU frame 100 ms is the idle editor throttle). The author's 1.1 to 1.3 ms is unverified by me; code inspection shows Update does a cheap pointer poll and per-frame glide math on one or two tiles. Editor timing is not representative of a low-end Android device.
- Build size: not measured by QA; no Android build was made. Change adds ~600 lines of C#, no new art, and the Input System was already installed, so the effect is expected to be negligible. The author should still state a figure in the PR.
- PNG screenshots are stored through Git LFS, as per the repo rules.

## Open items (none blocking)
1. Record an on-device-like frame time during real Play Mode input, and a build size figure, in the PR body.
2. Add a screenshot of an in-flight drag and of the shake frame if practical.
3. Decide on MoveInput location (Core vs View/UI). Resolved in Round 2: moved to View.

## Round 2 (review changes)

Range c1ff366..2739e47 (dae951d docs, a52323f refactor, c245378 feat, 2739e47 docs). Owner decisions: touch areas grow into gaps (48 dp target, 44 dp floor on 720p, 14-shelf layout fixed, levels 1-20 use at most 8 shelves); selection moves out of Core into View.

1. Tests (after forced refresh and compile): Edit Mode 223/223 passed, Play Mode 14/14 passed, 0 failures. Matches the author's claim.
2. Console: 0 project errors or warnings (only the same transient MCP-FOR-UNITY WebSocket tooling warning during reload; re-read after the runs was clean).
3. Levels: no level data changed. N/A.
4. Screenshots: no visual change to tile or shelf placement, so the round 1 screenshots in docs/qa/5/ still apply. Touch areas are invisible; none captured.
5. Checks requested:
   - Core has no selection or drag state: grep -i "select|drag" over Assets/Scripts/Core returns nothing. MoveInput.cs is now Assets/Scripts/View/MoveInput.cs (git R096, .meta R100 so the GUID is kept), namespace Game.View, using Game.Core. CLAUDE.md has the line "Core holds game rules only: no selection, drag, or other interaction state (that lives in View)". MET.
   - Shelf and tile placement unchanged: the c1ff366..HEAD diff of BoardLayout.cs touches only the constructor, touch-area code and a new density overload. The Compute geometry body (shelf rects, tile size, columns, rows, playfield) is untouched; only the final constructor call gains the density argument. BoardView changes one line (passes ScreenDensity.Current). MET by diff. Weakness: the pinning test Density_ChangesOnlyTouchAreas_NotShelvesOrTiles compares the new density overload against the old no-density overload of the same code, so it proves density does not move things, not that placement equals c1ff366 (no golden values). The existing layout tests (inside safe area, no overlap, etc.) are unchanged and pass. Minor.
   - Touch areas: ItemTouchAreas_ContainTheirTile_NeverOverlap_AndHitTestFindsThem asserts for 4-14 shelves on all six screen configs that each touch area contains its tile, lies inside its shelf drop area, hit-tests to its own slot at tile centre, hidden-tile centre and both corners, and never overlaps any other touch area (pairwise, including other shelves). NarrowMiddleItem test checks growth stops at the neighbours' pictures. WideItems test keeps halfway splits. Hit-testing end to end: FullWin_W1L001_ByTapTap and ByDrag Play Mode tests pass. MET. Note: "never overlaps a neighbour's picture" is asserted only in the narrow-middle case (shelf 0, slot 1, 14 shelves at 720p), not for every item; overlap of touch areas is asserted for all.
   - dp minimums: ItemTouchAreas_MeetDpMinimum passes for 720x1280 and 720x1600 at 2.0 (4-14 shelves, >= 44 dp), 1080x1920 and 1080x2400 at 2.625 (4-14, >= 48 dp), and both at 3.0 (4-8, >= 48 dp). The test thresholds come from the BoardLayout constants (48 and 44), and the lower 44 is applied only to the 720p cases. The author's worst case of 47.99 dp at 3.0 with 13-14 shelves is outside the tested range (4-8), consistent with decision that levels 1-20 use at most 8 shelves; nothing in code enforces the 8-shelf limit. Not a failure.
   - No test or threshold removed or loosened: git diff c1ff366..HEAD over Assets/Tests shows only additions (BoardLayoutTests +133, ScreenDensityTests new, MoveInputTests +1 line: using Game.View). HitTargets_AreAtLeast48Px_ForEveryShelfCount still asserts >= 48 px (untouched). MoveInputTests were moved with the class, not deleted. No test files changed vs origin/main other than what round 1 already reviewed. MET.
   - Docs: game-design.md has a new "Board size and touch areas (decided by Muthu, Oct 2026)" section with all four rules (fixed 4-14 layout; touch areas extend into gaps to 48 dp where there is room; 44 dp floor on 720p; levels 1 to 20 at most 8 shelves). style-guide.md has the same four rules in one bullet. MET. Side note: the same commit also changed game-design.md "Board: 6-14 shelves" to "4-14 shelves", a design-doc edit not listed in the owner's decisions; it matches the code and tests (4-14) but the owner should know.
   - No new packages or assets: git diff origin/main..HEAD over Packages, ProjectSettings, Assets/Art and docs/approved-dependencies.md is empty. New files are ScreenDensity.cs and ScreenDensityTests.cs (with .meta). MET.
6. Player-facing: ScreenDensity falls back to short side / 360 dp when the device reports no dpi (tested). Not verified on a real device. Touch areas on 720p 13-14 shelf boards are 44 dp or a little over, below the 48 dp target by owner's accepted decision.

Open items (not blocking)
1. Add a golden-value placement test (or compare against recorded c1ff366 numbers) so "placement unchanged" is pinned independent of the new code.
2. Consider asserting the touch area never covers a neighbour's picture for every item, not only the narrow-middle case.
3. Frame time and build size still need figures in the PR body (unchanged from round 1).

RESULT: PASS
