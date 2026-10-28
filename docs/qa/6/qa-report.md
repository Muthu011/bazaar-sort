# QA report for issue #6: clear, reveal and win feedback (placeholder)

Branch `task/6-clear-reveal-win-feedback`, HEAD 133bd82 (8 commits over origin/main).

## 1. Tests and console
- Unity force-refreshed with compile. Console after refresh and after both test runs: 0 errors, 0 warnings. (One transient MCP-for-Unity WebSocket warning appeared at the start of the refresh; it is tool noise, not project code, and did not recur.)
- Edit Mode: 257/257 passed. Play Mode: 28/28 passed. Reproduces the author's claims.
- Side effect: the test run left untracked `Assets/InitTestScene<guid>.unity(.meta)` in the working tree. Not committed. Unity normally cleans these up; remove them if they persist.

## 2. Test-file diffs against origin/main
- `BoardViewPlayModeTests.cs` (+17) and `BoardInputPlayModeTests.cs` (+13): additions only. A `WaitForAnimations` helper (bounded to 2 s, then asserts `IsAnimating` is false) plus `yield return WaitForAnimations()` calls before the existing assertions. No asserts removed or loosened, no tests deleted.
- Both test asmdefs: only added references (`Game.UI`, `UnityEngine.UI`).
- No deleted files in the whole diff; every other test file is new. No existing test weakened.

## 3. Zero-allocation test is real
- `Animations_AllocateNothing_WhileTheyPlay`: warms up a full chain, then builds a ChainBoard (reveal, clear, clear, Won), checks `IsAnimating` is true, then runs 40 frames of `AdvanceAnimations(Frame)` inside `Is.Not.AllocatingGCMemory()`. It then asserts the chain finished and the outcome is Won, so the measured region includes clear, reveal, step changes and the `OutcomeReady` raise.
- `Update` calls `AdvanceAnimations(Time.deltaTime)`, so the measured method is the per-frame path. Queues are fixed arrays; no `new` or string building in that path.
- Limit: it does not cover `UpdateTileMotion`/drag code or the real editor Profiler. The issue asks for a Profiler check noted in the PR; that note must come from the author. I did not run the Profiler.

## 4. No game rules in view/UI
- Win and Stuck come from `Board.Won`/`Board.Stuck` events (`BoardView.OnWon/OnStuck`), deferred until animations finish, then `OutcomeReady`. Panels only show on that event.
- `Retry` reloads the current level; `Next` loads the next level via `LevelSequence`. (Round 1 said it looped L5 to L1; that was the placeholder rule and was replaced in round 2: Next now stops after the last level.) Nothing else.
- `git diff origin/main` on `Assets/Scripts/Core`, `Assets/Art`, `Packages`, `ProjectSettings`: empty. Nothing UI-related in Core.

## 5. Dependencies
- Packages/ diff empty; no fonts or art added (uses the legacy built-in font).
- uGUI (`com.unity.ugui`) is already in the manifest and not newly added. It is not listed in `docs/approved-dependencies.md`. NOTE, not a breach: the rule forbids adding a package; recommend adding uGUI to the approved list.

## 6. Screenshots (docs/qa/6/)
- win_panel_1080x2400, stuck_panel_1080x1920 and mid_clear_1080x1920 viewed: panels centred, inside the safe area, text not clipped, buttons large (56 dp by design; the layout tests check 48 dp). Mid-clear shows the T triple mid-pop on shelf 1.
- Observations (not failures): win panel title "Cleared!" is placeholder wording; Win screenshots show the empty board behind it. Tiles are single letters (placeholder art). In the Stuck screenshot the dim backdrop is subtle.

## 7. Performance and size (not measured)
- I did not measure editor frame time or a build. Expected size impact is negligible: about 700 lines of C#, no new assets, and 4 PNGs (LFS) under docs/ only. The author should note frame time in the PR.

