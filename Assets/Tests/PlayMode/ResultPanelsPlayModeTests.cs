using System.Collections;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.UI;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Win and Stuck panels end to end: real pointer input through <see cref="BoardInput"/> wins or sticks the board,
    /// the panel shows once the animations end, and its buttons (clicked through the EventSystem with the Input System
    /// UI module) Retry or load the Next level through <see cref="LevelBootstrap"/>.
    /// </summary>
    public class ResultPanelsPlayModeTests : InputTestFixture
    {
        private static readonly string[] World1 =
        {
            "Assets/Levels/World1/w1_l001.json",
            "Assets/Levels/World1/w1_l002.json",
            "Assets/Levels/World1/w1_l003.json",
            "Assets/Levels/World1/w1_l004.json",
            "Assets/Levels/World1/w1_l005.json",
        };

        /// <summary>
        /// Moving s2's idli stand onto s1 fills s1 and reveals a full row on s2: no free front slot anywhere, items
        /// left, so the board is stuck. Valid level data (every id three times, one empty slot at start).
        /// </summary>
        private const string StuckLevelJson = @"{
  ""schema"": 1, ""id"": ""test_stuck"", ""world"": 1, ""index"": 99, ""role"": ""easy"", ""tutorial"": null,
  ""moveLimit"": null, ""timeLimitSec"": null,
  ""shelves"": [
    { ""id"": ""s1"", ""kind"": ""normal"", ""lockClears"": 0, ""layers"": [[""tumbler"", ""tawa"", null]] },
    { ""id"": ""s2"", ""kind"": ""normal"", ""lockClears"": 0, ""layers"": [[""idli_stand"", null, null], [""masala_dabba"", ""tawa"", ""tumbler""]] },
    { ""id"": ""s3"", ""kind"": ""normal"", ""lockClears"": 0, ""layers"": [[""masala_dabba"", ""masala_dabba"", ""idli_stand""]] },
    { ""id"": ""s4"", ""kind"": ""normal"", ""lockClears"": 0, ""layers"": [[""tumbler"", ""tawa"", ""idli_stand""]] }
  ],
  ""trays"": [], ""wrapped"": []
}";

        private GameObject _cameraObject;
        private GameObject _viewObject;
        private GameObject _panelsObject;
        private Camera _camera;
        private BoardView _view;
        private LevelBootstrap _bootstrap;
        private ResultPanels _panels;
        private TextAsset[] _levels;
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
        }

        public override void TearDown()
        {
            Object.Destroy(_panelsObject);
            Object.Destroy(_viewObject);
            Object.Destroy(_cameraObject);
            if (_levels != null)
            {
                foreach (var level in _levels) Object.Destroy(level);
            }
            base.TearDown();
        }

        /// <summary>Wires bootstrap and panels; their Start runs on the next frame.</summary>
        private void Wire(TextAsset[] levels, int startIndex = 0)
        {
            _levels = levels;
            _bootstrap = _viewObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(levels, _view, _camera, startIndex);
            _panelsObject = new GameObject("Result Panels");
            _panels = _panelsObject.AddComponent<ResultPanels>();
            _panels.Configure(_view, _bootstrap, _camera);
        }

        private static TextAsset[] LoadWorld1()
        {
            var levels = new TextAsset[World1.Length];
            for (int i = 0; i < World1.Length; i++)
            {
                levels[i] = new TextAsset(File.ReadAllText(World1[i])) { name = Path.GetFileNameWithoutExtension(World1[i]) };
            }
            return levels;
        }

        // ---- Pointer helpers ----

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

        private IEnumerator TapTap(int fromShelf, int fromSlot, int toShelf)
        {
            yield return Click(Tile(fromShelf, fromSlot));
            yield return Click(ShelfCentre(toShelf));
        }

        private IEnumerator ClickButton(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var centre = rect.TransformPoint(rect.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_camera, centre);
            yield return Click(screen);
            yield return null;
        }

        private IEnumerator WaitForOutcome()
        {
            float waited = 0f;
            while (_view.Outcome == BoardOutcome.None && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreNotEqual(BoardOutcome.None, _view.Outcome, "The board reached an outcome.");
        }

        private IEnumerator WinW1L001()
        {
            // Winning line from Assets/Levels/World1/pack-1.md: (1,1,0) (2,2,1) (3,0,2).
            yield return TapTap(1, 1, 0);
            yield return TapTap(2, 2, 1);
            yield return TapTap(3, 0, 2);
            Assert.IsTrue(_view.Board.IsWon, "w1_l001 won through input.");
            yield return WaitForOutcome();
        }

        private static void AssertSameStart(Board expected, Board actual)
        {
            Assert.AreEqual(expected.ShelfCount, actual.ShelfCount);
            Assert.AreEqual(expected.ItemCount, actual.ItemCount);
            for (int i = 0; i < expected.ShelfCount; i++)
            {
                var a = expected.GetShelf(i);
                var b = actual.GetShelf(i);
                Assert.AreEqual(a.HiddenLayerCount, b.HiddenLayerCount, $"Shelf {i} layers");
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    Assert.AreEqual(a.GetFrontItem(slot), b.GetFrontItem(slot), $"Shelf {i} slot {slot}");
                }
            }
        }

        // ---- Win ----

        [UnityTest]
        public IEnumerator WinByInput_ShowsWinPanel_ThenNextLoadsW1L002()
        {
            Wire(LoadWorld1());
            yield return null; // Start: bootstrap loads w1_l001, panels build
            yield return null;
            Assert.AreEqual("w1_l001", _bootstrap.CurrentLevelName);
            Assert.IsFalse(_panels.IsWinVisible, "No panel while playing.");

            yield return WinW1L001();
            yield return null;
            Assert.AreEqual(BoardOutcome.Won, _view.Outcome);
            Assert.IsTrue(_panels.IsWinVisible, "Win panel shows after the last clear.");
            Assert.IsFalse(_panels.IsStuckVisible);

            yield return ClickButton(_panels.WinNextButton);
            Assert.AreEqual(1, _bootstrap.CurrentIndex, "Next loads the next level.");
            Assert.AreEqual("w1_l002", _bootstrap.CurrentLevelName);
            Assert.IsFalse(_panels.IsWinVisible, "Panel hides on load.");
            Assert.IsFalse(_view.InputLocked, "Board input is live again.");

            var expected = LevelLoader.Load(File.ReadAllText(World1[1]), "w1_l002").Board;
            AssertSameStart(expected, _view.Board);
            Assert.AreEqual(expected.ItemCount - CountHidden(expected), _view.VisibleFrontTileCount);
        }

        [UnityTest]
        public IEnumerator WinPanel_Retry_RestoresTheStartState()
        {
            Wire(LoadWorld1());
            yield return null;
            yield return null;

            yield return WinW1L001();
            yield return null;
            Assert.IsTrue(_panels.IsWinVisible);

            yield return ClickButton(_panels.WinRetryButton);
            Assert.AreEqual(0, _bootstrap.CurrentIndex, "Retry stays on w1_l001.");
            Assert.IsFalse(_panels.IsWinVisible);
            var expected = LevelLoader.Load(File.ReadAllText(World1[0]), "w1_l001").Board;
            AssertSameStart(expected, _view.Board);
            Assert.AreEqual(9, _view.VisibleFrontTileCount, "All nine tiles drawn again.");

            // And it plays: a tap selects.
            yield return Click(Tile(1, 1));
            Assert.AreEqual(1, _view.SelectedShelf, "Input works after Retry.");
        }

        [UnityTest]
        public IEnumerator Next_AfterTheLastLevel_LoopsToTheFirst()
        {
            Wire(LoadWorld1(), 4);
            yield return null;
            yield return null;
            Assert.AreEqual("w1_l005", _bootstrap.CurrentLevelName);

            _panels.Show(BoardOutcome.Won); // winning L5 by input is 18 moves; the button path is what is under test
            yield return ClickButton(_panels.WinNextButton);
            Assert.AreEqual("w1_l001", _bootstrap.CurrentLevelName, "Placeholder: after L5, Next loops to L1.");
        }

        // ---- Stuck ----

        [UnityTest]
        public IEnumerator StuckByInput_ShowsStuckPanel_ThenRetryRestoresStart()
        {
            var level = new TextAsset(StuckLevelJson) { name = "test_stuck" };
            Wire(new[] { level });
            yield return null;
            yield return null;
            Assert.IsNotNull(_view.Board, "Stuck test level loads (passes validation).");

            yield return TapTap(1, 0, 0);
            Assert.IsTrue(_view.Board.IsStuck, "Precondition: the move leaves no legal move.");
            yield return WaitForOutcome();
            yield return null;
            Assert.AreEqual(BoardOutcome.Stuck, _view.Outcome);
            Assert.IsTrue(_panels.IsStuckVisible, "Stuck panel shows.");
            Assert.IsFalse(_panels.IsWinVisible);

            // Board input is locked under the panel: a tap on a tile does not select it.
            yield return Click(Tile(0, 0));
            Assert.AreEqual(-1, _view.SelectedShelf, "No selection through the panel.");

            yield return ClickButton(_panels.StuckRetryButton);
            Assert.IsFalse(_panels.IsStuckVisible, "Panel hides on Retry.");
            var expected = LevelLoader.Load(StuckLevelJson, "test_stuck").Board;
            AssertSameStart(expected, _view.Board);
            Assert.IsFalse(_view.Board.IsStuck);

            yield return Click(Tile(1, 0));
            Assert.AreEqual(1, _view.SelectedShelf, "Input works after Retry.");
        }

        // ---- Layout ----

        [UnityTest]
        public IEnumerator Panels_StayInsideTheSafeArea_WithButtonsOf48dp(
            [Values(1920, 2400)] int height)
        {
            const int width = 1080;
            const float density = 3f;
            var target = new RenderTexture(width, height, 24);
            _camera.targetTexture = target;
            // 20:9: camera cutout on top and a gesture bar below; 16:9: full screen.
            var safe = height == 2400 ? new Rect(0, 60, width, height - 170) : new Rect(0, 0, width, height);

            _panelsObject = new GameObject("Result Panels");
            _panels = _panelsObject.AddComponent<ResultPanels>();
            _panels.Build(_camera, false);
            _panels.Layout(safe, density);
            yield return null;

            foreach (var outcome in new[] { BoardOutcome.Won, BoardOutcome.Stuck })
            {
                _panels.Show(outcome);
                Canvas.ForceUpdateCanvases();
                var card = outcome == BoardOutcome.Won ? _panels.WinCard : _panels.StuckCard;
                AssertInside(safe, card, $"{outcome} card at {width}x{height}");

                var buttons = outcome == BoardOutcome.Won
                    ? new[] { _panels.WinNextButton, _panels.WinRetryButton }
                    : new[] { _panels.StuckRetryButton };
                foreach (var button in buttons)
                {
                    var r = ScreenRect((RectTransform)button.transform);
                    Assert.GreaterOrEqual(r.height / density, 48f - 0.01f, $"{button.name} height in dp");
                    Assert.GreaterOrEqual(r.width / density, 48f - 0.01f, $"{button.name} width in dp");
                    AssertInside(safe, (RectTransform)button.transform, button.name);
                }
            }

            _camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
        }

        private Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(_camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(_camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void AssertInside(Rect safe, RectTransform rect, string what)
        {
            var r = ScreenRect(rect);
            const float tolerance = 0.5f;
            Assert.GreaterOrEqual(r.xMin, safe.xMin - tolerance, $"{what}: left {r}");
            Assert.GreaterOrEqual(r.yMin, safe.yMin - tolerance, $"{what}: bottom {r}");
            Assert.LessOrEqual(r.xMax, safe.xMax + tolerance, $"{what}: right {r}");
            Assert.LessOrEqual(r.yMax, safe.yMax + tolerance, $"{what}: top {r}");
            Assert.Greater(r.width, 0f, $"{what}: has size");
        }

        private static int CountHidden(Board board)
        {
            int front = 0;
            for (int i = 0; i < board.ShelfCount; i++) front += board.GetShelf(i).FrontItemCount;
            return board.ItemCount - front;
        }
    }
}
