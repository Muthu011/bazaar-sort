# World 1, pack 1 (levels 1 to 5)

Hand-checked first five levels for the first phone build (issue #7). Solver numbers (issue #20) are stored in each level's `solver` block. Each level is proven winnable by a recorded line replayed in
`Assets/Tests/EditMode/World1PackTests.cs`. Moves are (from shelf, from slot, to shelf), 0-based; the item lands in
the target shelf's leftmost free front slot. Lengths of the L1 to L3 lines are the minimum (exhaustive search); L4 and L5 lines are the shortest found, not proven minimal. All five are `easy`.

| Level | Shelves | Hidden layers | Items | Types | Line (moves) | Intent |
| --- | --- | --- | --- | --- | --- | --- |
| w1_l001 | 4 | 0 | 9 | 3 | 3 | Teach match-3 only: one row per shelf, no hidden layers. |
| w1_l002 | 4 | 1 | 9 | 3 | 3 | Add one hidden layer: clearing the tumblers slides the second row forward. |
| w1_l003 | 4 | 1 | 12 | 4 | 8 | First win: same shelf count, more types, one hidden layer; free slots are limited so a short plan is needed, but no dead ends (win rate 0.992; the first layout was 0.882 and failed the 0.90 floor). |
| w1_l004 | 5 | 2 | 15 | 5 | 13 | Grow to 5 shelves with two hidden layers; park items on the spare shelf. |
| w1_l005 | 6 | 2 | 18 | 6 | 18 | Grow to 6 shelves; the board is wider but still forgiving (Undo is taught here). |

## Winning lines

- L1: (1,1,0) (2,2,1) (3,0,2)
- L2: (1,1,0) (0,1,1) (2,0,0)
- L3: (0,0,1) (2,0,3) (0,1,2) (0,0,3) (1,2,0) (0,1,1) (2,1,0) (3,0,2)
- L4: (0,1,1) (0,2,3) (2,0,3) (0,0,2) (0,1,3) (0,2,3) (1,0,0) (3,1,1) (0,0,3) (4,1,3) (1,0,0) (2,2,1) (0,0,2)
- L5: (3,1,5) (0,0,1) (4,1,0) (0,0,1) (0,1,2) (1,2,2) (0,2,1) (1,0,0) (3,0,1) (0,0,2) (1,0,0) (0,0,2) (1,1,0) (0,0,1) (1,0,2) (4,0,1) (2,1,1) (3,2,2)

## Notes

- Objects are all from the World 1 list; no religious items are used.
- Tutorial tags: L1 `match3`, L2 `layers`, L5 `undo` (FTUE: Undo taught free at L5).
- Sawtooth roles for the full 10-level pack (hard L5, breather L6, boss L10) come with levels 6 to 10; these five are the
  early easy ramp.
