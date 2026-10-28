# QA report: issue #20 level solver and pack tests (branch task/20-level-solver, HEAD 3ce7134)

## Tests
- Edit Mode: 346/346 passed (14.4 s). Play Mode: 32/32 passed (12.3 s).
- Console after forced refresh and compile: no errors or warnings from the project (only an MCP-for-Unity WebSocket notice, tooling noise).

## Core changes (Board.Clone, Shelf.Clone)
- Board.Clone builds a new Board from cloned shelves. Shelf.Clone uses MemberwiseClone (all value fields, including _nextHidden) and gives the copy its own _front array. Hidden layers are shared by reference; they are only read (RevealNext copies a layer into _front slot by slot), so sharing is safe. BoardCloneTests cover front rows, hidden layers, independence after moves, and no subscribers.
- The only Core edits are those two Clone methods and `_front` losing `readonly`. No rule changed.

## Solver uses Core rules
- Move legality comes only from Board.CanMove (SearchContext.GenerateMoves); moves are applied with Board.TryMove on clones; HasLegalMove and IsWon are used for stuck and win. No legality logic is re-implemented. The solver only prunes duplicate/equivalent moves (same-type items in a row, identical shelf twins), which are filtered after or around CanMove, never added.

## LevelPackTests failure cases
- Unsolvable: Level_IsSolvable asserts Solvability.Solvable and Complete (read from code; not run on a bad level).
- Duplicate hash: Pack_HasNoDuplicateLayouts groups by hash (read from code; LayoutHashTests cover hash equivalence).
- Out-of-band win rate: Level_EstWinRateIsInItsBand asserts a band exists and contains the rate (read from code).
- Stale solver block: verified by experiment. Temporarily edited w1_l002 estWinRate 1.0 to 0.5; Level_StoredSolverBlockMatchesFreshSolve failed with "stale solver block", all else passed. File reverted (git status clean).

## Bands
- DifficultyBands matches level-designer.md: levels 1-10 0.90-1.00, medium 0.70-0.85, hard and boss 0.35-0.50. Author's choice: easy and breather (from level 11) 0.85-1.00; documented in code and schema.
- `git diff 9356e59 HEAD` touches only pack-1.md, w1_l003.json and World1PackTests.cs. Solver sources, bands, play policy, N and seed are unchanged.

## L3 fix and test integrity
- w1_l003: s1 front tawa becomes tumbler, s4 slot 2 tumbler becomes tawa. Still 4 shelves, 4 types, 12 items, one hidden layer, minMoves 8. estWinRate 0.882 to 0.992, deadEndRate 0.017 to 0.
- World1PackTests change is only the L3 winning line (8 moves); no assertion removed or loosened. Across the whole branch, tests are only added, plus Game.Solver added to the test asmdef references.
- L1, L2, L4, L5 JSON diffs (commit f5265a5) add only the `solver` line (plus the comma on the preceding line).
- Minor: pack-1.md prose was reflowed into longer lines; harmless.

## Performance
- SolverPerformanceTests asserts result.Seconds < 10 and replays the solution on a clone. Logged: "14-shelf synthetic solve: Solvable, minMoves 31, 3225 states expanded, 357640 boards built, 2.60 s". Scope per Muthu's decision: 14 shelves, 48 items, 4 hidden layers; larger boards report unknown (proposed #28).

## Packaging and hygiene
- Game.Solver asmdef: includePlatforms Editor only, so it is excluded from player builds; only Game.Tests.EditMode references it. Newtonsoft is already approved. No package changes (Packages/ untouched).
- Assets/Scripts/Editor/SolverMenu.cs is also editor-only. No stray temp files; Unity's InitTestScene file from the Play Mode run was gone afterwards; git status is clean.

## docs/level-schema.md
- New "solver block" section is accurate against the code: fields, A* with budget (2,000,000 states or 60 s gives unknown), 1000 plays seed 20270112, policy, deadEndRate and hash definitions, and the band table.

## Not covered
- No screenshots: no player-facing screen changed. Frame time and build size not measured (editor-only code, no runtime change beyond Clone methods).
- Unsolvable and duplicate-hash failure paths were checked by reading, not by a mutation run.

RESULT: PASS

Failures: none. Notes for author: none blocking.
