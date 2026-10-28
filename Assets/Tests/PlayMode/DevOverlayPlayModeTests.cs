using System.Collections;
using System.IO;
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
    /// The dev level select (editor and development builds only): one button per World 1 level, clicked with a real
    /// pointer through the EventSystem, loads that level through <see cref="LevelBootstrap.LoadLevel"/>; the buttons
    /// sit in the top HUD reserve, clear of the board's playfield, and are at least 48 dp.
    /// </summary>
    public class DevOverlayPlayModeTests : InputTestFixture
    {
        private static readonly string[] World1 =
        {
            "Assets/Levels/World1/w1_l001.json",
            "Assets/Levels/World1/w1_l002.json",
            "Assets/Levels/World1/w1_l003.json",
            "Assets/Levels/World1/w1_l004.json",
            "Assets/Levels/World1/w1_l005.json",
        };

        private GameObject _cameraObject;
        private GameObject _viewObject;
        private GameObject _eventSystemObject;
        private GameObject _overlayObject;
        private Camera _camera;
        private BoardView _view;
        private LevelBootstrap _bootstrap;
        private DevOverlay _overlay;
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

            _levels = new TextAsset[World1.Length];
            for (int i = 0; i < World1.Length; i++)
            {
                _levels[i] = new TextAsset(File.ReadAllText(World1[i])) { name = Path.GetFileNameWithoutExtension(World1[i]) };
            }
            _bootstrap = _viewObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(_levels, _view, _camera);
            _overlayObject = new GameObject("Dev Overlay");
            _overlay = _overlayObject.AddComponent<DevOverlay>();
            _overlay.Configure(_bootstrap, _camera);
            _eventSystemObject = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        public override void TearDown()
        {
            Object.Destroy(_eventSystemObject);
            Object.Destroy(_overlayObject);
            Object.Destroy(_viewObject);
            Object.Destroy(_cameraObject);
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

        private Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(_camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(_camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest]
        public IEnumerator LevelSelect_L3Button_LoadsW1L003()
        {
            yield return null; // Start: bootstrap loads w1_l001, overlay builds
            yield return null;
            Assert.AreEqual("w1_l001", _bootstrap.CurrentLevelName);
            Assert.AreEqual(World1.Length, _overlay.LevelButtons.Length, "One button per World 1 level.");

            Canvas.ForceUpdateCanvases();
            var button = (RectTransform)_overlay.LevelButtons[2].transform;
            yield return Click(ScreenRect(button).center);
            yield return null;

            Assert.AreEqual(2, _bootstrap.CurrentIndex);
            Assert.AreEqual("w1_l003", _bootstrap.CurrentLevelName);
            Assert.IsNotNull(_view.Board, "The view shows the loaded board.");
        }

        /// <summary>
        /// Portrait phones (the app is portrait only; the editor Game view may be landscape, so render to a texture):
        /// 1080x1920 full screen and 1080x2400 with a 110 px cutout and 60 px gesture bar, both at 3 px per dp.
        /// </summary>
        [UnityTest]
        public IEnumerator LevelSelect_ButtonsAreTouchSized_AndClearOfThePlayfield(
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
                yield return null;
                Canvas.ForceUpdateCanvases();

                var playfield = _view.Layout.Playfield;
                foreach (var button in _overlay.LevelButtons)
                {
                    var rect = ScreenRect((RectTransform)button.transform);
                    Assert.GreaterOrEqual(rect.width / density, 47.9f, $"{button.name} width in dp");
                    Assert.GreaterOrEqual(rect.height / density, 47.9f, $"{button.name} height in dp");
                    Assert.IsFalse(rect.Overlaps(playfield), $"{button.name} {rect} overlaps the playfield {playfield}.");
                    Assert.GreaterOrEqual(rect.yMin, playfield.yMax - 0.5f, $"{button.name} is above the playfield.");
                    Assert.LessOrEqual(rect.yMax, safe.yMax + 0.5f, $"{button.name} is inside the safe area.");
                    Assert.GreaterOrEqual(rect.xMin, safe.xMin - 0.5f, $"{button.name} left edge in the safe area.");
                    Assert.LessOrEqual(rect.xMax, safe.xMax + 0.5f, $"{button.name} right edge in the safe area.");
                }
            }
            finally
            {
                _camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
