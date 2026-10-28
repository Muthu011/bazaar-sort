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
