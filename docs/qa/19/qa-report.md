# QA report: issue #19 Locked shelf (branch task/19-locked-shelf)

## 1. Tests and console
- Refresh (force, compile): clean. Console after refresh and after both runs: 0 errors, 0 warnings.
- Edit Mode: 329/329 passed. Play Mode: 35/35 passed. No failures.

## 2. Test diff against origin/main (Assets/Tests)
- Only removed test: `Validate_LockedShelf_IsNotSupportedYet`, replaced by `Validate_LockedShelf_IsAccepted` and others (accepted with items and hidden layers, etc.).
- `Validate_ConveyorShelf_IsNotSupportedYet` unchanged, same message: `shelf kind "conveyor" is not supported yet (conveyor shelves come after launch); use "normal"`.
- The unknown-kind test is unchanged. No thresholds, Ignore or Explicit attributes touched. Other edits are additions only (MoveInputTests, PointerGestureTests, LevelLoaderTests, LevelValidatorTests).
- Note: the validator message for conveyor no longer contains the "locked ... Slice 2" branch, which is expected.

## 3. Core vs View
- Lock rules are in Core only: Shelf (LockClears, LockClearsLeft, IsLocked, internal CountDownLock), Board (CanPick, CanMove lock checks, HasLegalMove, Settle countdown, ShelfUnlocked). No UnityEngine, view or selection state added to Core.
- MoveInput only calls `Board.CanPick` / `CanMove` / `TryMove` and maps the verdict to Rejected or Ignored. It has no lock rules of its own.

## 4. Validator vs World 1
- Assets/Levels is unchanged against origin/main (w1_l001 to w1_l005 untouched). World1PackTests pass, so the new rules (open-shelf empty front slot, lockClears bounds, LockNeverOpens) do not reject them. The new rules only affect locked shelves or non-zero lockClears.

## 5. Event order and Stuck
- Code: CountDownLocks runs before Cleared is raised, RaiseUnlocks runs right after it, Won/Stuck are decided after the full settle.
- Tests: `MultipleLocks_OneClearOpensSeveral_InShelfOrderAfterCleared` (Moved, Cleared, Unlocked 0, Unlocked 2), `Lock_OpensAtExactlyLockClears_AndRaisesEventOnce`, `LockOpeningOnTheLastClear_RescuesTheBoard` (no Stuck while a legal move exists), `NotStuck_WhenTheSameRoomIsOnAnOpenShelf`, `SameBoard_LockNotYetOpen_IsStuck`, `Win_NeedsLockedItemsCleared` (Won last).
- Gap (minor): no test reads `LockClearsLeft` from inside a Cleared handler, so "counts update before Cleared" is proven by code order only, not asserted directly.

## 6. Play Mode through real input
`LockedShelf_CountsDown_PopsOff_ThenTakesMoves` uses InputTestFixture mouse clicks and drags: tap on a locked item shakes and does not select, drop on the locked shelf is refused, a neighbour clear changes the text "2" to "1" with a tick, the second clear opens the shelf and the padlock pops and disappears, then a tap-tap move onto the opened shelf succeeds and wins. `LockTickAndPop_AllocateNothing` moves the board (tick and pop start) then measures 40 AdvanceAnimations frames with `Is.Not.AllocatingGCMemory`, covering tick finish and the pop. Minor: the event-time work (setting the padlock text when the clear happens) is outside the measured window.

## 7. docs/game-design.md
"Locked shelf (issue #19)" section is present and matches behaviour (no moves, board-wide count, ShelfUnlocked once, hidden layers stay, lockClears bounds, starting triple rule, open-shelf empty slot, LockNeverOpens bound, Stuck, Win, shake, placeholder look). The shelf-kinds line was updated for conveyor.

## 8. Dependencies and art
No changes under Packages, ProjectSettings, Assets/Art or fonts. Padlock is drawn in code (PlaceholderSprites). Both screenshots are Git LFS (filter: lfs, pointer files in the index).

## 9. Screenshots (1080x1920 and 1080x2400)
- The padlock sits on the top-right corner of the locked shelf and its body overlaps the third hidden-strip "M" tile, partly covering it. The tile is still identifiable but it looks crowded and is a visual defect for players (the hidden layer's third item is partly hidden by the padlock). With a full third hidden slot the overlap would be worse.
- The scrim darkens the shelf and its items are still visible. The number "2" is legible.
- The padlock is about 115 px wide at 1080 wide, so it is above 48 px (it is not tappable anyway).

## 10. Stray files
No `Assets/InitTestScene*` files. Working tree was clean before this report.

## Issues for the author
1. (Minor, visual) Padlock overlaps the rightmost hidden-strip tile. Move it so it does not cover the hidden strip (for example above the shelf corner or inset to the right of the strip).
2. (Minor, test gap) Add a test that observes `LockClearsLeft` inside the Cleared handler to prove counts update before Cleared.
3. (Minor) Extend the allocation test to cover the event-time tick start.

RESULT: PASS
