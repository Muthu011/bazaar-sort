#!/usr/bin/env bash
# Creates Slice 2 issues (layers, locks, solver and game feel, 9–13 Nov 2026) as `proposed`.
# Run once from the repo root. Approve by relabelling to `ready`.
set -e
new() { gh issue create --title "$1" --label "proposed,$2" --body "$3" >/dev/null && echo "created: $1"; }

new "Locked shelf: opens after N clears" "area:code,p1,needs-editor" "## Goal
Enable the \`locked\` shelf kind from docs/level-schema.md (\`lockClears\`).

## Acceptance criteria
- [ ] Core: a locked shelf accepts no moves and shows its items until the board has made \`lockClears\` triple clears, then opens
- [ ] Core raises a ShelfUnlocked event; Edit Mode tests cover lock, count down and open, including a clear that happens on the locked shelf's neighbour
- [ ] Loader accepts \`locked\` (conveyor still rejected with \"not supported yet\")
- [ ] View: placeholder padlock with the clears-left number, counts down on each clear, padlock pops off when it opens
- [ ] Rules written into docs/game-design.md"

new "Level solver and pack tests" "area:code,p1" "## Goal
Every level is proven solvable and rated before Muthu plays it.

## Acceptance criteria
- [ ] C# solver in an Editor or test assembly (no new dependency), using Game.Core rules so it can never disagree with the game
- [ ] Reports: solvable yes or no, shortest solution length, dead-end ratio, and an estimated first-try win rate from random-but-sensible play
- [ ] Writes the \`solver\` block into each level JSON, including the canonical duplicate \`hash\`
- [ ] Edit Mode \`LevelPackTests\` fails on any unsolvable level, any duplicate hash, or a win rate outside the level-designer targets
- [ ] Solves any 14-shelf level in under 10 s on the Mac; note the time in the PR"

new "Game feel pass (animation, particles, haptics)" "area:ui,p1,needs-editor" "## Goal
Moves feel lively and satisfying with placeholder shapes. Start after #8 merges.

## Acceptance criteria
- [ ] Choose DOTween (free) or Unity tweening and record the choice in docs/approved-dependencies.md
- [ ] Pick-up: item lifts with small squash and stretch and a shadow
- [ ] Move: item flies to the target shelf on a curved arc, about 0.25 s, ease-out
- [ ] Wrong move: soft shake plus a short haptic buzz
- [ ] Triple clear: squash, pop, particle burst, and a coin flies to the counter
- [ ] Layer reveal: next row slides forward with a small bounce; lock open has its own pop
- [ ] Win: stars drop in one by one with a scale punch, then confetti; buttons press down on tap
- [ ] Light Android haptics with an on/off setting
- [ ] 60 fps and no per-frame allocation during animations; short screen recording in the PR"

new "Progress save and level select for 15 levels" "area:systems,p2,needs-editor" "## Goal
The game remembers where the player is.

## Acceptance criteria
- [ ] Saves highest unlocked level and stars per level locally; survives app restart
- [ ] Opening the app continues at the next unplayed level
- [ ] Dev builds keep a level select for all 15 levels; release builds hide it
- [ ] After the last available level, show \"More levels coming soon\" with Replay
- [ ] Save code lives in Services, not Core; Edit Mode tests for load, save and a corrupt save file"

new "Levels w1_l006 to w1_l015 (re-check 1 to 5)" "area:levels,p1" "## Goal
Ten more World 1 levels on the sawtooth curve, all solver-checked. Needs the locked shelf and solver issues merged first.

## Acceptance criteria
- [ ] 10 JSON files using World 1 object ids, at most 8 shelves (levels 1 to 20 rule)
- [ ] Curve per .claude/agents/level-designer.md: L7 designed near-loss, L10 boss (Pongal kitchen), L11 introduces the locked shelf
- [ ] Solver passes all 15 levels, including the existing L1 to L5; fix any that fail
- [ ] pack-2.md with one line of intent and the solver numbers per level"

new "World 1 look page (art direction, no art in game)" "area:art,p2" "## Goal
A clear look for World 1 so Muthu can make test art. Nothing goes into the game this slice.

## Acceptance criteria
- [ ] docs/style-guide.md section: warm Madurai kitchen, brass and steel, soft shadows, bright but not neon; palette with hex values; line and shading rules
- [ ] Asset list with exact pixel sizes: the 10 World 1 objects, one shelf, one background, UI buttons and panels, padlock, coin, star
- [ ] Readability rules: every object recognisable at 48 dp, distinct silhouette and main colour per object
- [ ] Image-tool prompt for each of the 10 objects, matching the style page"
