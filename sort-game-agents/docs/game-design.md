# Bazaar Sort — game design summary for agents

Source of truth: the "Indian Object Sort Game: Go-Live Plan" doc. This file is the short version agents work from; update it when the plan changes.

## Core
- Board: 6–14 shelves per level. Each shelf has 3 front slots and up to 4 layers behind.
- Move: tap or drag a front object to any shelf with a free front slot.
- Clear: 3 identical objects on a shelf's front row clear; the next layer slides forward.
- Win: all objects cleared. Fail: out of moves, out of time, or no legal move left (offer continue).
- Session length: 60–120 s per level.

## Mechanics (first level)
Basic triple (1) · Hidden layer (4) · Locked shelf, opens after N clears (11) · Set Trays: tray asks for 3 different related objects, icons shown (21) · Wrapped item, revealed by a neighbouring clear (31) · Soft timer (41) · Conveyor shelf (61) · Boss every 10th level.

## Boosters
Undo, Shuffle, Extra Slot (temporary 6th slot), Magnet (pulls one triple), Time Freeze. 2 of each free during the tutorial. Cost: coins or one rewarded ad.

## Worlds and objects
1. Amma's Kitchen (1–20): filter coffee tumbler (dabara set), pressure cooker, idli stand, tawa, masala dabba, steel tiffin, coconut scraper, brass kudam, rolling pin (belan), chai kettle.
2. Street Food Lane (21–40): kulhad, pani puri, vada, banana leaf, samosa, jalebi, dosa, vada pav, bhutta, kulfi.
3. Bazaar (41–60): glass bangles, brass lamp, jasmine string, spice sack, kolhapuri chappal (never mixed with pooja items), umbrella, clay pot, silk thread, kite, tea box.
Pongal pack (10 levels, at launch): Pongal pot, sugarcane, turmeric plant, kolam powder, banana, rice sack, jaggery, cow bells, flowers, clay stove.
4. Wedding Season (61–80) and 5. Festival India (81–100+) arrive as weekly updates.

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
