# QA report: issue #7, World 1 levels 1-5

Branch: task/7-levels-w1-l001-l005 (HEAD 93ba9d2), 4 commits ahead of origin/main.

## Checklist
1. Tests: Edit Mode 237/237 passed (includes 17 World1PackTests), Play Mode 14/14 passed. No failures.
2. Console: forced refresh and compile, then ran all tests. Errors: 0. Warnings: 0 from game code. One MCP-FOR-UNITY "WebSocket is not initialised" warning appeared during the domain reload (editor tooling transport, not the repo); it did not reappear after tests.
3. Levels (hand check of all 5 JSON files plus tests):
   - Required fields present, schema 1, ids/index match, kind normal only, no locked/conveyor, trays/wrapped empty.
   - Object ids are all World 1 ids (tumbler, idli_stand, masala_dabba, tawa, pressure_cooker, steel_tiffin).
   - Every type count is 3 (multiple of 3). Items: 9, 9, 12, 15, 18.
   - No starting front-row triple; each level has an empty front slot.
   - Layouts are distinct (tested). Shelves 4,4,4,5,6, all at most 8.
   - Hidden layers 0, 1, 1, 2, 2. L1 tag match3, L2 tag layers, L5 tag undo.
   - Role easy for all, within the easy band by shelf and type count. No solver block (see notes).
4. Play Mode screenshot: not taken. The optional in-game check of w1_l005 through LevelBootstrap was skipped. No screenshot is attached, and the 6-shelf layout fit is unverified visually. Only the loader/Core path is verified.
5. Acceptance criteria:
   - 5 JSON files following the schema, World 1 ids: MET (checked by hand).
   - L1 match-3 only: MET (0 hidden layers, tutorial match3). L2 one hidden layer: MET (exactly 1, asserted). L3-L5 grow 4 to 6 shelves: MET (4, 5, 6, asserted).
   - Loads without validation errors: MET (Level_LoadsWithoutErrors x5 pass).
   - Winnable by hand: MET. Level_RecordedLineWins replays each recorded line through Board.TryMove (the real Core move, no shortcuts), asserts each move is accepted, asserts no early win, then asserts IsWon and ItemCount == 0.
   - pack-1.md with one line of intent per level: MET (table has one Intent per level, plus the winning lines).
6. Player-facing notes: no problems seen from the data. Not visually verified. L3 has no tutorial tag (acceptable). pack-1.md says Undo is taught at L5, but the Undo tutorial is only a tag here; confirm the tutorial system supports it.

## Other checks
- Progression assertions exist and are real: Progression_L1TeachesMatch3WithNoHiddenLayers, _L2AddsExactlyOneHiddenLayer, _L3ToL5GrowFromFourToSixShelves, Levels_StayWithinEightShelvesAndAreDistinct. Each asserts concrete values.
- Tests weakened: no. The diff of Assets/Tests against origin/main only adds World1PackTests.cs(+.meta); nothing removed.
- Packages: no changes under Packages/.
- Stray files: working tree clean. No TestRunnerApi or callback code found in the repo.
- Religious items: none used.
- Solver gate: NOTE, not a failure. CLAUDE.md DoD item 4 requires the solver, which does not exist until Slice 2. Solvability is proven only by one recorded line per level; difficulty band, uniqueness by canonical hash, and solver.minMoves are not checked by a solver. The solver check should be rerun on these five levels once it lands. pack-1.md states that L4 and L5 lines are not proven minimal.
- Not checked: frame time and build size (no change expected, only data and tests added).

RESULT: PASS

Failures: none. Follow-ups: (1) rerun the solver on these levels in Slice 2; (2) add a 6-shelf screenshot of w1_l005 to docs/qa/7/.
