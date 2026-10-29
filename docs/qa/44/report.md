# QA report: issue #44 "Real star rule and coin wallet" (round 2, PR #63)

Branch `task/44-stars-coin-wallet` at e0d6f57 (5 local commits 6762a5e..e0d6f57 over the pushed branch, not pushed). Verified 2026-10-29 in the Unity editor through Unity MCP. This replaces the round-1 report (pay-every-replay rule, "9999+"), which is superseded.

## 1. Tests
- Edit Mode: 852 / 852 passed, 0 failed, 0 skipped (39.5 s).
- Play Mode: 87 / 87 passed, 0 failed, 0 skipped (45.8 s). Matches the author's numbers.
- Known, non-blocking: the Play Mode assembly does not compile at intermediate commit db399c2 (ClearCounter API changed before its callers). Only the tip compiles, so the branch is not bisectable across that commit. Not re-checked by checkout (would disturb the editor); taken from the author's report.

## 2. Console
After a forced refresh + compile, and again after both test runs: 0 project errors, 0 project warnings. One `MCP-FOR-UNITY [WebSocket] ... not initialised` warning was seen right after the domain reload; it is the MCP bridge, not project code, and was gone on the second read.

## 3. Levels
`git diff origin/main HEAD -- Assets/Levels` is empty. No level changed, so the solver/uniqueness/band check does not apply.

