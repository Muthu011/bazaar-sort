using System.Collections;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.Services;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Issue #57 through the real stack: <see cref="LevelBootstrap"/> loads the level, <see cref="TutorialDirector"/>
    /// reads its <c>tutorial</c> key, and a simulated mouse drives <see cref="BoardInput"/>. L1 lets only the shown move
    /// through until the first triple; L2 shows the hidden-layer arrow; L11 shows the locked line. Completion is saved
    /// (fake store) and skips the tutorial on a replay.
    /// </summary>
    public class TutorialPlayModeTests : InputTestFixture
    {
        private GameObject _cameraObject;
        private GameObject _viewObject;
        private GameObject _tutorialObject;
        private Camera _camera;
        private BoardView _view;
        private LevelBootstrap _bootstrap;
        private TutorialDirector _tutorial;
        private ProgressService _progress;
        private TextAsset[] _levels;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();
        }

        private void Build(params int[] levelIndices)
        {
            _cameraObject = new GameObject("Test Camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _viewObject = new GameObject("Board View");
            _view = _viewObject.AddComponent<BoardView>();
            _view.Haptics = new FakeHaptics();
            var input = _viewObject.AddComponent<BoardInput>();
            input.Configure(_view);

            _levels = new TextAsset[levelIndices.Length];
            for (int i = 0; i < levelIndices.Length; i++)
            {
                string name = $"w1_l{levelIndices[i]:000}";
                _levels[i] = new TextAsset(File.ReadAllText($"Assets/Levels/World1/{name}.json")) { name = name };
            }

            _bootstrap = _viewObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(_levels, _view, _camera);
            _progress = new ProgressService(new FakeProgressStore());
            _bootstrap.Progress = _progress;

            _tutorialObject = new GameObject("Tutorial");
            _tutorial = _tutorialObject.AddComponent<TutorialDirector>();
            _tutorial.Configure(_bootstrap, input);
        }

        public override void TearDown()
        {
            Object.Destroy(_tutorialObject);
            Object.Destroy(_viewObject);
            Object.Destroy(_cameraObject);
            if (_levels != null)
            {
                foreach (var level in _levels) Object.Destroy(level);
            }
            base.TearDown();
        }

        private Board Board => _view.Board;

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

        private IEnumerator Drag(Vector2 from, Vector2 to)
        {
            Move(_mouse.position, from);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            for (int i = 1; i <= 6; i++)
            {
                Move(_mouse.position, Vector2.Lerp(from, to, i / 6f));
                yield return null;
            }
            Release(_mouse.leftButton);
            yield return null;
        }

        // ---- L1: restricted taps ----

        [UnityTest]
        public IEnumerator L1_OnlyTheShownMoveIsTappable_UntilTheFirstTriple()
        {
            Build(1);
            yield return null; // Start: bootstrap loads w1_l001, the director starts the match3 guide
            yield return null; // BoardInput binds to the board with the gate

            Assert.IsNotNull(_tutorial.Guide, "L1 runs its tutorial on a fresh save.");
            Assert.AreEqual(TutorialKind.Match3, _tutorial.Guide.Kind);
            Assert.AreEqual(new GuidedMove(1, 1, 0), _tutorial.Guide.Move);
            Assert.IsTrue(_tutorial.IsHandVisible, "Hand pointer is shown.");
            Assert.AreEqual(TutorialDirector.Match3Text, _tutorial.CueText);

            // Another item can't be picked: no selection, nothing moves.
            yield return Click(Tile(2, 2));
            Assert.IsFalse(_view.IsHighlightVisible, "A tap on another item does nothing.");
            Assert.AreEqual("idli_stand", Board.GetShelf(2).GetFrontItem(2));

            // A drag of another item doesn't start.
            yield return Drag(Tile(2, 2), ShelfCentre(3));
            Assert.AreEqual("idli_stand", Board.GetShelf(2).GetFrontItem(2), "A drag of another item does nothing.");
            Assert.IsNull(Board.GetShelf(3).GetFrontItem(1));

            // The guided item selects, but a legal move elsewhere is refused and the selection stays.
            yield return Click(Tile(1, 1));
            Assert.IsTrue(_view.IsHighlightVisible, "The shown item can be picked.");
            yield return Click(ShelfCentre(3));
            Assert.IsTrue(_view.IsHighlightVisible, "A tap on the wrong shelf keeps the selection.");
            Assert.AreEqual("tumbler", Board.GetShelf(1).GetFrontItem(1), "Nothing moved.");
            Assert.IsNull(Board.GetShelf(3).GetFrontItem(1));

            // The shown move: tumbler onto the tumbler pair, which clears.
            yield return Click(ShelfCentre(0));
            Assert.IsTrue(Board.GetShelf(0).IsFrontEmpty, "The first triple cleared.");
            Assert.IsNull(_tutorial.Guide, "The tutorial ends on the first triple.");
            Assert.IsTrue(_progress.IsTutorialDone(TutorialKeys.Match3), "Completion is saved.");
            yield return null;
            Assert.IsFalse(_tutorial.IsHandVisible, "The hand is gone.");
            Assert.IsFalse(_tutorial.IsTextVisible);

            // Free play from here: any item can be picked.
            yield return Click(Tile(2, 2));
            Assert.IsTrue(_view.IsHighlightVisible, "After the first triple every item is tappable.");
        }

        [UnityTest]
        public IEnumerator L1_AfterFirstCompletion_TheTutorialIsSkipped()
        {
            Build(1);
            _progress.MarkTutorialDone(TutorialKeys.Match3);
            yield return null;
            yield return null;

            Assert.IsNull(_tutorial.Guide, "A completed tutorial is skipped.");
            Assert.IsFalse(_tutorial.IsHandVisible);
            yield return Click(Tile(2, 2));
            Assert.IsTrue(_view.IsHighlightVisible, "No restriction once the tutorial was completed.");
        }

        [UnityTest]
        public IEnumerator L1_RetryBeforeCompletion_RunsTheTutorialAgain()
        {
            Build(1);
            yield return null;
            yield return null;
            _bootstrap.Retry();
            yield return null;
            yield return null;

            Assert.IsNotNull(_tutorial.Guide);
            Assert.AreSame(Board, _tutorial.Guide.Board, "The guide follows the reloaded board.");
            yield return Click(Tile(2, 2));
            Assert.IsFalse(_view.IsHighlightVisible, "Still restricted on the reloaded board.");
        }

        // ---- L2: hidden layer arrow ----

        [UnityTest]
        public IEnumerator L2_ShowsTheArrowOverTheHiddenRow_WithoutText_UntilTheReveal()
        {
            Build(2);
            yield return null;
            yield return null;

            Assert.AreEqual(TutorialKind.Layers, _tutorial.Guide.Kind);
            Assert.IsTrue(_tutorial.IsArrowVisible, "The arrow is shown.");
            Assert.IsFalse(_tutorial.IsTextVisible, "L2 has no text.");
            Assert.IsFalse(_tutorial.IsHandVisible);
            var hidden = _view.Layout.GetHiddenSlotRect(0, 1);
            Assert.AreEqual(hidden.center.x, _tutorial.ArrowPixelPosition.x, 0.5f, "Arrow is centred on the hidden row.");
            Assert.Greater(_tutorial.ArrowPixelPosition.y, hidden.center.y, "Arrow sits above the hidden row.");

            // Nothing is restricted on L2: any item can be picked.
            yield return Click(Tile(2, 0));
            Assert.IsTrue(_view.IsHighlightVisible);
            yield return Click(ShelfCentre(3));

            // Clear s1's tumbler triple: its hidden layer is revealed and the arrow goes, once.
            yield return Click(Tile(1, 1));
            yield return Click(ShelfCentre(0));
            Assert.IsNull(_tutorial.Guide);
            Assert.IsTrue(_progress.IsTutorialDone(TutorialKeys.Layers));
            yield return null;
            Assert.IsFalse(_tutorial.IsArrowVisible);
        }

        // ---- L11: locked shelf line ----

        [UnityTest]
        public IEnumerator L11_ShowsOneShortLineOnTheLockedShelf()
        {
            Build(11);
            yield return null;
            yield return null;

            Assert.AreEqual(TutorialKind.Locked, _tutorial.Guide.Kind);
            Assert.AreEqual(5, _tutorial.Guide.FocusShelf);
            Assert.AreEqual(TutorialDirector.LockedLine(1), _tutorial.CueText);
            Assert.IsFalse(_tutorial.IsHandVisible);
            Assert.IsFalse(_tutorial.IsArrowVisible);

            // The line covers no item and not the padlock (QA round 1).
            var pill = _tutorial.PillPixelRect;
            Assert.Greater(pill.height, _view.Layout.TileSize * 0.25f, "The pill is tall enough to read.");
            for (int shelf = 0; shelf < Board.ShelfCount; shelf++)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    Assert.IsFalse(pill.Overlaps(_view.Layout.GetFrontSlotRect(shelf, slot)), $"Pill covers item ({shelf},{slot}).");
                }
            }
            float padlockLeft = _view.GetPadlockPixelPosition(5).x - 0.5f * _view.Layout.TileSize * BoardView.PadlockWidthTiles;
            Assert.LessOrEqual(pill.xMax, padlockLeft, "Pill stays left of the padlock.");
            Assert.GreaterOrEqual(pill.yMin, _view.Layout.GetFrontSlotRect(5, 0).yMax, "Pill sits above the locked shelf's items.");
        }

        [UnityTest]
        public IEnumerator L1_HandFingertipLeavesTheItemReadable()
        {
            Build(1);
            yield return null;
            yield return null;
            _tutorial.Advance(0.4f); // hand on the item
            Assert.IsTrue(_tutorial.IsHandVisible);
            var tile = _view.Layout.GetFrontSlotRect(1, 1);
            Assert.Less(_tutorial.HandTipPixelPosition.y, tile.center.y, "Fingertip below the tile centre.");
        }
    }
}
