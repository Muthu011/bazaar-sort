# World 1, pack 2 (levels 6 to 14)

Issue #23. Levels 6 to 14 of the World 1 sawtooth; L15 (hard) is split into its own issue until the solver handles it
(see "Solver hangs on locked levels"). Solver numbers come from each level's `solver` block (seed 20270112, 1000 plays):
minimum moves (A*), first-try win-rate estimate and dead-end rate. Targets: L1 to L6 and L8 to L9 at least 0.90; L7 and
L10 at least 0.75; from L11, easy 0.85 to 1.00, medium 0.70 to 0.85 (docs/game-design.md).

| Level | Role | Shelves | Locks | Hidden layers | Items | Types | Min moves | Win rate | Dead ends | Intent |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| w1_l006 | breather | 6 | 0 | 1 | 18 | 6 | 11 | 0.992 | 0.000 | Breather after the onboarding ramp: two layers, six types, lots of room. |
| w1_l007 | medium | 7 | 0 | 2 | 36 | 10 | 21 | 0.932 | 0.001 | Designed near-loss: full ten-type board with tight free slots; Extra Slot is offered free once (tutorial `extra_slot`). |
| w1_l008 | hard | 7 | 0 | 2 | 39 | 10 | 22 | 0.908 | 0.001 | First hard level: three layers deep across 7 shelves; the challenge is planning, not luck. |
| w1_l009 | hard | 8 | 0 | 2 | 45 | 10 | 29 | 0.901 | 0.001 | Hard: grows to the 8-shelf cap with three layers and the longest line so far. |
| w1_l010 | boss | 8 | 0 | 3 | 51 | 10 | 29 | 0.926 | 0.001 | Boss, Pongal kitchen theme from World 1 objects: biggest board of the first ten, four layers deep. |
| w1_l011 | easy | 6 | 1 | 1 | 21 | 7 | 11 | 0.940 | 0.002 | Introduces the locked shelf on an easy board: one lock that opens after 1 clear. |
| w1_l012 | easy | 6 | 1 | 1 | 21 | 7 | 15 | 0.943 | 0.001 | Easy lock practice: one lock that needs 2 clears. |
| w1_l013 | medium | 7 | 2 | 2 | 33 | 9 | 23 | 0.788 | 0.002 | Medium: two locks (2 and 3 clears) so the player chooses which to open first. |
| w1_l014 | medium | 7 | 2 | 2 | 39 | 10 | 25 | 0.796 | 0.002 | Medium: two locks (2 and 1 clears) on a ten-type board. |

## Re-check of levels 1 to 5

| Level | Min moves | Win rate | Dead ends |
| --- | --- | --- | --- |
| w1_l001 | 3 | 1.000 | 0.000 |
| w1_l002 | 3 | 1.000 | 0.000 |
| w1_l003 | 8 | 0.992 | 0.000 |
| w1_l004 | 9 | 0.976 | 0.001 |
| w1_l005 | 8 | 0.998 | 0.000 |

## Notes

- Objects are all from the World 1 list; no religious items are used. At most 8 shelves (levels 1 to 20 rule).
- New mechanic for the pack: the locked shelf, introduced on an easy level (L11) and used in every level after it.
- L7 to L10 stay forgiving because the onboarding targets keep the first-try estimate high; their difficulty comes from
  board size, layer depth and line length rather than dead ends.
