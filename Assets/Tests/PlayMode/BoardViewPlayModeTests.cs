using System.Collections;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// The placeholder <see cref="BoardView"/> with the real level w1_l001: one tile per item, and redraws driven only
    /// by Core events. Play Mode tests in the editor run with the project root as the working directory.
    /// </summary>
    public class BoardViewPlayModeTests
    {
        private const string LevelPath = "Assets/Levels/World1/w1_l001.json";

        private GameObject _cameraObject;
        private GameObject _viewObject;
        private Camera _camera;
        private BoardView _view;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("Test Camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _viewObject = new GameObject("Board View");
            _view = _viewObject.AddComponent<BoardView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_viewObject);
            Object.Destroy(_cameraObject);
        }

        private static Board LoadLevel()
        {
            var result = LevelLoader.Load(File.ReadAllText(LevelPath), "w1_l001.json");
            Assert.IsTrue(result.Success, "w1_l001 should load.");
            return result.Board;
        }

        private Rect FullScreen => new Rect(0, 0, _camera.pixelWidth, _camera.pixelHeight);

        private static int FrontItemCount(Board board)
        {
            int count = 0;
            for (int i = 0; i < board.ShelfCount; i++) count += board.GetShelf(i).FrontItemCount;
            return count;
        }

        [UnityTest]
        public IEnumerator Bind_W1L001_ShowsOneTilePerItemWithItsLetter()
        {
            var board = LoadLevel();
            _view.Bind(board, _camera, FullScreen, BoardLayoutSettings.Default);
            yield return null;

            Assert.AreEqual(board.ShelfCount, _view.ShelfVisualCount);
            Assert.AreEqual(board.ItemCount, FrontItemCount(board), "Precondition: w1_l001 has no hidden layers.");
            Assert.AreEqual(board.ItemCount, _view.VisibleFrontTileCount, "One visible tile per item.");

            for (int shelf = 0; shelf < board.ShelfCount; shelf++)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    string item = board.GetShelf(shelf).GetFrontItem(slot);
                    string expected = item == null ? null : PlaceholderPalette.Get(item).Letter;
                    Assert.AreEqual(expected, _view.GetFrontLetter(shelf, slot), $"Shelf {shelf} slot {slot}");
                    if (item != null)
                    {
                        Assert.AreEqual(PlaceholderPalette.Get(item).Color, _view.GetFrontColor(shelf, slot), $"Shelf {shelf} slot {slot} colour");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator CoreEvents_RedrawTheBoard_ThroughToWin()
        {
            var board = LoadLevel();
            _view.Bind(board, _camera, FullScreen, BoardLayoutSettings.Default);
            yield return null;

            // Moved: idli stand from s3 slot 2 to s4 (lands in slot 1). No clear.
            Assert.IsTrue(board.TryMove(2, 2, 3));
            Assert.IsNull(_view.GetFrontLetter(2, 2), "Source slot is drawn empty after Moved.");
            Assert.AreEqual("I", _view.GetFrontLetter(3, 1), "Target slot shows the moved item after Moved.");

            // Moved + Cleared: tumbler from s2 slot 1 to s1 makes a triple, which clears.
            Assert.IsTrue(board.TryMove(1, 1, 0));
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                Assert.IsNull(_view.GetFrontLetter(0, slot), $"s1 slot {slot} is drawn empty after Cleared.");
            }
            Assert.AreEqual(6, board.ItemCount, "Precondition: 9 items minus one triple.");
            Assert.AreEqual(board.ItemCount, _view.VisibleFrontTileCount);

            // Masala dabba from s4 slot 0 to s3 clears; idli stand from s4 slot 1 to s2 clears and wins.
            Assert.IsTrue(board.TryMove(3, 0, 2));
            Assert.IsTrue(board.TryMove(3, 1, 1));
            Assert.IsTrue(board.IsWon);
            Assert.AreEqual(0, _view.VisibleFrontTileCount);
            Assert.AreEqual(BoardView.WonText, _view.StatusText, "Won event shows the win status.");
        }

        [UnityTest]
        public IEnumerator Unbind_StopsListening()
        {
            var board = LoadLevel();
            _view.Bind(board, _camera, FullScreen, BoardLayoutSettings.Default);
            yield return null;
            _view.Unbind();
            yield return null;

            Assert.IsTrue(board.TryMove(2, 2, 3), "Board still works on its own.");
            Assert.AreEqual(0, _view.ShelfVisualCount);
            Assert.AreEqual(0, _viewObject.transform.childCount, "Shelf objects are destroyed.");
        }

        [UnityTest]
        public IEnumerator LayerRevealed_ShowsTheNextLayer()
        {
            var board = new Board(
                new Shelf(new[] { "tumbler", "tumbler", null }),
                new Shelf(new[] { "tumbler", "tawa", null }, new[] { "tawa", "tawa", "idli_stand" }),
                new Shelf(new[] { "idli_stand", "idli_stand", null }));
            _view.Bind(board, _camera, FullScreen, BoardLayoutSettings.Default);
            yield return null;

            Assert.IsTrue(board.TryMove(1, 0, 0)); // tumbler triple clears on s1
            Assert.IsTrue(board.TryMove(1, 1, 0)); // s2 front empties, its hidden layer slides forward
            Assert.AreEqual("W", _view.GetFrontLetter(1, 0));
            Assert.AreEqual("W", _view.GetFrontLetter(1, 1));
            Assert.AreEqual("I", _view.GetFrontLetter(1, 2));
        }

        [UnityTest]
        public IEnumerator LevelBootstrap_LoadsLevelIntoView()
        {
            var level = new TextAsset(File.ReadAllText(LevelPath)) { name = "w1_l001" };
            var bootstrap = _viewObject.AddComponent<LevelBootstrap>();
            bootstrap.Configure(level, _view, _camera);
            yield return null; // Start runs
            yield return null;

            Assert.IsNotNull(_view.Board, "Bootstrap bound a board.");
            Assert.AreEqual(4, _view.ShelfVisualCount);
            Assert.AreEqual(_view.Board.ItemCount, _view.VisibleFrontTileCount);
            Object.Destroy(level);
        }
    }
}
