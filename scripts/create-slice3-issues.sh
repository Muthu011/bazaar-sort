#!/usr/bin/env bash
# Creates Slice 3 issues (first-time flow and boosters) as `proposed`.
# Run once from the repo root. Approve by relabelling to `ready`.
set -e
new() { gh issue create --title "$1" --label "proposed,$2" --body "$3" >/dev/null && echo "created: $1"; }

new "Portrait phone layout and safe areas" "area:ui,p1,needs-editor" "## Goal
The game is built and tested as a portrait phone game, not in a free-aspect landscape window.

## Acceptance criteria
- [ ] Player Settings: portrait only; Game view presets added for 1080x2400 (20:9), 1080x1920 (16:9) and 720x1600
- [ ] Board sits in the middle band; top bar (level number, Levels button) and bottom bar (boosters, coins, settings) respect the notch and gesture-bar safe areas
- [ ] Shelves scale to fill the width, 2 or 3 per row; items keep the 48 dp touch rule (44 dp floor on 720p)
- [ ] Settings button opens a small panel; Haptics toggle moves into it
- [ ] FPS and frame-time text shows only in development builds
- [ ] Screenshots of L1, L10 and L15 at all three sizes in the PR"

new "Fail state: out of moves, fail screen, retry" "area:code,p1,needs-editor" "## Goal
A level can be lost, so winning means something.

## Acceptance criteria
- [ ] Core: stuck detection already exists; raise Failed when no legal move remains and no booster can help
- [ ] Fail screen: \"Out of space!\" with Retry and, from Slice 6, a rewarded-ad continue (button hidden for now)
- [ ] No interstitial or penalty after a fail (rule in docs/game-design.md)
- [ ] Edit Mode tests for fail detection; Play Mode test drives a designed fail"

new "Undo, Shuffle and Extra Slot boosters" "area:code,p1,needs-editor" "## Goal
The three launch boosters from docs/game-design.md.

## Acceptance criteria
- [ ] Core: Undo (last move, unlimited history within a level), Shuffle (re-deal visible items, never creating an instant triple), Extra Slot (temporary 6th slot for the rest of the level)
- [ ] Each booster costs coins from the wallet; 2 of each free during the tutorial
- [ ] Booster bar at the bottom with counts; disabled state when unusable
- [ ] Animations use DOTween in Game.View only
- [ ] Edit Mode tests per booster, including Shuffle never making a triple and Undo after a clear"

new "Tutorial for L1, L2, L5, L7 and L11" "area:ui,p1,needs-editor" "## Goal
A new player learns every rule without reading.

## Acceptance criteria
- [ ] L1: hand pointer, \"Match 3!\", only the correct move is tappable until the first triple
- [ ] L2: the hidden layer is shown once with an arrow, no text
- [ ] L5: Undo taught free; L7: Extra Slot free once; L11: the locked shelf explained in one short line
- [ ] Driven by the level JSON \`tutorial\` field; skippable after first completion
- [ ] Short screen recording of L1 and L2 in the PR"

new "Real star rule and coin wallet" "area:systems,p2" "## Goal
Replace the always-3-stars placeholder and the display-only coin counter.

## Acceptance criteria
- [ ] Stars by moves against the solver minimum: 3 stars at or under minMoves x 1.5, 2 stars at or under x 2.5, otherwise 1; rule in Core with tests and in docs/game-design.md
- [ ] Coins: 10 per win plus 5 per star above 1; saved with progress; spent by boosters
- [ ] Best stars per level kept in the save; Edit Mode tests for earn, spend and save"

new "Levels w1_l016 to w1_l020" "area:levels,p2" "## Goal
Five more levels so the Gate 1 build has 20.

## Acceptance criteria
- [ ] Sawtooth curve continues; L20 is a boss
- [ ] At most 8 shelves; solver passes with targets from docs/game-design.md
- [ ] Notes added to pack-2.md"

new "Home screen and level path (placeholder art)" "area:ui,p2,needs-editor" "## Goal
The app opens to a simple home screen instead of straight into a level.

## Acceptance criteria
- [ ] Home: game title, big Play button (continues at the next level), coins, settings
- [ ] Level path: a scrolling list of level nodes with stars; locked levels greyed
- [ ] Back from a level returns to the path; dev level select stays in dev builds only
- [ ] Placeholder shapes only; real art comes in Slice 4"