## 8. Game.unity provenance
- Committed scene is the builder-generated one: Main Camera has only Transform and Camera (no stray components), `_startIndex: 0` (first level, not w1_l005), no `w1_l005` reference, objects are Result Panels, EventSystem, Board View, Main Camera, Level Bootstrap. Working tree was clean. The author's account (pre-existing uncommitted edits, overwritten by rebuilding) is plausible as editor or manual changes to the open scene; they were not committed and nothing from them remains.

## Acceptance criteria
- Triple clear: pop + fade under 0.5 s: MET (0.35 s, covered by ClearRevealMotion and play tests; tests pass).
- Layer reveal: items slide forward: MET (0.22 s slide, tested).
- Win panel with Next and Retry; Stuck panel with Retry: MET (tests plus screenshots).
- No allocations per frame during animations: MET by automated test; PR Profiler note still owed by the author.

## Round 2 (review changes)
HEAD 14e6f3f (commits 31698ee..14e6f3f over round-1 324bfe2). Owner decisions: uGUI approved; no loop after the last level, show "More levels coming soon" with Replay to L1.

- Refresh (force, compile) then console: 0 errors, 0 warnings (only a transient MCP WebSocket tool-noise warning at refresh). Console still 0 after both runs.
- Edit Mode 265/265 passed; Play Mode 29/29 passed. Reproduces the author's claims.
- Test diff against 324bfe2: only two tests encoded the loop rule. Edit Mode `Next_AfterTheLastLevel_LoopsToTheFirst` became `TryGetNext_AfterTheLastLevel_HasNoNext_AndNeverLoops` (asserts false, index -1, HasNext false, and the one-level case): equal or stronger. Other LevelSequence tests were renamed to the new API with the same assertions plus `First_IsIndexZero` and HasNext checks. Play Mode `Next_AfterTheLastLevel_LoopsToTheFirst` was replaced by `WinLastLevelByInput_...` and `Next_OnTheLastLevel_DoesNotLoop`; the old one used `_panels.Show(Won)` as a shortcut, the new one is stronger. New `ResultPanelChoiceTests` (4 tests) and a two-line-title layout test were added. No other test removed or loosened; no thresholds changed.
- Safe-area and 48 dp Play Mode test now loops over Win, Stuck and MoreLevelsSoon (card inside the safe area, and the Replay button in the button set), so the new panel is covered; the layout test also checks a 2-line title at 48 dp.
- New Play Mode test: wins w1_l005 with 18 `TapTap` pointer-input moves (no Board shortcut; each move is counted via `Board.Moved` and asserted as it goes, then `IsWon`). It then asserts: More-levels panel visible, Win and Stuck hidden, title text "More levels coming soon", exactly one button (Replay), no active button labelled Next, board input locked under the panel, Replay click loads index 0 / w1_l001, panel hidden, input unlocked, start state equals the loader's board, 9 tiles drawn, and a tile tap selects a shelf. `Next_OnTheLastLevel_DoesNotLoop` checks `Next()` returns false and the level stays w1_l005.
- Non-last win still shows Win with Next (`WinByInput_ShowsWinPanel_ThenNextLoadsW1L002`, loads w1_l002); Stuck still shows Retry (`StuckByInput_...RetryRestoresStart`); `ResultPanelChoice` tests cover Stuck on any level. Both pass.
- Docs: `docs/approved-dependencies.md` has the uGUI row; `docs/game-design.md` Level rules state the last-level rule.
- Screenshot `docs/qa/6/more_levels_soon_1080x1920.png` viewed: centred card, two-line title not clipped, one large Replay button, inside the safe area. Observation: the backdrop is dimmed to the point that the board is only faintly visible behind it; acceptable.
- `git diff 324bfe2 -- Packages Assets/Scripts/Core`: empty. No `Assets/InitTestScene*` files present or tracked; working tree clean before this edit.
- Not measured this round: frame time and build size (unchanged expectation: one small panel, no assets).

All acceptance criteria remain MET, and both owner decisions are implemented and tested.

RESULT: PASS
