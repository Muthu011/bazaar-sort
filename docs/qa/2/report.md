# QA report - #2 Core board model: shelves, moves, triple clear, layer reveal

Branch: `task/2-core-board-model` (stacked on `task/1-project-skeleton`, PR #9) - Date: 2026-10-28
Scope: `git diff c4326d8..HEAD` (commits 3d18467, 3858a15): Board.cs, BoardEvents.cs, Shelf.cs, ShelfTests.cs, BoardTests.cs, plus .meta files.

The QA agent's Unity MCP tools could not connect (the agent files still use the old `unityMCP` server name). The test and console results below are the producer's run through Unity MCP on HEAD 3858a15, not re-run by QA. Code review, meta-file, style and asmdef checks were done by QA directly from the repo.

## Results
| Check | Result | Evidence |
| --- | --- | --- |
| Edit Mode tests | PASS (producer) | 56/56 passed, 0 failed, 0 skipped (ShelfTests 9, BoardTests 40 cases, GameRules 7) |
| Play Mode tests | PASS (producer) | 1/1 passed (`SmokePlayModeTests.PlayerLoop_TicksAFrame_AndCoreIsReachable`) |
| Console | PASS (producer) | 0 errors, 0 warnings after the runs |
| Core has no UnityEngine | PASS | `grep UnityEngine Assets/Scripts/Core` matches only a comment in GameRules.cs; Game.Core.asmdef has `noEngineReferences: true` |
| .meta files | PASS | All 5 new files have a committed .meta (Board, BoardEvents, Shelf, BoardTests, ShelfTests) |
| Style | PASS | PascalCase public members, `_camelCase` private fields, no singletons or static instances |
| Working tree | PASS | Clean before the report was added |
| Levels / solver | n/a | No level files changed |
| Screens / screenshots | n/a | Pure C#, no scene or UI |
| Frame time / build size | n/a | No scene; no runtime change in a build |

## Acceptance criteria
| # | Criterion | Status | Code | Tests |
| --- | --- | --- | --- | --- |
| 1 | Shelf with 3 front slots and layers behind | Met | `Shelf.SlotCount = 3`, `_front`, `_hidden`, `HiddenLayerCount`, `GetHiddenItem` | `Shelf_HasThreeFrontSlots`, `Constructor_FrontRowAndHiddenLayers_FromLevelData`, `Constructor_EmptyStringIsEmptySlot`, `Constructor_CopiesInput`, `Constructor_RejectsWrongSlotCount`, `Constructor_RejectsNoLayersOrNull`, `FullFrontRow_HasNoFreeSlot`, `Getters_RejectOutOfRange`, `Construct_CountsItemsInAllLayers` |
| 2 | TryMove rejects full shelf, empty slot, same slot | Met (see note 1) | `Board.CanMove` returns TargetFull / EmptySlot / SameShelf, plus InvalidShelf / InvalidSlot / GameOver | `TryMove_ToFullShelf_IsRejected`, `TryMove_FromEmptySlot_IsRejected`, `TryMove_ToSameShelf_IsRejected`, `TryMove_OutOfRangeIndices_AreRejectedWithoutThrowing` (6 cases), `CanMove_DoesNotChangeBoard`, `TryMove_Legal_MovesItemToLeftmostFreeSlot`, `TryMove_LeftmostFreeSlot_SkipsFilledSlots`, `AfterWin_MovesAreRejectedAsGameOver_AndWonDoesNotRepeat`, `StuckBoard_RejectsEveryMove` |
| 3 | Triple on front row clears; next layer moves forward when front is empty | Met | `Shelf.TryClearFrontMatch`, `Shelf.RevealNextLayer`, `Board.Settle` loop | `MoveCompletingTriple_ClearsFrontRow`, `ThreeDifferentItems_DoNotClear`, `TwoIdenticalItems_DoNotClear`, `ItemIds_AreCaseSensitive`, `ClearWithLayerBehind_RevealsNextLayer`, `PartialFrontRow_DoesNotRevealLayer`, `MovingLastFrontItemAway_RevealsNextLayer`, `RevealedLayerThatIsATriple_ClearsImmediately_AndRevealsNext`, `EmptyHiddenLayer_IsSkippedToNextLayer`, `ClearWithNoLayersLeft_LeavesEmptyFront`, `ClearedShelf_CanReceiveItemsAgain`, `Construct_EmptyFrontWithHiddenLayer_RevealsSilently` |
| 4 | Win when no items remain; stuck when no legal move | Met | `IsWon`, `IsStuck`, `HasLegalMove`, end of `TryMove` | `ClearingLastItems_RaisesWonOnce_AndNotStuck`, `WonIsRaisedAfterFinalClearAndReveals_NotMidMove`, `ItemsLeftInHiddenLayers_IsNotWon`, `Construct_EmptyBoard_IsWonAndNotStuck`, `MoveAndRevealThatFillEveryShelf_RaisesStuckOnce_AfterSettle`, `MoveThatFillsLastFreeSlotButClears_IsNotStuck`, `StuckIsNotRaisedMidMove_WhenTargetClearFreesSpace`, `StuckWhenRevealFillsTheOnlyFreeShelf`, `SingleShelfWithFreeSlot_IsStuck_BecauseSameShelfMovesAreIllegal`, `ConstructionDoesNotRaiseEvents_EvenIfInitiallyStuck` |
| 5 | Events Moved, Cleared, LayerRevealed, Won, Stuck | Met | `Board` events with `MoveInfo`, `ClearInfo`, `LayerRevealInfo`; order documented in the class summary | Event-capturing assertions in the clear / reveal / win / stuck tests above; `SmallLevel_PlaysThroughToWin` for an end-to-end run |
| 6 | Edit Mode tests cover every rule incl. edge cases | Met | - | 49 new test cases across ShelfTests and BoardTests (all rules above, plus chained reveals, empty hidden layer, case sensitivity, no events at construction, mid-move Won/Stuck suppression) |

## Code review (rule bugs)
No rule bugs found.
- Event order is Moved, then source settle, then target settle, then at most one of Won or Stuck. Won and Stuck are checked only after both shelves settle, so there is no mid-move false Stuck or Won.
- Reveal chains are handled: `Settle` loops clear then reveal until stable. An empty hidden layer is skipped by looping, and `_itemCount` stays correct.
- `_itemCount` is only decremented on clear. Won is raised once, and Stuck once (after Stuck no move is accepted, so a repeat cannot happen).
- `IsWon` is checked before index validation in `CanMove`, so any move on a won board returns GameOver. This is fine.

## Notes (not failures)
1. "Same slot" in the issue is implemented as "same shelf" (an item cannot move to the shelf it is on), because items always land in the leftmost free slot. This is documented in the XML comment and covered by a test. Muthu or the designer should confirm this reading before the view relies on it.
2. A starting front row that already contains a triple is not cleared at construction (documented). Level validation or the solver must reject such levels.
3. A board that starts Stuck raises no event at construction (documented and tested). The view must check `IsStuck` once at level start.
4. Stuck means only "no shelf pair is available". It does not detect unwinnable-but-movable states. That belongs to the solver, not this issue.
5. `Shelf` has a `params string[][]` overload next to the `IReadOnlyList` one. This is harmless but could make overload resolution ambiguous for callers passing `null`.

RESULT: PASS
