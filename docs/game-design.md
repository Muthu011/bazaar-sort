# Bazaar Sort — game design summary for agents

Source of truth: the "Indian Object Sort Game: Go-Live Plan" doc. This file is the short version agents work from; update it when the plan changes.

## Core
- Board: 4–14 shelves per level. Each shelf has 3 front slots and up to 4 layers behind.
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

### Board size and touch areas (decided by Muthu, Oct 2026)
- The 4–14 shelf layout is fixed. Don't shrink or move shelves or items to make tap targets bigger.
- Each item's touch area is bigger than its picture: it extends into the gaps between items and halfway into the gaps between shelves, to reach 48 dp wherever there's room.
- 44 dp is the floor on 720p phones (the 13–14 shelf boards).
- Levels 1 to 20 use at most 8 shelves.

## FTUE (first-time user flow)
L1: one rule, "Match 3!", hand pointer. L2: hidden layer, no text. L3: first win, stars + coins. L5: Undo taught free. L7: designed near-loss, Extra Slot free once. L10 boss: Pongal kitchen, first album card (album UI ships in February). No interstitial before level 12 or after a fail. Notification permission asked after level 10.

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
