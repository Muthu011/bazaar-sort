# Bazaar Sort — game design summary for agents

Source of truth: the "Indian Object Sort Game: Go-Live Plan" doc. This file is the short version agents work from; update it when the plan changes.

## Core
- Board: 4–12 shelves per level (12 is the cap for the whole game; Muthu, 2026-10-29). Each shelf has 3 front slots and up to 4 layers behind.
- Move: tap or drag a front object to any shelf with a free front slot.
- Clear: 3 identical objects on a shelf's front row clear; the next layer slides forward.
- Win: all objects cleared. Fail: out of moves, out of time, or no legal move left (offer continue).
- Session length: 60–120 s per level.

## Mechanics (first level)
Launch build (scope locked): Basic triple (1) · Hidden layer (4) · Locked shelf, opens after N clears (11) · Set Trays: tray asks for 3 different related objects, icons shown (21) · Boss every 10th level.
After launch only (do not build before 2027-01-12): Wrapped item · Soft timer · Conveyor shelf · Magnet and Time Freeze boosters · Album · Shop-street meta · Tamil/Hindi UI.
Build order: see the build ladder (slices 1–7) in the Sort Game Preparation Plan; only the current slice's issues may be `ready`.

## Boosters
Undo, Shuffle, Extra Slot (temporary 6th slot) at launch; Magnet and Time Freeze later. 2 of each free during the tutorial. Cost: coins or one rewarded ad.

## Worlds and objects
1. Amma's Kitchen (1–20): filter coffee tumbler (dabara set), pressure cooker, idli stand, tawa, masala dabba, steel tiffin, coconut scraper, brass kudam, rolling pin (belan), chai kettle.
2. Street Food Lane (21–40): kulhad, pani puri, vada, banana leaf, samosa, jalebi, dosa, vada pav, bhutta, kulfi.
3. Bazaar (41–60): glass bangles, brass lamp, jasmine string, spice sack, kolhapuri chappal (never mixed with pooja items), umbrella, clay pot, silk thread, kite, tea box.
Pongal pack (10 levels, at launch): Pongal pot, sugarcane, turmeric plant, kolam powder, banana, rice sack, jaggery, cow bells, flowers, clay stove.
4. Wedding Season (61–80) and 5. Festival India (81–100+) arrive as weekly updates.

### Object ids (master list)
Level files may only use these ids (snake_case, case-sensitive). The code copy is `ObjectCatalog` in `Assets/Scripts/Levels/`; keep the two in step. Add a world's ids when its levels are made.
- World 1, Amma's Kitchen (confirmed by Muthu, 28 Oct 2026): `tumbler` (filter coffee tumbler, dabara set), `pressure_cooker`, `idli_stand`, `tawa`, `masala_dabba`, `steel_tiffin`, `coconut_scraper`, `brass_kudam`, `rolling_pin` (belan), `chai_kettle`.

