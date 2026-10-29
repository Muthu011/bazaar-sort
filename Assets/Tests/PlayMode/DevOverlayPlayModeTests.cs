using System.Collections;
using System.IO;
using System.Linq;
using Game.Levels;
using Game.Services;
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
    /// The dev level select (editor and development builds only, issue #22): a "Levels" button in the top HUD reserve
    /// opens a card with one cell per level file on disk; a cell clicked with a real pointer through the EventSystem
    /// loads that level. The board ignores the pointer while the card is open. Cells show saved stars, and Reset
    /// progress empties the save. A release build never installs the overlay.
    /// </summary>
    public class DevOverlayPlayModeTests : InputTestFixture
    {
        private GameObject _cameraObject;
        private GameObject _viewObject;
        private GameObject _eventSystemObject;
        private GameObject _overlayObject;
        private Camera _camera;
        private BoardView _view;
        private LevelBootstrap _bootstrap;
        private DevOverlay _overlay;
        private TextAsset[] _levels;
        private ProgressService _progress;
        private Mouse _mouse;
        private RenderTexture _phone;
        private bool _laidOut;

        /// <summary>
        /// Clicks go to a fixed portrait phone (1080x2400 at 3 px/dp, full screen) rendered into a texture, so the tests
        /// do not depend on the editor Game view's size (issue #40: with a portrait Game view preset the overlay followed
        /// the Game view's screen while the clicks did not land on the cells).
        /// </summary>
        private static readonly Rect PhoneSafeArea = new Rect(0, 0, 1080, 2400);

        /// <summary>Every level file, in play order, like the generated LevelList.</summary>
        private static string[] AllLevelPaths() => LevelOrder.Sort(
            Directory.GetFiles("Assets/Levels", "*.json", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'))).ToArray();

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();

            _cameraObject = new GameObject("Test Camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _phone = new RenderTexture(1080, 2400, 24);
            _camera.targetTexture = _phone;
            _laidOut = false;
            _viewObject = new GameObject("Board View");
            _view = _viewObject.AddComponent<BoardView>();
            _viewObject.AddComponent<BoardInput>().Configure(_view);

            var paths = AllLevelPaths();
            _levels = new TextAsset[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                _levels[i] = new TextAsset(File.ReadAllText(paths[i])) { name = Path.GetFileNameWithoutExtension(paths[i]) };
            }
            _progress = new ProgressService(new FakeProgressStore(), _ => { });
            _bootstrap = _viewObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(_levels, _view, _camera);
            _bootstrap.Progress = _progress;
            _overlayObject = new GameObject("Dev Overlay");
            _overlay = _overlayObject.AddComponent<DevOverlay>();
            _overlay.Configure(_bootstrap, _camera);
            _eventSystemObject = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        public override void TearDown()
        {
            DevOverlay.Uninstall();
            Object.Destroy(_eventSystemObject);
            Object.Destroy(_overlayObject);
            Object.Destroy(_viewObject);
            Object.Destroy(_cameraObject);
            _phone.Release();
            Object.Destroy(_phone);
            foreach (var level in _levels) Object.Destroy(level);
            base.TearDown();
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

        private IEnumerator ClickButton(Button button)
        {
            if (!_laidOut && _camera.targetTexture == _phone)
            {
                // Once built (after Start): pin the board and overlay to the phone instead of following Screen.
                _view.Bind(_view.Board, _camera, PhoneSafeArea, BoardLayoutSettings.Default);
                _overlay.Layout(PhoneSafeArea, 3f);
                _laidOut = true;
            }
            Canvas.ForceUpdateCanvases();
            yield return Click(ScreenRect((RectTransform)button.transform).center);
            yield return null;
        }

        private Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(_camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(_camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest]
        public IEnumerator LevelSelect_HasACellForEveryLevelFile()
        {
            yield return null;
            yield return null;
            Assert.GreaterOrEqual(_levels.Length, 14, "World 1 on main has 14 levels (15 once w1_l015 lands).");
            Assert.AreEqual(_levels.Length, _overlay.LevelButtons.Length, "One cell per level file.");
            Assert.IsFalse(_overlay.IsOpen, "Closed until the Levels button is tapped.");
        }

        [UnityTest]
        public IEnumerator LevelSelect_OpenThenL3Cell_LoadsW1L003_AndCloses()
        {
            yield return null; // Start: bootstrap loads w1_l001 (fresh save), overlay builds
            yield return null;
            Assert.AreEqual("w1_l001", _bootstrap.CurrentLevelName);

            yield return ClickButton(_overlay.ToggleButton);
            Assert.IsTrue(_overlay.IsOpen);
            Assert.IsTrue(_view.InputLocked, "The board ignores the pointer under the card.");

            yield return ClickButton(_overlay.LevelButtons[2]);
            Assert.AreEqual(2, _bootstrap.CurrentIndex);
            Assert.AreEqual("w1_l003", _bootstrap.CurrentLevelName);
            Assert.IsNotNull(_view.Board, "The view shows the loaded board.");
            Assert.IsFalse(_overlay.IsOpen);
            Assert.IsFalse(_view.InputLocked, "Board input is back after the card closes.");
        }

        [UnityTest]
        public IEnumerator LevelSelect_LastCell_LoadsTheLastLevel_EvenWhenLocked()
        {
            yield return null;
            yield return null;
            yield return ClickButton(_overlay.ToggleButton);
            int last = _levels.Length - 1;
            Assert.AreEqual(DevLevelCellState.Locked, _overlay.CellState(last));
            yield return ClickButton(_overlay.LevelButtons[last]);
            Assert.AreEqual(last, _bootstrap.CurrentIndex);
        }

        [UnityTest]
        public IEnumerator LevelSelect_Close_ReturnsTheBoard()
        {
            yield return null;
            yield return null;
            yield return ClickButton(_overlay.ToggleButton);
            yield return ClickButton(_overlay.CloseButton);
            Assert.IsFalse(_overlay.IsOpen);
            Assert.IsFalse(_view.InputLocked);
            Assert.AreEqual("w1_l001", _bootstrap.CurrentLevelName, "Close loads nothing.");
        }

        [UnityTest]
        public IEnumerator LevelSelect_ShowsSavedStars_AndResetEmptiesTheSave()
        {
            var ids = _bootstrap.LevelIds;
            ProgressRules.RecordWin(ids, _progress, 0);
            ProgressRules.RecordWin(ids, _progress, 1);
            yield return null;
            yield return null;
            // Start may already have run before the save was seeded (continue is covered in ProgressPlayModeTests).
            Assert.IsTrue(_bootstrap.LoadLevel(2));

            yield return ClickButton(_overlay.ToggleButton);
            Assert.AreEqual(DevLevelCellState.Cleared, _overlay.CellState(0));
            Assert.AreEqual(DevLevelCellState.Cleared, _overlay.CellState(1));
            Assert.AreEqual(DevLevelCellState.Current, _overlay.CellState(2));
            Assert.AreEqual(DevLevelCellState.Locked, _overlay.CellState(3));
            Assert.AreEqual(3, _overlay.VisibleStars(0));
            Assert.AreEqual(0, _overlay.VisibleStars(2));

            yield return ClickButton(_overlay.ResetButton);
            Assert.AreEqual(0, _progress.GetStars(ids[0]));
            Assert.AreEqual(DevLevelCellState.Locked, _overlay.CellState(1));
            Assert.AreEqual(0, _overlay.VisibleStars(0));
            Assert.IsTrue(_overlay.IsOpen, "Reset keeps the card open.");
        }

        /// <summary>
        /// Portrait phones (the app is portrait only; the editor Game view may be landscape, so render to a texture):
        /// 1080x1920 full screen and 1080x2400 with a 110 px cutout and 60 px gesture bar, both at 3 px per dp.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelSelect_ToggleIsClearOfThePlayfield_AndEveryButtonIsTouchSized(
            [Values(1920, 2400)] int height)
        {
            yield return null;
            yield return null;

            const float density = 3f;
            var target = new RenderTexture(1080, height, 24);
            try
            {
                _camera.targetTexture = target;
                var safe = height >= 2400 ? new Rect(0, 60, 1080, height - 170) : new Rect(0, 0, 1080, height);
                _view.Bind(_view.Board, _camera, safe, BoardLayoutSettings.Default);
                _overlay.Layout(safe, density);
                _overlay.Open();
                yield return null;
                Canvas.ForceUpdateCanvases();

                var playfield = _view.Layout.Playfield;
                var toggle = ScreenRect((RectTransform)_overlay.ToggleButton.transform);
                Assert.IsFalse(toggle.Overlaps(playfield), $"Toggle {toggle} overlaps the playfield {playfield}.");
                Assert.GreaterOrEqual(toggle.yMin, playfield.yMax - 0.5f, "Toggle is above the playfield.");

                var buttons = _overlay.LevelButtons.Concat(new[] { _overlay.ToggleButton, _overlay.ResetButton, _overlay.CloseButton });
                foreach (var button in buttons)
                {
                    var rect = ScreenRect((RectTransform)button.transform);
                    Assert.GreaterOrEqual(rect.width / density, 47.9f, $"{button.name} width in dp");
                    Assert.GreaterOrEqual(rect.height / density, 47.9f, $"{button.name} height in dp");
                    Assert.LessOrEqual(rect.yMax, safe.yMax + 0.5f, $"{button.name} is inside the safe area.");
                    Assert.GreaterOrEqual(rect.yMin, safe.yMin - 0.5f, $"{button.name} is inside the safe area.");
                    Assert.GreaterOrEqual(rect.xMin, safe.xMin - 0.5f, $"{button.name} left edge in the safe area.");
                    Assert.LessOrEqual(rect.xMax, safe.xMax + 0.5f, $"{button.name} right edge in the safe area.");
                }
            }
            finally
            {
                _overlay.Close();
                _camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
            }
        }

        // ---- Fps and frame-time readout (issue #40: development builds only, right of the top bar) ----

        [UnityTest]
        public IEnumerator FpsReadout_IsBuiltInDevelopmentBuilds_AtTheRightOfTheTopBar()
        {
            yield return null;
            yield return null;
            Assert.IsTrue(_overlay.FpsEnabled, "The editor counts as a development build.");
            Assert.IsNotNull(_overlay.FpsText, "Fps line built.");
            Assert.IsNotNull(_overlay.WorstFrameText, "Frame-time line built.");

            const float density = 3f;
            var target = new RenderTexture(1080, 2400, 24);
            try
            {
                _camera.targetTexture = target;
                var safe = new Rect(0, 63, 1080, 2400 - 63 - 110);
                _view.Bind(_view.Board, _camera, safe, BoardLayoutSettings.Default);
                _overlay.Layout(safe, density);
                Canvas.ForceUpdateCanvases();
                var playfield = _view.Layout.Playfield;
                foreach (var label in new[] { _overlay.FpsText, _overlay.WorstFrameText })
                {
                    var rect = ScreenRect(label.rectTransform);
                    Assert.GreaterOrEqual(rect.yMin, playfield.yMax - 0.5f, $"{label.name} {rect} is in the top bar.");
                    Assert.LessOrEqual(rect.yMax, safe.yMax + 0.5f, $"{label.name} is below the notch.");
                    Assert.Greater(rect.center.x, safe.center.x, $"{label.name} is on the right.");
                }
            }
            finally
            {
                _camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator FpsReadout_IsNotBuilt_WhenTheGuardSaysRelease()
        {
            yield return null;
            Object.DestroyImmediate(_overlayObject);
            _overlayObject = new GameObject("Dev Overlay (fps off)");
            var overlay = _overlayObject.AddComponent<DevOverlay>();
            overlay.FpsEnabled = DevBuild.IsFpsReadoutEnabled(false);
            overlay.Configure(_bootstrap, _camera);
            overlay.Build();
            Assert.IsFalse(overlay.FpsEnabled);
            Assert.IsNull(overlay.FpsText, "No fps text when the guard is off.");
            Assert.IsNull(overlay.WorstFrameText, "No frame-time text when the guard is off.");
            yield return null; // Update runs without the readout
            Assert.IsNull(GameObject.Find("Dev Fps"), "No fps object in the scene.");
        }

        // ---- Release gating ----

        [UnityTest]
        public IEnumerator ReleaseBuild_NeverInstallsTheOverlay()
        {
            yield return null;
            Object.DestroyImmediate(_overlayObject);
            _cameraObject.tag = "MainCamera";

            Assert.IsNull(DevOverlay.TryInstall(false), "Release: nothing is made.");
            Assert.IsNull(Object.FindAnyObjectByType<DevOverlay>(), "No overlay in the scene.");
            Assert.IsNull(GameObject.Find("Dev Fps"), "Release: no fps or frame-time text anywhere.");
            Assert.IsNull(GameObject.Find("Dev Worst Frame"));

            var installed = DevOverlay.TryInstall(true);
            Assert.IsNotNull(installed, "Development build: the overlay is added for the scene's bootstrap.");
            _overlayObject = installed.gameObject;
        }
    }
}
