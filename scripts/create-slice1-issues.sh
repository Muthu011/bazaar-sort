#!/usr/bin/env bash
# Creates Slice 1 issues (core sort on your phone, 2–6 Nov 2026) as `proposed`.
# Run once from the repo root after scripts/setup-github.sh. Approve by relabelling to `ready`.
set -e
new() { gh issue create --title "$1" --label "proposed,$2" --body "$3" >/dev/null && echo "created: $1"; }

new "Unity project skeleton and folder layout" "area:code,p1,needs-editor" "## Goal
A clean Unity 6 project that every later task builds on.

## Acceptance criteria
- [ ] Folders exactly as in CLAUDE.md (Scripts/Core, View, UI, Services; Levels; Art; Tests/EditMode, Tests/PlayMode)
- [ ] Assembly definitions: Game.Core (no UnityEngine references beyond math), Game.View, Game.Tests.EditMode, Game.Tests.PlayMode
- [ ] 2D URP, portrait only, Android target selected, package id set
- [ ] One passing Edit Mode test and one passing Play Mode test
- [ ] Unity console clean"

new "Core board model: shelves, moves, triple clear, layer reveal" "area:code,p1" "## Goal
All game rules for Slice 1 in plain C#, fully tested, with no Unity scene needed.

## Acceptance criteria
- [ ] Shelf with 3 front slots and a stack of layers behind
- [ ] Board.TryMove(fromShelf, fromSlot, toShelf) rejects illegal moves (full shelf, empty slot, same slot)
- [ ] Three identical items on a shelf's front row clear; next layer moves forward when the front row is empty
- [ ] Win when no items remain; stuck when no legal move exists
- [ ] Events raised for Moved, Cleared, LayerRevealed, Won, Stuck (for the view to animate)
- [ ] Edit Mode tests cover every rule above, including edge cases"

new "Level JSON loader and validation" "area:code,p1" "## Goal
Levels are data: load docs/level-schema.md files into the Core board.

## Acceptance criteria
- [ ] Loads Assets/Levels/World1/w1_l001.json into a Board
- [ ] Validation errors for: wrong slot count, item counts not a multiple of 3, unknown object id, no empty front slot
- [ ] Edit Mode tests with one valid and four invalid sample files"

new "Placeholder board view (shapes, not art)" "area:ui,p1,needs-editor" "## Goal
See the board on screen using simple coloured rounded squares with a 1-letter label per object type.

## Acceptance criteria
- [ ] Shelves laid out for portrait phones from 4 to 14 shelves, nothing off-screen on 16:9 and 20:9
- [ ] Each object type has a distinct colour and letter (placeholder table in code)
- [ ] View listens to Core events only; it holds no game rules
- [ ] Screenshot of w1_l001 on 16:9 and 20:9 in the PR"

new "Input: tap to pick and drop, plus drag" "area:code,p1,needs-editor" "## Goal
Players can move items with tap-tap or drag.

## Acceptance criteria
- [ ] Tap an item to select (highlight), tap a shelf to move; tap elsewhere to cancel
- [ ] Drag an item onto a shelf moves it; dropping on an invalid shelf snaps back with a small shake
- [ ] Touch targets at least 48 px; works with mouse in the editor
- [ ] Play Mode test drives a full win of w1_l001 through the input layer"

new "Clear, reveal and win feedback (placeholder)" "area:ui,p2,needs-editor" "## Goal
Clears feel good even with placeholder shapes.

## Acceptance criteria
- [ ] Triple clear: quick scale pop + fade under 0.5 s
- [ ] Layer reveal: items slide forward
- [ ] Win panel with Next and Retry; Stuck panel with Retry
- [ ] No allocations per frame during animations (checked in Profiler, note in PR)"

new "Levels w1_l001 to w1_l005" "area:levels,p2" "## Goal
Five easy levels to play on the phone this week.

## Acceptance criteria
- [ ] 5 JSON files following docs/level-schema.md, using World 1 object ids
- [ ] L1 teaches match-3 only; L2 adds one hidden layer; L3–L5 grow from 4 to 6 shelves
- [ ] Every level loads without validation errors and is winnable by hand (solver arrives in Slice 2)
- [ ] pack-1.md with one line of intent per level"

new "Android dev build on Muthu's phone" "area:release,p1,needs-editor" "## Goal
Muthu plays Slice 1 on a real Android phone by Friday 6 November.

## Acceptance criteria
- [ ] Development APK builds from the editor; level select for L1–L5 in dev builds only
- [ ] docs/build-android.md: exact steps to build and install over USB
- [ ] Runs at 60 fps on a mid-range phone (note the model in the PR)"