### Level rules (decided 28 Oct 2026)
- A level must never start with a ready-made triple in a front row. This includes a shelf whose front row is empty and whose next non-empty layer is a triple, because that layer moves forward at start. The level loader rejects such levels; triples in deeper layers are fine.
- Dropping an item back on the shelf it came from is a cancel: no move counted, no shake.
- After the last available level, show "More levels coming soon" with a Replay button that goes to L1. Next never loops back to L1 (Muthu, PR #17).
- Shelf kinds: `normal` and `locked` are supported. The loader rejects `conveyor` (after launch) with a "not supported yet" error.

### Locked shelf (issue #19)
- A locked shelf takes no moves. You can't pick up its items or drop anything on it. Its items stay visible.
- Every triple clear anywhere on the board counts every locked shelf down by 1. A clear on a neighbouring shelf counts the same as any other. Chained clears (a revealed layer that clears) count once each.
- When the count reaches 0 the shelf opens and plays like a normal shelf from then on. Core raises `ShelfUnlocked(shelfIndex)` once, right after the clear that opened it.
- A locked shelf may hold hidden layers. They stay put while it is locked.
- `lockClears` must be 1 or more on a locked shelf, and absent or 0 on every other shelf.
- A locked shelf may not start with a triple in its front row (same rule as every shelf). It would clear the moment it opens.
- At least one empty front slot must be on an open shelf at the start. Free slots on locked shelves don't count.
- The loader rejects a lock that can never open: `lockClears` larger than (items on the other shelves) / 3. Deeper "can this lock be reached in play" checks are left to the solver (#20).
- Stuck: no legal move between open shelves. With no move, no clear can happen, so no lock can open: Stuck is final. Stuck never fires while a legal move exists, and a clear that opens a shelf with room counts before Stuck is checked.
- Win: every item cleared, including those on locked shelves.
- Tapping a locked item, or dropping onto a locked shelf, gives the normal invalid-move shake.
- Placeholder look: dark scrim over the shelf and a brass padlock on its top-right corner showing the clears left. The number bumps on each clear; the padlock grows and fades out when the shelf opens.

### Fail (issue #41)
- A level is lost when a move leaves items on the board, no legal move, and no booster that could help. Core raises `Stuck` (the fact) and then `Failed` (the loss); the fail screen shows on `Failed`.
- "No booster can help" is asked through `IBoosterRescue` on the board. Until boosters ship (#42) nothing can help, so every Stuck is a fail. With a booster that could help, the board is stuck but not failed, and the fail screen waits.
- Fail screen: "Out of space!" with Retry. Retry restarts the same level from its start state.
- A rewarded-ad Continue button sits on the fail screen from Slice 6. Until then it exists but is hidden and does nothing.
- The Continue button is the main button: above Retry, larger, labelled "Free Extra Slot" with a small video icon, because what the player gets is an extra slot. When it shows, Retry becomes the smaller secondary button. While it is hidden, Retry alone is the main button (Muthu, PR #51).
- The final wording of the Continue button is checked in Slice 6, when the rewarded ad is wired.
- No interstitial and no penalty after a fail: no ad, no lost coins, stars or unlocks, nothing saved. The player just tries again.

### Stars and coins (issue #44)
Code: `Game.Core.WinRewards` (rules), `Game.Core.RewardConfig` (amounts), `Game.Core.MoveCounter` (moves), the wallet in the progress save (`Game.Services.ProgressService`). Rulings by Muthu on PR #63.
- Moves: every successful move since the level was loaded. A drop back on the same shelf is a cancel and doesn't count. Retry starts again at 0.
- Undone moves still count. Undo (#53) takes a move back on the board but never takes it off the move count: the count only goes up until the next level load. `MoveCounter` has no way to lower it, and `MoveCounterTests` fails if one is added.
- Stars, against the solver's shortest win (`solver.minMoves` in the level file):
  - 3 stars: moves at or under minMoves x 1.5.
  - 2 stars: moves at or under minMoves x 2.5.
  - 1 star: anything more. Every win earns at least 1 star.
- Thresholds that aren't whole numbers are not rounded. With minMoves 5, 7 moves is 3 stars (7 ≤ 7.5) and 8 moves is 2 stars. The code compares 2 x moves with 3 x minMoves and 5 x minMoves, so the check is exact.
- A level with no minMoves (missing, 0 or negative, for example a test board the solver hasn't stamped) gives 3 stars. Every shipped level must have one (`LevelParserTests`).
- First win of a level: 10 coins, plus 5 for each star above 1. So 1 star = 10, 2 stars = 15, 3 stars = 20.
- Replay (the level was won before): 5 coins for each star above the previous best stars, otherwise 0. Same stars or fewer earn nothing. A 1-star first win (10) then a 3-star replay (+10) totals 20, the same as a 3-star first win; going 1, 2, 3 stars pays 10 + 5 + 5. A level never pays more than 20 in total.
- The amounts (10 and 5) are config values, `RewardConfig.Default` (`FirstWinCoins`, `CoinsPerExtraStar`), not literals in the rules. There is no remote config in the project yet; when Firebase Remote Config is wired, a service builds a `RewardConfig` from it and sets `LevelBootstrap.Rewards`.
- The save keeps the best stars per level, and stars never go down.
- The win, its best stars, its coins and the unlock are saved in one write, the moment the board is won.
- Wallet: saved with progress (`coins` in `progress.json`) and never negative. A spend can't take more coins than the wallet holds; with too few coins nothing changes and nothing is saved. Boosters spend from the wallet (prices: #56).
- A save written before coins existed loads as 0 coins and keeps its stars and unlocks.
- A fail costs nothing (see Fail).
- Clears earn no coins and fly no coin (the per-clear coin flight was removed).
- HUD: the coin pill shows the wallet balance and updates on every spend at once. The number is always drawn at full size (20 dp) and never shrinks; the pill widens leftwards to fit it, from 84 dp (up to 3 digits) to 103.5 dp (5 digits). Above 99,999 it shows "99K+" ("more than 99 thousand"): "99999+" would not fit beside the three 48 dp booster slots on a 360 dp phone. The booster slots are sized for the widest pill, so they never move.
- Win card: three star slots. The earned stars are lit in marigold and the rest show as dim empty stars. The dim colour #D7CCC8 is a placeholder until the Slice 4 art pass.
- Win card coins: under the stars, a "+N" line with a coin shows the coins this win earned (string key `win.coins_earned`, English "+{0}"; no localisation system yet, so the English template sits in code next to the key, like the other Win card text). A replay that earns 0 has no line at all (hidden, not "+0"), and the card is as tall as before.
- The coins are saved the moment the board is won, but the pill keeps the old balance until they arrive: once the stars have landed, coins fly from the "+N" line into the pill (one coin per 5 earned, up to 6), the pill rises above the card's dim backdrop, and its number counts up as each coin lands, ending on the wallet. Leaving the card (Next, Retry) or any other panel shows the whole balance at once; nothing is ever lost.

### Board size and touch areas (decided by Muthu, Oct 2026)
- The 4–12 shelf layout is fixed. Don't shrink or move shelves or items to make tap targets bigger.
- Each item's touch area is bigger than its picture: it extends into the gaps between items and halfway into the gaps between shelves, to reach 48 dp wherever there's room.
- 44 dp is the floor on 720p phones (the biggest boards).
- Every level has at most 12 shelves, for the whole game. No per-phone special cases (Muthu, 2026-10-29). The loader and the solver pack tests reject a level with more than 12 shelves.
- Columns: the layout uses the column count (1, 2 or 3) that gives the biggest items, so small boards stay one column (Muthu, 2026-10-29).
- Levels 1 to 20 use at most 8 shelves.

### First-try win-rate targets (decided by Muthu, Oct 2026)
Measured by the solver's first-try estimate (`estWinRate`):
- Levels 1 to 6 and 8 to 9: at least 0.90.
- Level 7 (designed near-loss) and level 10 (boss): at least 0.75.
- Level 11 onward: normal 0.70 to 0.85; hard and boss 0.35 to 0.50.

## FTUE (first-time user flow)
L1: one rule, "Match 3!", hand pointer. L2: hidden layer, no text. L11: locked shelf, one short line. Each tutorial runs on first play and skips itself once completed; no Skip button, because each is only a move or two and skipping could miss the one rule a new player needs (Muthu, PR #59). The hand and arrow stay code-drawn placeholders until art briefs E7 (tutorial hand, plus a pressed version) and E8 (yellow curved arrow) are imported in Slice 4. Tutorial text is English at launch. L3: first win, stars + coins. L5: Undo taught free. L7: designed near-loss, Extra Slot free once. L10 boss: Pongal kitchen, first album card (album UI ships in February). No interstitial before level 12 or after a fail. Notification permission asked after level 10.

## Monetisation (launch build)
AdMob rewarded (boosters, continue, double coins) and interstitial (from L12, max 1 per 3 min, never after a fail, off for payers). IAP via Unity IAP: Remove Ads ₹199/$2.99, starter pack ₹99/$1.99 (after L15), coin packs ₹79–₹799, continue 900 coins, Pongal festival pack ₹149/$3.99.

## Analytics events
game_loaded, level_start, level_complete (moves, time, stars, boosters), level_fail (reason, items_left), booster_used, rewarded_offer_shown, rewarded_watched, interstitial_shown, album_card_earned, daily_started, iap_view, iap_purchase, rating_prompt, session_end. Every event: level_id, world, build_version, ab_variant.

## Milestones
| Milestone | Date | Done when |
| --- | --- | --- |
| Core mechanic on phone | 2026-11-06 | Triples + layers playable on Android, 5 levels |
| Prototype | 2026-11-20 | 20 levels, FTUE L1–L10, internal build |
| Gate 1 playtest | 2026-11-27 | 15 of 20 new testers finish L10 untaught |
| Launch build | 2026-12-24 | 60 levels + Pongal pack, boosters, daily puzzle, ads, IAP, Firebase, store listing |
| Closed test | 2026-12-26 → 2027-01-08 | 12+ testers for 14 days, no P1 bugs |
| Launch (India) | 2027-01-12 | Live on Google Play |
| Gate 2 retention | 2027-03-31 | D1 35%+, D7 12%+, day-0 playtime 20 min+, rating 4.3+ |