## 4. Rulings 1-8
1. First win 10 / 15 / 20: MET. `WinRewards.CoinsForWin(stars, previousBest, config)`; first-win cases in `WinRewardsTests`; `Win_InTheMinimum_Earns3Stars_And20Coins_SavedAndOnTheWinCard` (Play Mode, live scene) passes.
2. Replays: MET. `Replay_PaysFivePerStarAboveThePreviousBest` covers same (1/2/3 -> 0), fewer (3->1, 3->2, 2->1 -> 0), more (1->2 +5, 2->3 +5, 1->3 +10); `OneStarFirstWin_ThenThreeStarReplay_TotalsTheSameAsAThreeStarFirstWin` (20 cap); Progress rules tests and Play Mode `WinCard_ReplayWithNoNewStar_HasNoCoinLine_AndThePillStays` pass. Amounts come from `RewardConfig.Default` (10/5), not literals; custom-config test exists. No remote config in the project yet (honest, documented).
3. Undone moves count: MET. `MoveCounter` exposes only Count and Reset; `HasNoDecrement_OnlyCountAndReset` fails if one is added. Undo itself is not built (#53), so this is verified at the API level only.
4. Per-clear coin flight removed: MET. Play Mode asserts `CoinsInFlight == 0` after a clear and the wallet unchanged.
5. Win card "+N" line: MET. `WinCard_ShowsPlusN_AndItsCoinsFlyIntoThePill_WhichCountsUpWithoutAJump` (Play Mode) passes; `WinCoinsTextTests` cover key `win.coins_earned`, "+{0}", and hidden at 0 and negative; `WinCard_LeftMidFlight_ShowsTheWholeBalanceAtOnce` passes. Screenshot `win_card_plus20_coins.png` shows "+20" with a coin under the stars, above Next/Retry, no overlap.
6. Pill auto-widens to 5 digits, min 84 dp, text never shrinks: MET. `CoinBalanceTextTests` PillWidth cases (84 up to 3 digits, 92 at 4, 103.5 at 5, overflow inside max). Play Mode `Pill_WidensForFiveDigits_AndNeverShrinksTheText` asserts `label.fontSize == HudLayout.CounterFontDp` at 5 digits. Screenshot `hud_coin_pill_5_digits.png` shows "99999" fitting with margin. Overflow above 99,999 shows "99K+" (`hud_coin_pill_overflow.png`, fits). This replaces the old "9999+" text, per ruling.
7. #D7CCC8 placeholder: MET as a doc item. `docs/game-design.md` says it stays a placeholder until the Slice 4 art pass. No code change expected.
8. Rules in docs/game-design.md: MET. The "Stars and coins" section now states first win, replay, undone moves, config, no per-clear flight, pill width and "99K+", the "+N" line and the count-up. It matches the code I read.

## 5. Booster slot shrink and overlap
`HudLayoutTests.CoinPill_AtEveryWidth_StaysInTheRow_WithoutTouchingBoostersOrSettings` runs 360x742.33, 360x616 and 412x915 dp for text "0", "20", "999", "1250", "99999" and "99K+": pill stays left of settings, clear of the last booster by the gap, `BottomBarFits`, and every booster width >= 48 dp (touch floor). `CoinPill_GrowsLeftwards_AndTheBoostersNeverMove` confirms the right edge is fixed and boosters do not move as the balance grows. All pass. Slots at about 48.2 dp on a 360 dp phone are just above the 48 floor: a pass with almost no slack, noted for #56 when boosters get real icons. In the 1080x1920 screenshots nothing overlaps.

## 6. Test-diff audit (vs origin/task/44-stars-coin-wallet)
Every removal matches a removed feature or a changed rule; none weakened.
- `ClearCounterTests`: the coin-landing tests (`ClearWithACoin_TicksUpWhenTheCoinLands`, `EachLandingAddsExactlyOne...`, the dropped-flying-coins case) went with the flight (ruling 4). Clear-once, reset-on-load and 0-start are kept (`Cleared` instead of `Shown`), and `ManyClears_AreAllCounted` (100) is added. `Text_IsCachedAndCapped` ("99+") was removed with the tally's `Text` property: the tally is no longer drawn (the pill shows the wallet), so there is nothing to cap. Acceptable.
- `GameFeelPlayModeTests`: the clear-coin test is renamed and now asserts no coin in flight and counted at once; Retry/Next reset, skipped-pop and never-twice assertions are kept. `Feel_AllocatesNothing_...` keeps its zero-alloc assertion and 150-frame window; only "Coin" dropped from the name and comments.
- `ProgressRulesTests` replay test: replaced by the new replay rule (ruling 2) with more cases (45 total coins assertion dropped because replays no longer pay every time).
- `ProgressPlayModeTests`: the old wallet tests (every win, replays included, adds coins; "a win's coins show at once") encoded the superseded rule; replaced by the Win-card/count-up, no-line-on-replay, left-mid-flight and 5-digit pill tests. The `Launch()` helper was removed from this file; Play Mode still passes and the file launches via its shared setup. Net coverage went up (+5 Play Mode tests overall: 82 -> 87).
- `CoinBalanceTextTests`: 9999+ -> 99K+ per ruling 6; more cases added.
- `WinRewardsTests`: `CoinsForWin(stars)` cases replaced by the (stars, previousBest, config) versions; no thresholds changed.
- No test was deleted without a replacement tied to a ruling, and no star or frame-time threshold was touched.

## 7. Frame time and build size
- Frame time: the Play Mode frame-time tests (`FrameBudgetMs = 16.6`, plus `Feel_FrameTimeAndGc_AreMeasured_OnABusyChain` and the allocation test) all passed in the 87 / 87 run. The editor FPS overlay in older captures is an unfocused-editor artifact, not a device figure.
- Build size: NOT measured (no Android build run). Change adds about 1,590 lines across 38 files (mostly C# and tests), 3 PNGs under docs/qa, no new packages or art assets; expected impact negligible.

## 8. Screenshots in docs/qa/44/
- Kept: `win_card_plus20_coins.png` (Win card with "+20" line), `hud_coin_pill_5_digits.png` ("99999"), `hud_coin_pill_overflow.png` ("99K+"). All 1080x1920.
- Removed as stale: `win_card_2_of_3_stars.png` (pre-ruling card with no "+N" line, and the old pill) and `hud_coin_pill_wallet_135.png` (old cramped pill and old booster-slot size; the 5-digit and overflow captures now cover the pill). The 3-star card with its "+20" line replaces the former.
- Gap: there is no screenshot of a 2-star card with "+15", or of a replay with the line hidden. Both are covered by Play Mode tests, not by an image. Captures are staged (the level was not played to a win); the real path is covered by the Play Mode tests.

## 9. Player-facing notes (non-blocking)
- The dim star (#D7CCC8) is low contrast on the cream card but readable; ruling 7 keeps it as a placeholder.
- The coin pill is a display, not a tap target, so the 48 dp rule does not apply to it. The booster slots are placeholders for #56 and sit at 48.2 dp on a 360 dp phone.
- "99K+" is the overflow text; fine at the current economy (levels top out at 20 coins each).
- "Spent by boosters" remains a known partial owned by #56: the spend API exists and is unit tested, no booster consumes it yet.

RESULT: PASS

Top issues for the author (none blocking):
1. db399c2 does not compile its Play Mode assembly; consider squashing or fixing the history before merge so the branch bisects.
2. Booster slots at 48.2 dp on 360 dp phones leave about 0.2 dp over the 48 dp floor; any future widening of the pill or margins breaks it (a test guards this).
3. Add images for a 2-star "+15" card and a replay with no line if you want visual evidence for those cases.
4. Build size not measured; do it in the release-candidate build.
