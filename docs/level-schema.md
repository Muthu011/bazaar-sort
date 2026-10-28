# Level schema (v1)

One JSON file per level: `Assets/Levels/World<N>/w<N>_l<NNN>.json`.

```json
{
  "schema": 1,
  "id": "w1_l001",
  "world": 1,
  "index": 1,
  "role": "easy",              // easy | medium | hard | breather | boss
  "tutorial": "match3",        // optional: match3 | layers | undo | extra_slot
  "moveLimit": null,           // null = unlimited
  "timeLimitSec": null,        // null = no timer
  "shelves": [
    {
      "id": "s1",
      "kind": "normal",        // normal | locked | conveyor
      "lockClears": 0,         // locked: clears needed to open
      "layers": [              // layers[0] is the front row; null = empty slot
        ["tumbler", "tumbler", null],
        ["dabba", "dabba", "dabba"]
      ]
    }
  ],
  "trays": [                   // Set Trays (from level 21); optional
    { "id": "t1", "set": ["idli_plate", "sambar_bowl", "chutney_cup"] }
  ],
  "wrapped": ["s3:1:0"],       // optional: shelf:layer:slot positions hidden until a neighbour clears
  "solver": {                  // written by the solver, never by hand
    "solvable": true,
    "minMoves": 4,
    "deadEndRate": 0.0,
    "estWinRate": 0.97,
    "hash": "…"
  }
}
```

Rules:
- Each shelf has exactly 3 slots per layer.
- Every object type appears a multiple of 3 times, except objects used by trays (which must match their tray sets exactly).
- Object ids come from `docs/game-design.md` world lists, in snake_case.
- At least one empty front slot must exist at the start.
- `solver.hash` is a canonical hash (layout normalised for mirroring and object renaming) used to reject duplicates.

## The `solver` block (issue #20)

Written by the editor menu **Bazaar Sort/Run Solver on All Levels** (`Game.Solver.SolverRunner`), which rewrites only
this one line of each file. Edit Mode `LevelPackTests` fail when a stored block disagrees with a fresh solve, so run the
menu after every level change. Code: `Assets/Scripts/Editor/Solver/` (editor-only assembly `Game.Solver`). Every move
is applied by `Game.Core.Board`, so the solver uses exactly the game's rules.

- `solvable`, `minMoves`: exact shortest win, found by A* over canonical board states (shelves are interchangeable
  whatever their order, locked ones included since each shelf's lock counter is part of the state; item order inside a
  row never matters; object types are relabelled canonically, so boards that differ only by renaming types are one
  state) with an admissible heuristic. A search that runs out of budget (2,000,000 expanded states or 60 s) reports
  "unknown" and nothing is written.
- Level budget (issue #31): one analysis of one level (shortest win, simulated plays and dead-end searches together)
  may build at most 40,000,000 boards and take at most 30 s. When either runs out the analysis stops, is reported as
  INCONCLUSIVE with the reason, and nothing is written; `LevelPackTests` then fail with that message instead of hanging.
- `estWinRate`: wins out of 1000 simulated first-try plays, seed 20270112. The simulated player prefers any move that
  clears a triple; otherwise it picks at random among moves that don't lead back to a board it has already seen in that
  play (no undoing, no circling). It loses when stuck, when every move leads back to a seen board, or after 300 moves.
  Each of (shelf, item type, target shelf) is one choice.
- `deadEndRate`: of the distinct boards (not yet won) that those 1000 plays passed through, the fraction from which a win
  is no longer reachable. "Distinct" and the player's "already seen" use the same board identity as before issue #31
  (normal shelves in any order are the same board; other shelf kinds keep their place; object types by name), so the
  stored numbers did not change when the search keys became canonical.
- `hash`: first 16 hex digits of SHA-256 over the canonical layout. Same hash for any shelf order, mirroring, item order
  inside a row and renaming of object types; different for any change to a row or hidden layer, or to a shelf's kind or
  `lockClears`. Two levels with the same hash are duplicates.

Target `estWinRate` bands (`Game.Solver.DifficultyBands`, from the level-designer targets): levels 1 to 10 0.90 to
1.00 whatever the role, except level 7 (designed near-loss) and level 10 (boss) at 0.75 to 1.00; `medium` 0.70 to 0.85; `hard` and `boss` 0.35 to 0.50; `easy` and `breather` (from level 11)
0.85 to 1.00. The level number is `index`.
