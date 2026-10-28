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
- `Retry` reloads the current level; `Next` loads `LevelSequence.Next` (loops L5 to L1). Nothing else.
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

RESULT: PASS
