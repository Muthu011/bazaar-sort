# QA report: issue #44 "Real star rule and coin wallet"

Branch `task/44-stars-coin-wallet` (5 commits on origin/main, not pushed). Verified 2026-10-29 in the Unity editor through Unity MCP.

## 1. Tests
- Edit Mode: 779 / 779 passed, 0 failed, 0 skipped (50.6 s).
- Play Mode: 82 / 82 passed, 0 failed, 0 skipped (39.9 s). Includes the existing frame-time tests (`TutorialGameScenePlayModeTests`, budget `FrameBudgetMs = 16.6`, asserts mean PlayerLoop under budget; `Feel_FrameTimeAndGc_AreMeasured_OnABusyChain`).
- Both match the author's numbers.

## 2. Console
After a forced refresh + compile and again after both test runs and the Play Mode session: 0 project errors, 0 project warnings. One transient `MCP-FOR-UNITY [WebSocket] Unexpected receive error` warning appeared during the domain reload; it is the Unity MCP bridge, not project code, and did not recur.

## 3. Levels
No level changed: `git diff origin/main -- Assets/Levels` is empty and the working tree was clean before QA. Solver check = confirmed level JSON untouched. (`LevelParser` now reads `solver.minMoves`, covered by `LevelParserTests`.)

## 4. Screenshots (Play Mode, Game scene, Level 5)
- `docs/qa/44/win_card_2_of_3_stars.png`: Win card, 2 stars lit marigold, 3rd dim pale clay (#D7CCC8). Staged: `LevelBootstrap._lastWinStars` set to 2 by reflection and `ResultPanels.Show(Win)` called (the level was not played to a win). The real path (moves -> StarsFor -> LastWinStars) is covered by PlayMode tests.
- `docs/qa/44/hud_coin_pill_wallet_135.png`: HUD coin pill shows `135` after `GameServices.Progress.RecordWin(..., 135)`; it updated live. The dev save was reset to 0 afterwards.
- Captures are only 320x640 (editor Game view size). Legible, but not device-resolution.

## 5. Acceptance criteria
1. Star rule: MET. `Core/WinRewards.StarsFor` uses exact integer compares (2*moves vs 3*min / 5*min). Run live with minMoves 5: moves 5->3, 6->3, 7->3, 8->2, 12->2, 13->1 (matches the expected 7->3, 8->2, 12->2, 13->1). Unit tests cover exact 1.5x/2.5x edges, off-by-one, non-integer thresholds, minMoves<=0, overflow, monotonicity. Rule documented in `docs/game-design.md` ("Stars and coins") and `docs/level-schema.md`.
2. Coins: PARTIAL, as stated (known, not a failure). 10 / 15 / 20 per win verified by tests (`Coins_Are10PerWinPlus5PerStarAbove1`, `RecordWin_EarnsCoinsForItsStars`). Saved with progress (file store round trip test). Spend API `ProgressService.TrySpendCoins` is correct: pays and saves once and raises Changed; exact balance leaves 0; insufficient funds changes nothing and does not save; 0 is free with no save; negative throws. Boosters do not exist yet (#56).
3. Best stars kept + Edit Mode tests: MET. Best stars never decrease (`RecordWin_Replays_EarnCoinsEveryTime_ButKeepBestStars`, `Wallet_FileStore_RoundTripsCoinsAndBestStars`); earn, spend and save all have Edit Mode tests.

## 6. Back-compat
`Serializer_ReadsASaveFromBeforeCoins_AsZeroCoins_KeepingEverythingElse` and `Wallet_SaveFromBeforeCoins_LoadsWithoutWarning_AndEarnsFromZero` pass: a version-1 save without `coins` loads as 0 coins, keeps stars/unlocks/tutorials, and logs no warning. A negative `coins` value is a rejected test case.

## 7. Replaced tests (CLAUDE.md: no weakening)
Reviewed the diff; none weakened.
- `Serializer_NeverWritesCoins` -> `Serializer_RoundTripsCoins`: the old assertion encoded the placeholder rule "no coins in the save", which the issue reverses. The new one asserts `"coins":135` is written, read back, and survives Clone, plus a new old-save test and a negative-coins rejection.
- `StarsForAWin_MatchTheWinCard` -> `WinCardStarSlots_MatchMaxStars`: `StarsForWin()` was removed because stars are no longer constant; the new test keeps the same invariant (3 slots == `UiFeel.WinStars` == MaxStars). Star behaviour is now covered far more deeply by `WinRewardsTests`.
- GameFeel clear-tally checks `hud.CountText` -> `hud.Counter.Shown`: same values asserted (0, 1, 2, reset to 0), now against the tally rather than the pill that shows the wallet; new assertions add that a clear leaves the wallet at "0". Added `WinStarsLit == 3` check.
- All `RecordWin` call sites only gained the new stars argument. No thresholds changed.

## 8. Frame time and build size
- Frame time: the Play Mode frame-time tests (16.6 ms budget) passed. The editor FPS overlay read about 6-7 fps in the screenshots because the editor was unfocused/driven over MCP; this is not a device number and is not what the test measures.
- Build size: NOT measured (no Android build run in this QA pass). Change is about 840 lines added across 31 files (mostly tests and docs), 0 new assets or packages, so the expected impact is negligible.

## 9. Player-facing notes (non-blocking)
- Dim star (#D7CCC8) on the cream card is low contrast but readable in the screenshot; the lit/dim difference is clear.
- The Win card lacks a "+N coins" line, so the player only sees the earned coins in the pill after closing (design call for Muthu, not an acceptance item).
- Coin pill is a display, not a tap target, so the 48 px rule does not apply.

RESULT: PASS

Top issues for the author: none blocking. Follow-ups: wire the spend API to boosters in #56; consider showing coins earned on the Win card; measure build size in a release-candidate build.
