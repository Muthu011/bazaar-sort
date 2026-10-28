using System.Collections;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Drives w1_l001 through the real input layer: simulated mouse and touch devices from the Input System test
    /// fixture, <see cref="BoardInput"/> reading <see cref="Pointer.current"/>, hit testing, <see cref="MoveInput"/>
    /// and the board. Pointer positions are worked out from the view's own layout, so the tests do not depend on the
    /// Game view size.
    /// </summary>
    public class BoardInputPlayModeTests : InputTestFixture
    {
        private const string LevelPath = "Assets/Levels/World1/w1_l001.json";
        private const int DragSteps = 6;

        private GameObject _cameraObject;
        private GameObject _viewObject;
        private Camera _camera;
        private BoardView _view;
        private Board _board;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();

            _cameraObject = new GameObject("Test Camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _viewObject = new GameObject("Board View");
            _view = _viewObject.AddComponent<BoardView>();
            _viewObject.AddComponent<BoardInput>().Configure(_view);

            var result = LevelLoader.Load(File.ReadAllText(LevelPath), "w1_l001.json");
            Assert.IsTrue(result.Success, "w1_l001 should load.");
            _board = result.Board;
            _view.Bind(_board, _camera, new Rect(0, 0, _camera.pixelWidth, _camera.pixelHeight), BoardLayoutSettings.Default);
        }

        public override void TearDown()
        {
            Object.Destroy(_viewObject);
            Object.Destroy(_cameraObject);
            base.TearDown();
        }

        // ---- Pointer helpers ----

        private Vector2 Tile(int shelf, int slot) => _view.Layout.GetFrontSlotRect(shelf, slot).center;

        private Vector2 ShelfCentre(int shelf) => _view.Layout.GetShelfRect(shelf).center;

        /// <summary>A point in the HUD strip at the top: on screen but on no shelf.</summary>
        private Vector2 Elsewhere => new Vector2(_camera.pixelWidth * 0.5f, _camera.pixelHeight - 2f);

        /// <summary>Positions round-trip through world units, so compare within a hundredth of a pixel.</summary>
        private static void AssertNear(Vector2 expected, Vector2 actual, string message = null)
        {
            Assert.Less(Vector2.Distance(expected, actual), 0.01f, $"{message} expected {expected} but was {actual}");
        }

        /// <summary>Clears and reveals animate (issue #6); the drawn board settles a moment after the move.</summary>
        private IEnumerator WaitForAnimations()
        {
            float waited = 0f;
            while (_view.IsAnimating && waited < 2f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsFalse(_view.IsAnimating, "Clear and reveal animations finish.");
        }

        private IEnumerator Click(Vector2 position)
        {
            Move(_mouse.position, position);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
        }

        private IEnumerator MouseDrag(Vector2 from, Vector2 to)
        {
            Move(_mouse.position, from);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            for (int i = 1; i <= DragSteps; i++)
            {
                Move(_mouse.position, Vector2.Lerp(from, to, (float)i / DragSteps));
                yield return null;
            }
            Release(_mouse.leftButton);
            yield return null;
        }

        private IEnumerator TapTap(int fromShelf, int fromSlot, int toShelf)
        {
            yield return Click(Tile(fromShelf, fromSlot));
            Assert.IsTrue(_view.IsHighlightVisible, $"Tap on ({fromShelf},{fromSlot}) highlights it.");
            Assert.AreEqual(fromShelf, _view.SelectedShelf);
            Assert.AreEqual(fromSlot, _view.SelectedSlot);
            yield return Click(ShelfCentre(toShelf));
            Assert.IsFalse(_view.IsHighlightVisible, "Highlight goes once the move is made.");
        }

        // ---- Full wins ----

        [UnityTest]
        public IEnumerator FullWin_W1L001_ByTapTap()
        {
            yield return null; // BoardInput binds to the board

            yield return TapTap(2, 2, 3); // idli stand s3 -> s4
            Assert.AreEqual("idli_stand", _board.GetShelf(3).GetFrontItem(1));
            yield return TapTap(1, 1, 0); // tumbler s2 -> s1, clears
            Assert.IsTrue(_board.GetShelf(0).IsFrontEmpty);
            yield return TapTap(3, 0, 2); // masala dabba s4 -> s3, clears
            yield return TapTap(3, 1, 1); // idli stand s4 -> s2, clears and wins

            Assert.IsTrue(_board.IsWon, "w1_l001 is won through tap-tap input.");
            yield return WaitForAnimations();
            Assert.AreEqual(0, _view.VisibleFrontTileCount);
            Assert.AreEqual(BoardView.WonText, _view.StatusText);
        }

        [UnityTest]
        public IEnumerator FullWin_W1L001_ByDrag()
        {
            yield return null;

            yield return MouseDrag(Tile(2, 2), ShelfCentre(3));
            Assert.AreEqual("idli_stand", _board.GetShelf(3).GetFrontItem(1), "Drag moves the item.");
            AssertNear(_view.GetFrontTileHome(2, 2), _view.GetFrontTilePixelPosition(2, 2), "Dragged tile object reset to its slot.");
            Assert.IsFalse(_view.IsTileMoving, "A landed drag does not glide or shake.");

            yield return MouseDrag(Tile(1, 1), Tile(0, 2));
            yield return MouseDrag(Tile(3, 0), ShelfCentre(2));
            yield return MouseDrag(Tile(3, 1), ShelfCentre(1));

            Assert.IsTrue(_board.IsWon, "w1_l001 is won through drag input.");
            Assert.AreEqual(BoardView.WonText, _view.StatusText);
        }

        // ---- Feedback ----

        [UnityTest]
        public IEnumerator Drag_TileFollowsPointer_ThenInvalidDropSnapsBackWithShake()
        {
            yield return null;
            var home = _view.GetFrontTileHome(0, 0);
            var target = ShelfCentre(1); // s2 is full

            Move(_mouse.position, home);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            var midway = Vector2.Lerp(home, target, 0.5f);
            Move(_mouse.position, midway);
            yield return null;
            Assert.IsTrue(_view.IsDragging);
            Assert.AreEqual(midway.x, _view.GetFrontTilePixelPosition(0, 0).x, 0.5f, "Tile follows the pointer.");
            Assert.AreEqual(midway.y, _view.GetFrontTilePixelPosition(0, 0).y, 0.5f);
            Assert.Greater(_view.GetFrontSortingOrder(0, 0), _view.GetFrontSortingOrder(1, 0), "Dragged tile draws on top.");

            Move(_mouse.position, target);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;

            Assert.AreEqual("tumbler", _board.GetShelf(0).GetFrontItem(0), "Board unchanged after an invalid drop.");
            Assert.AreEqual(3, _board.GetShelf(1).FrontItemCount);
            Assert.IsTrue(_view.IsTileMoving, "Tile glides back.");

            bool shook = false;
            float timeout = Time.time + 2f;
            while (_view.IsTileMoving && Time.time < timeout)
            {
                if (_view.IsShaking && Mathf.Abs(_view.GetFrontTilePixelPosition(0, 0).x - home.x) > 0.01f) shook = true;
                yield return null;
            }

            Assert.IsTrue(shook, "Invalid drop shakes the tile.");
            Assert.IsFalse(_view.IsTileMoving, "Motion finishes.");
            AssertNear(home, _view.GetFrontTilePixelPosition(0, 0), "Tile is back in its slot.");
        }

        [UnityTest]
        public IEnumerator DragOntoOwnShelf_ReturnsHomeWithoutShake()
        {
            yield return null;
            yield return MouseDrag(Tile(0, 0), Tile(0, 2));
            Assert.IsTrue(_view.IsTileMoving, "Tile glides back.");
            Assert.IsFalse(_view.IsShaking, "A cancel is not a mistake: no shake.");
            yield return new WaitForSeconds(TileMotion.Duration(true, true) + 0.1f);
            AssertNear(_view.GetFrontTileHome(0, 0), _view.GetFrontTilePixelPosition(0, 0));
            Assert.AreEqual("tumbler", _board.GetShelf(0).GetFrontItem(0));
        }

        [UnityTest]
        public IEnumerator TapItem_HighlightsIt_TapElsewhereCancels()
        {
            yield return null;
            var home = _view.GetFrontTileHome(2, 2);

            yield return Click(Tile(2, 2));
            Assert.IsTrue(_view.IsHighlightVisible);
            Assert.AreEqual(2, _view.SelectedShelf);
            Assert.AreEqual(2, _view.SelectedSlot);
            Assert.Greater(_view.GetFrontTilePixelPosition(2, 2).y, home.y, "Selected tile lifts.");
            AssertNear(_view.GetFrontTilePixelPosition(2, 2), _view.HighlightPixelPosition, "Highlight sits behind the tile.");

            yield return Click(Elsewhere);
            Assert.IsFalse(_view.IsHighlightVisible, "Tap elsewhere cancels.");
            Assert.AreEqual(-1, _view.SelectedShelf);
            AssertNear(home, _view.GetFrontTilePixelPosition(2, 2), "Tile is put back down.");
            Assert.AreEqual("idli_stand", _board.GetShelf(2).GetFrontItem(2), "No move.");
        }

        [UnityTest]
        public IEnumerator TapInvalidShelf_ShakesAndDeselects()
        {
            yield return null;
            yield return Click(Tile(0, 0));
            yield return Click(ShelfCentre(1)); // full
            Assert.IsFalse(_view.IsHighlightVisible);
            Assert.IsTrue(_view.IsShaking, "Refused tap move shakes the item.");
            Assert.AreEqual(3, _board.GetShelf(1).FrontItemCount, "No move.");
        }

        // ---- Touch ----

        [UnityTest]
        public IEnumerator Touch_TapTapAndDrag_MoveItems()
        {
            var touch = InputSystem.AddDevice<Touchscreen>();
            touch.MakeCurrent();
            yield return null;

            // Tap-tap: idli stand s3 -> s4.
            BeginTouch(1, Tile(2, 2));
            yield return null;
            EndTouch(1, Tile(2, 2));
            yield return null;
            Assert.IsTrue(_view.IsHighlightVisible, "Touch tap selects.");
            BeginTouch(2, ShelfCentre(3));
            yield return null;
            EndTouch(2, ShelfCentre(3));
            yield return null;
            Assert.AreEqual("idli_stand", _board.GetShelf(3).GetFrontItem(1), "Touch tap-tap moves.");

            // Drag: tumbler s2 slot 1 -> s1, which clears the tumbler triple.
            var from = Tile(1, 1);
            var to = ShelfCentre(0);
            BeginTouch(3, from);
            yield return null;
            for (int i = 1; i <= DragSteps; i++)
            {
                MoveTouch(3, Vector2.Lerp(from, to, (float)i / DragSteps));
                yield return null;
            }
            EndTouch(3, to);
            yield return null;
            Assert.IsTrue(_board.GetShelf(0).IsFrontEmpty, "Touch drag moves and the triple clears.");
        }

        // ---- Hit target size on the live layout ----

        [UnityTest]
        public IEnumerator LiveLayout_HitTargetsAreAtLeast48Px()
        {
            yield return null;
            var layout = _view.Layout;
            for (int shelf = 0; shelf < layout.ShelfCount; shelf++)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    var r = layout.GetSlotHitRect(shelf, slot);
                    Assert.GreaterOrEqual(Mathf.Min(r.width, r.height), BoardLayout.MinTouchTargetPx, $"Shelf {shelf} slot {slot}");
                }
            }
        }
    }
}
