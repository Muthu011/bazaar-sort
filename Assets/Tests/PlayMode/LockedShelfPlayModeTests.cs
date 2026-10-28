using System.Collections;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Locked shelf (issue #19) through the real input layer, on the test-only level t_locked_shelf: s4 is locked for
    /// two clears and holds two idli stands with a masala dabba triple behind them.
    /// </summary>
    public class LockedShelfPlayModeTests : InputTestFixture
    {
        private const string LevelPath = "Assets/Tests/PlayMode/LevelSamples/t_locked_shelf.json";
        private const int Locked = 3;
        private const int DragSteps = 6;
        private const float Frame = 1f / 60f;

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
            _board = Load();
            _view.Bind(_board, _camera, new Rect(0, 0, _camera.pixelWidth, _camera.pixelHeight), BoardLayoutSettings.Default);
        }

        public override void TearDown()
        {
            Object.Destroy(_viewObject);
            Object.Destroy(_cameraObject);
            base.TearDown();
        }

        private static Board Load()
        {
            var result = LevelLoader.Load(File.ReadAllText(LevelPath), "t_locked_shelf.json");
            Assert.IsTrue(result.Success, "t_locked_shelf should load.");
            return result.Board;
        }

        private Vector2 Tile(int shelf, int slot) => _view.Layout.GetFrontSlotRect(shelf, slot).center;

        private Vector2 ShelfCentre(int shelf) => _view.Layout.GetShelfRect(shelf).center;

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
            Assert.AreEqual(fromShelf, _view.SelectedShelf, $"Tap on ({fromShelf},{fromSlot}) selects it.");
            yield return Click(ShelfCentre(toShelf));
        }

        private IEnumerator WaitWhile(System.Func<bool> condition, string message)
        {
            float waited = 0f;
            while (condition() && waited < 2f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsFalse(condition(), message);
        }

        [UnityTest]
        public IEnumerator LockedShelf_CountsDown_PopsOff_ThenTakesMoves()
        {
            yield return null; // BoardInput binds to the board

            // Start: padlock with 2 on s4 only.
            Assert.IsTrue(_view.IsPadlockVisible(Locked));
            Assert.IsTrue(_view.IsScrimVisible(Locked));
            Assert.AreEqual("2", _view.GetPadlockText(Locked));
            for (int i = 0; i < Locked; i++) Assert.IsFalse(_view.IsPadlockVisible(i), $"Shelf {i} is open: no padlock.");

            // Tapping a locked item doesn't select it; it shakes.
            yield return Click(Tile(Locked, 0));
            Assert.AreEqual(-1, _view.SelectedShelf, "A locked item can't be selected.");
            Assert.IsTrue(_view.IsShaking, "A locked item shakes when tapped.");
            Assert.AreEqual(MoveResult.SourceLocked, _board.CanPick(Locked, 0));

            // Dropping onto the locked shelf is refused: snap back with shake, board unchanged.
            yield return MouseDrag(Tile(1, 1), ShelfCentre(Locked));
            Assert.AreEqual("idli_stand", _board.GetShelf(1).GetFrontItem(1), "Refused drop leaves the item home.");
            Assert.AreEqual(2, _board.GetShelf(Locked).FrontItemCount);
            Assert.IsTrue(_view.IsTileMoving, "Refused drop glides back.");
            yield return WaitWhile(() => _view.IsTileMoving, "Snap back finishes.");

            // Clear 1 on a neighbour (s1): the number counts down with a tick.
            yield return TapTap(1, 2, 0);
            Assert.IsTrue(_board.GetShelf(0).IsFrontEmpty, "Tumblers cleared on s1.");
            Assert.AreEqual("1", _view.GetPadlockText(Locked));
            Assert.IsTrue(_view.IsPadlockVisible(Locked));
            yield return WaitWhile(() => _view.IsPadlockTicking(Locked), "Tick finishes.");
            Assert.AreEqual(1f, _view.GetPadlockScale(Locked), 1e-4f, "Padlock back at rest after the tick.");

            // Clear 2 (s3): the shelf opens and the padlock pops off.
            yield return MouseDrag(Tile(1, 0), ShelfCentre(2));
            Assert.IsFalse(_board.GetShelf(Locked).IsLocked, "Two clears open s4.");
            Assert.IsTrue(_view.IsPadlockPopping(Locked), "Padlock pops off.");
            yield return WaitWhile(() => _view.IsPadlockPopping(Locked), "Pop finishes.");
            Assert.IsFalse(_view.IsPadlockVisible(Locked), "Padlock is gone once open.");
            Assert.IsFalse(_view.IsScrimVisible(Locked), "Scrim is gone once open.");
            Assert.IsNull(_view.GetPadlockText(Locked));

            // A move onto the now-open shelf works: idli stand triple clears, the dabba triple behind it clears: win.
            yield return TapTap(1, 1, Locked);
            Assert.IsTrue(_board.IsWon, "Moving onto the opened shelf works and wins the level.");
            yield return WaitWhile(() => _view.IsAnimating, "Clears finish.");
            Assert.AreEqual(BoardView.WonText, _view.StatusText);
        }

        [UnityTest]
        public IEnumerator Relayout_KeepsPadlockOnItsShelf()
        {
            yield return null;
            _view.Relayout(new Rect(0, 0, _camera.pixelWidth, _camera.pixelHeight * 0.8f)); // e.g. a safe-area change
            var shelf = _view.Layout.GetShelfRect(Locked);
            var position = _view.GetPadlockPixelPosition(Locked);
            Assert.That(position.x, NUnit.Framework.Is.InRange(shelf.xMin, shelf.xMax), "Padlock sits over its shelf.");
            Assert.That(position.y, NUnit.Framework.Is.InRange(shelf.center.y, shelf.yMax), "On the upper part, clear of the front row centre.");
        }

        [UnityTest]
        public IEnumerator LockTickAndPop_AllocateNothing()
        {
            // Warm up: one full run so first-call costs are out of the way.
            var warm = Load();
            _view.Bind(warm, _camera, new Rect(0, 0, _camera.pixelWidth, _camera.pixelHeight), BoardLayoutSettings.Default);
            yield return null;
            warm.TryMove(1, 2, 0);
            warm.TryMove(1, 0, 2);
            for (int i = 0; i < 60; i++) _view.AdvanceAnimations(Frame);

            var board = Load();
            _view.Bind(board, _camera, new Rect(0, 0, _camera.pixelWidth, _camera.pixelHeight), BoardLayoutSettings.Default);
            yield return null;
            Assert.IsTrue(board.TryMove(1, 2, 0)); // tick
            Assert.IsTrue(board.TryMove(1, 0, 2)); // opens: pop
            Assert.IsTrue(_view.IsPadlockPopping(Locked));

            Assert.That(() =>
            {
                for (int i = 0; i < 40; i++) _view.AdvanceAnimations(Frame);
            }, Is.Not.AllocatingGCMemory());
            Assert.IsFalse(_view.IsPadlockVisible(Locked), "The pop finished inside the measured frames.");
        }
    }
}
