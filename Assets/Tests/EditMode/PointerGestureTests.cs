using System.Collections.Generic;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="PointerGesture"/>: raw pointer events on a real <see cref="BoardLayout"/> become taps and drags,
    /// and the view is told what to show. Board: w1_l001's shape on a 1080x1920 screen.
    /// </summary>
    public class PointerGestureTests
    {
        private sealed class FakeView : IBoardInputView
        {
            public readonly List<string> Calls = new List<string>();

            public BoardLayout Layout { get; set; }

            public void ShowSelection(int shelf, int slot) => Calls.Add($"Select {shelf}:{slot}");
            public void ClearSelection() => Calls.Add("Clear");
            public void BeginDrag(int shelf, int slot, Vector2 screenPosition) => Calls.Add($"BeginDrag {shelf}:{slot}");
            public void DragTo(Vector2 screenPosition) => Calls.Add("DragTo");
            public void EndDrag(int shelf, int slot, DropFeedback feedback) => Calls.Add($"EndDrag {shelf}:{slot} {feedback}");
            public void Shake(int shelf, int slot) => Calls.Add($"Shake {shelf}:{slot}");
        }

        private Board _board;
        private FakeView _view;
        private PointerGesture _gesture;

        [SetUp]
        public void SetUp()
        {
            _board = new Board(
                new Shelf(new[] { "tumbler", "tumbler", null }),
                new Shelf(new[] { "idli_stand", "tumbler", "idli_stand" }),
                new Shelf(new[] { "masala_dabba", "masala_dabba", "idli_stand" }),
                new Shelf(new[] { "masala_dabba", null, null }));
            _view = new FakeView
            {
                Layout = BoardLayout.Compute(4, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default),
            };
            _gesture = new PointerGesture(new MoveInput(_board), _view);
        }

        private Vector2 Tile(int shelf, int slot) => _view.Layout.GetFrontSlotRect(shelf, slot).center;

        private void Tap(Vector2 p)
        {
            _gesture.PointerDown(p);
            _gesture.PointerUp(p);
        }

        private void Drag(Vector2 from, Vector2 to)
        {
            _gesture.PointerDown(from);
            _gesture.PointerMove(Vector2.Lerp(from, to, 0.5f));
            _gesture.PointerMove(to);
            _gesture.PointerUp(to);
        }

        [Test]
        public void Tap_OnItem_ShowsSelection()
        {
            Tap(Tile(2, 2));
            CollectionAssert.AreEqual(new[] { "Select 2:2" }, _view.Calls);
            Assert.IsTrue(_gesture.Input.HasSelection);
        }

        [Test]
        public void TapTap_MovesAndClearsSelection()
        {
            Tap(Tile(2, 2));
            Tap(Tile(3, 2));
            CollectionAssert.AreEqual(new[] { "Select 2:2", "Clear" }, _view.Calls);
            Assert.AreEqual(InputOutcomeKind.Moved, _gesture.LastOutcome.Kind);
            Assert.AreEqual("idli_stand", _board.GetShelf(3).GetFrontItem(1));
        }

        [Test]
        public void Tap_Elsewhere_Cancels()
        {
            Tap(Tile(2, 2));
            Tap(new Vector2(540f, 1900f)); // HUD strip
            CollectionAssert.AreEqual(new[] { "Select 2:2", "Clear" }, _view.Calls);
            Assert.AreEqual(InputOutcomeKind.Deselected, _gesture.LastOutcome.Kind);
        }

        [Test]
        public void Tap_InvalidTarget_ClearsAndShakes()
        {
            Tap(Tile(0, 0));
            Tap(Tile(1, 0)); // shelf 1 is full
            CollectionAssert.AreEqual(new[] { "Select 0:0", "Clear", "Shake 0:0" }, _view.Calls);
            Assert.AreEqual(MoveResult.TargetFull, _gesture.LastOutcome.Result);
        }

        [Test]
        public void SmallWobble_IsStillATap()
        {
            var p = Tile(2, 2);
            _gesture.PointerDown(p);
            _gesture.PointerMove(p + new Vector2(_gesture.DragThreshold * 0.5f, 0f));
            _gesture.PointerUp(p + new Vector2(_gesture.DragThreshold * 0.5f, 0f));
            CollectionAssert.AreEqual(new[] { "Select 2:2" }, _view.Calls);
        }

        [Test]
        public void DragThreshold_ScalesWithTile_ButHasAFloor()
        {
            Assert.AreEqual(Mathf.Max(PointerGesture.MinDragThresholdPx, _view.Layout.TileSize * PointerGesture.DragThresholdTiles),
                _gesture.DragThreshold, 0.001f);
            Assert.GreaterOrEqual(_gesture.DragThreshold, PointerGesture.MinDragThresholdPx);
        }

        [Test]
        public void Drag_ToValidShelf_Lands()
        {
            Drag(Tile(2, 2), Tile(3, 2));
            CollectionAssert.AreEqual(new[] { "BeginDrag 2:2", "DragTo", "DragTo", "EndDrag 2:2 Landed" }, _view.Calls);
            Assert.AreEqual("idli_stand", _board.GetShelf(3).GetFrontItem(1));
        }

        [Test]
        public void Drag_ToFullShelf_SnapsBackWithShake()
        {
            Drag(Tile(0, 0), Tile(1, 1));
            Assert.AreEqual("EndDrag 0:0 SnapBackWithShake", _view.Calls[_view.Calls.Count - 1]);
            Assert.AreEqual("tumbler", _board.GetShelf(0).GetFrontItem(0), "Board unchanged.");
        }

        [Test]
        public void Drag_ToOwnShelf_ReturnsHome()
        {
            Drag(Tile(0, 0), Tile(0, 2));
            Assert.AreEqual("EndDrag 0:0 ReturnHome", _view.Calls[_view.Calls.Count - 1]);
        }

        [Test]
        public void Drag_FromEmptySlot_DoesNotDrag_AndActsAsTapAtPressPoint()
        {
            Tap(Tile(2, 2));
            _view.Calls.Clear();
            Drag(Tile(3, 2), new Vector2(540f, 100f)); // press on shelf 3's empty slot, slide away
            CollectionAssert.AreEqual(new[] { "Clear" }, _view.Calls, "The release is a tap on shelf 3, which moves there.");
            Assert.AreEqual("idli_stand", _board.GetShelf(3).GetFrontItem(1));
        }

        [Test]
        public void Drag_ReplacesTapSelection()
        {
            Tap(Tile(2, 2));
            _view.Calls.Clear();
            _gesture.PointerDown(Tile(0, 0));
            _gesture.PointerMove(Tile(3, 1));
            CollectionAssert.AreEqual(new[] { "Clear", "BeginDrag 0:0" }, _view.Calls);
        }

        [Test]
        public void Cancel_MidDrag_ReturnsHome()
        {
            _gesture.PointerDown(Tile(0, 0));
            _gesture.PointerMove(Tile(3, 1));
            _gesture.PointerCancel();
            Assert.AreEqual("EndDrag 0:0 ReturnHome", _view.Calls[_view.Calls.Count - 1]);
            Assert.IsFalse(_gesture.IsPressed);
            Assert.IsFalse(_gesture.Input.IsDragging);
        }

        [Test]
        public void NoLayout_IgnoresInput()
        {
            _view.Layout = null;
            Tap(new Vector2(100f, 100f));
            Assert.IsEmpty(_view.Calls);
        }

        // ---- Locked shelves (issue #19) ----

        private void UseLockedBoard()
        {
            // Shelf 1 is locked: B _ _.
            _board = new Board(
                new Shelf(new[] { "tumbler", "tumbler", null }),
                Shelf.Locked(1, new[] { "idli_stand", null, null }),
                new Shelf(new[] { "tumbler", "idli_stand", "idli_stand" }),
                new Shelf(new[] { "masala_dabba", null, null }));
            _gesture = new PointerGesture(new MoveInput(_board), _view);
        }

        [Test]
        public void Tap_OnLockedItem_ShakesItWithoutSelecting()
        {
            UseLockedBoard();
            Tap(Tile(1, 0));
            CollectionAssert.AreEqual(new[] { "Clear", "Shake 1:0" }, _view.Calls);
            Assert.AreEqual(MoveResult.SourceLocked, _gesture.LastOutcome.Result);
            Assert.IsFalse(_gesture.Input.HasSelection);
        }

        [Test]
        public void Drag_FromLockedItem_DoesNotLiftIt_AndShakesOnRelease()
        {
            UseLockedBoard();
            Drag(Tile(1, 0), Tile(3, 1));
            CollectionAssert.AreEqual(new[] { "Clear", "Shake 1:0" }, _view.Calls);
            Assert.AreEqual("idli_stand", _board.GetShelf(1).GetFrontItem(0));
        }

        [Test]
        public void Drag_OntoLockedShelf_SnapsBackWithShake()
        {
            UseLockedBoard();
            Drag(Tile(2, 1), Tile(1, 2));
            Assert.AreEqual("EndDrag 2:1 SnapBackWithShake", _view.Calls[_view.Calls.Count - 1]);
            Assert.AreEqual(MoveResult.TargetLocked, _gesture.LastOutcome.Result);
        }
    }
}
