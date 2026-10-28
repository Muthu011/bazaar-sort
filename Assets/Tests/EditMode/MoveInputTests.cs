using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="MoveInput"/>: the tap and drag state machine. It adds no rules; every verdict comes from
    /// <see cref="Board.CanMove"/>.
    /// </summary>
    public class MoveInputTests
    {
        private const string A = "tumbler";
        private const string B = "idli_stand";
        private const string C = "masala_dabba";

        private Board _board;
        private MoveInput _input;

        // Shelf 0: A A _   Shelf 1: B A B (full)   Shelf 2: C C B   Shelf 3: C _ _
        [SetUp]
        public void SetUp()
        {
            _board = new Board(
                new Shelf(new[] { A, A, null }),
                new Shelf(new[] { B, A, B }),
                new Shelf(new[] { C, C, B }),
                new Shelf(new[] { C, null, null }));
            _input = new MoveInput(_board);
        }

        private static BoardTarget At(int shelf, int slot) => BoardTarget.At(shelf, slot);

        private void AssertIdle()
        {
            Assert.IsFalse(_input.HasSelection, "No selection");
            Assert.IsFalse(_input.IsDragging, "Not dragging");
            Assert.AreEqual(-1, _input.ActiveShelf);
            Assert.AreEqual(-1, _input.ActiveSlot);
        }

        // ---- Tap ----

        [Test]
        public void TapItem_SelectsIt()
        {
            var outcome = _input.Tap(At(2, 2));
            Assert.AreEqual(InputOutcomeKind.Selected, outcome.Kind);
            Assert.AreEqual(2, outcome.Shelf);
            Assert.AreEqual(2, outcome.Slot);
            Assert.IsTrue(_input.HasSelection);
            Assert.AreEqual(2, _input.ActiveShelf);
            Assert.AreEqual(2, _input.ActiveSlot);
        }

        [Test]
        public void TapEmptySlot_NothingSelected_IsIgnored()
        {
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.Tap(At(0, 2)).Kind);
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.Tap(At(0, -1)).Kind, "Shelf but no slot column");
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.Tap(BoardTarget.None).Kind);
            AssertIdle();
        }

        [Test]
        public void TapTargetShelf_MovesSelectedItem()
        {
            _input.Tap(At(2, 2));
            var outcome = _input.Tap(At(3, 2)); // empty slot on shelf 3
            Assert.AreEqual(InputOutcomeKind.Moved, outcome.Kind);
            Assert.AreEqual(MoveResult.Ok, outcome.Result);
            Assert.AreEqual(2, outcome.Shelf);
            Assert.AreEqual(2, outcome.Slot);
            Assert.AreEqual(3, outcome.ToShelf);
            Assert.IsNull(_board.GetShelf(2).GetFrontItem(2));
            Assert.AreEqual(B, _board.GetShelf(3).GetFrontItem(1), "Lands in leftmost free slot (Core rule).");
            AssertIdle();
        }

        [Test]
        public void TapItemOnTargetShelf_AlsoMovesThere()
        {
            _input.Tap(At(2, 2));
            var outcome = _input.Tap(At(3, 0)); // an item on a shelf with room
            Assert.AreEqual(InputOutcomeKind.Moved, outcome.Kind);
            Assert.AreEqual(3, outcome.ToShelf);
        }

        [Test]
        public void TapMove_ThatClearsATriple_ReportsMoved()
        {
            _input.Tap(At(1, 1)); // tumbler
            var outcome = _input.Tap(At(0, 2));
            Assert.AreEqual(InputOutcomeKind.Moved, outcome.Kind);
            Assert.IsTrue(_board.GetShelf(0).IsFrontEmpty, "Triple cleared by Core.");
        }

        [Test]
        public void TapOffBoard_CancelsSelection()
        {
            _input.Tap(At(2, 2));
            var outcome = _input.Tap(BoardTarget.None);
            Assert.AreEqual(InputOutcomeKind.Deselected, outcome.Kind);
            Assert.AreEqual(2, outcome.Shelf, "Reports which item to un-highlight.");
            Assert.AreEqual(2, outcome.Slot);
            AssertIdle();
            Assert.AreEqual(B, _board.GetShelf(2).GetFrontItem(2), "Board unchanged.");
        }

        [Test]
        public void TapSelectedItemAgain_Cancels()
        {
            _input.Tap(At(2, 2));
            Assert.AreEqual(InputOutcomeKind.Deselected, _input.Tap(At(2, 2)).Kind);
            AssertIdle();
        }

        [Test]
        public void TapEmptyPartOfOwnShelf_Cancels()
        {
            _input.Tap(At(0, 0));
            Assert.AreEqual(InputOutcomeKind.Deselected, _input.Tap(At(0, 2)).Kind, "Empty slot");
            _input.Tap(At(0, 0));
            Assert.AreEqual(InputOutcomeKind.Deselected, _input.Tap(At(0, -1)).Kind, "Shelf frame");
            AssertIdle();
        }

        [Test]
        public void TapOtherItemOnSameShelf_SwitchesSelection()
        {
            _input.Tap(At(2, 0));
            var outcome = _input.Tap(At(2, 2));
            Assert.AreEqual(InputOutcomeKind.Selected, outcome.Kind);
            Assert.AreEqual(2, _input.ActiveShelf);
            Assert.AreEqual(2, _input.ActiveSlot);
        }

        [Test]
        public void TapInvalidTarget_FullShelf_IsRejectedAndClearsSelection()
        {
            _input.Tap(At(0, 0));
            var outcome = _input.Tap(At(1, 1)); // shelf 1 is full
            Assert.AreEqual(InputOutcomeKind.Rejected, outcome.Kind);
            Assert.AreEqual(MoveResult.TargetFull, outcome.Result, "Verdict comes from Board.CanMove.");
            Assert.AreEqual(0, outcome.Shelf, "Shake the selected item.");
            Assert.AreEqual(0, outcome.Slot);
            Assert.AreEqual(1, outcome.ToShelf);
            AssertIdle();
            Assert.AreEqual(A, _board.GetShelf(0).GetFrontItem(0), "Board unchanged.");
            Assert.AreEqual(3, _board.GetShelf(1).FrontItemCount);
        }

        [Test]
        public void Tap_AfterWin_IsIgnored()
        {
            var board = new Board(new Shelf(new[] { A, A, null }), new Shelf(new[] { A, null, null }));
            var input = new MoveInput(board);
            input.Tap(At(1, 0));
            Assert.AreEqual(InputOutcomeKind.Moved, input.Tap(At(0, 2)).Kind);
            Assert.IsTrue(board.IsWon);
            Assert.AreEqual(InputOutcomeKind.Ignored, input.Tap(At(0, 0)).Kind);
            Assert.IsFalse(input.HasSelection);
        }

        [Test]
        public void Tap_OutOfRangeTarget_IsIgnored()
        {
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.Tap(At(9, 0)).Kind);
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.Tap(At(0, 7)).Kind);
            AssertIdle();
        }

        [Test]
        public void TapOutOfRangeShelf_WithSelection_IsRejectedByCore()
        {
            _input.Tap(At(0, 0));
            var outcome = _input.Tap(At(9, 0));
            Assert.AreEqual(InputOutcomeKind.Rejected, outcome.Kind);
            Assert.AreEqual(MoveResult.InvalidShelf, outcome.Result);
        }

        // ---- Drag ----

        [Test]
        public void DragValid_MovesItem()
        {
            var start = _input.BeginDrag(At(2, 2));
            Assert.AreEqual(InputOutcomeKind.DragStarted, start.Kind);
            Assert.IsTrue(_input.IsDragging);
            Assert.IsFalse(_input.HasSelection, "Dragging is not a tap selection.");

            var drop = _input.Drop(At(3, -1));
            Assert.AreEqual(InputOutcomeKind.Moved, drop.Kind);
            Assert.AreEqual(3, drop.ToShelf);
            Assert.AreEqual(B, _board.GetShelf(3).GetFrontItem(1));
            AssertIdle();
        }

        [Test]
        public void DragInvalid_FullShelf_IsRejected_SoViewSnapsBackWithShake()
        {
            _input.BeginDrag(At(0, 0));
            var drop = _input.Drop(At(1, 2));
            Assert.AreEqual(InputOutcomeKind.Rejected, drop.Kind);
            Assert.AreEqual(MoveResult.TargetFull, drop.Result);
            Assert.AreEqual(0, drop.Shelf);
            Assert.AreEqual(0, drop.Slot);
            AssertIdle();
            Assert.AreEqual(A, _board.GetShelf(0).GetFrontItem(0), "Board unchanged.");
        }

        [Test]
        public void DragOntoOwnShelf_ReturnsHomeWithoutShake()
        {
            _input.BeginDrag(At(0, 1));
            var drop = _input.Drop(At(0, 2));
            Assert.AreEqual(InputOutcomeKind.ReturnedHome, drop.Kind);
            Assert.AreEqual(0, drop.Shelf);
            Assert.AreEqual(1, drop.Slot);
            AssertIdle();
        }

        [Test]
        public void DragOffBoard_ReturnsHomeWithoutShake()
        {
            _input.BeginDrag(At(0, 1));
            Assert.AreEqual(InputOutcomeKind.ReturnedHome, _input.Drop(BoardTarget.None).Kind);
            AssertIdle();
        }

        [Test]
        public void DragFromEmptySlot_IsIgnored_AndKeepsSelection()
        {
            _input.Tap(At(2, 2));
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.BeginDrag(At(0, 2)).Kind);
            Assert.IsTrue(_input.HasSelection);
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.Drop(At(3, 0)).Kind, "Drop without drag does nothing.");
        }

        [Test]
        public void DragItem_ReplacesTapSelection()
        {
            _input.Tap(At(2, 2));
            _input.BeginDrag(At(0, 0));
            Assert.IsTrue(_input.IsDragging);
            Assert.AreEqual(0, _input.ActiveShelf);
            Assert.AreEqual(0, _input.ActiveSlot);
        }

        [Test]
        public void TapWhileDragging_IsIgnored()
        {
            _input.BeginDrag(At(0, 0));
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.Tap(At(3, 0)).Kind);
            Assert.IsTrue(_input.IsDragging);
        }

        [Test]
        public void CancelDrag_ReturnsHome()
        {
            _input.BeginDrag(At(2, 1));
            var outcome = _input.CancelDrag();
            Assert.AreEqual(InputOutcomeKind.ReturnedHome, outcome.Kind);
            Assert.AreEqual(2, outcome.Shelf);
            Assert.AreEqual(1, outcome.Slot);
            AssertIdle();
            Assert.AreEqual(InputOutcomeKind.Ignored, _input.CancelDrag().Kind);
        }

        [Test]
        public void Reset_ForgetsSelection()
        {
            _input.Tap(At(2, 2));
            _input.Reset();
            AssertIdle();
        }

        [Test]
        public void Constructor_RejectsNullBoard()
        {
            Assert.Throws<System.ArgumentNullException>(() => new MoveInput(null));
        }
    }
}
