using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Core;
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
    /// Issue #40 HUD on portrait phones, clicked with a real pointer through the EventSystem: the settings button opens a
    /// small panel holding the Haptics toggle (moved out of the HUD row), the board ignores the pointer while it is open,
    /// Close and a level load shut it, the toggle writes the setting where it already lived (SettingsService over the
    /// key-value store), the top bar shows the level number, and every bar piece stays in the safe area clear of the board.
    /// Renders into a portrait texture with simulated notch and gesture-bar insets (the editor Game view may be anything).
    /// </summary>
    public class HudPlayModeTests : InputTestFixture
    {
        private const string W1L001 = "Assets/Levels/World1/w1_l001.json";
        private const string W1L002 = "Assets/Levels/World1/w1_l002.json";

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();
        private Camera _camera;
        private BoardView _view;
        private LevelBootstrap _bootstrap;
        private GameHud _hud;
        private FakeKeyValueStore _store;
        private SettingsService _settings;
        private RenderTexture _target;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();
            _store = new FakeKeyValueStore();
            _settings = new SettingsService(_store);
            GameServices.Install(_settings, new Haptics(new FakeHaptics(), _settings));

            _target = new RenderTexture(1080, 2400, 24);
            _assets.Add(_target);
            _camera = Make("Test Camera").AddComponent<Camera>();
            _camera.targetTexture = _target;
            var viewObject = Make("Board View");
            _view = viewObject.AddComponent<BoardView>();
            viewObject.AddComponent<BoardInput>().Configure(_view);
            var levels = new[] { Level(W1L001), Level(W1L002) };
            _bootstrap = viewObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(levels, _view, _camera);
            _hud = Make("HUD").AddComponent<GameHud>();
            _hud.Configure(_view, _bootstrap, _camera);
            Make("EventSystem").AddComponent<UnityEngine.EventSystems.EventSystem>()
                .gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        public override void TearDown()
        {
            foreach (var go in _objects) Object.Destroy(go);
            _objects.Clear();
            if (_camera != null) _camera.targetTexture = null;
            foreach (var asset in _assets)
            {
                if (asset is RenderTexture rt) rt.Release();
                Object.Destroy(asset);
            }
            _assets.Clear();
            GameServices.ResetToDefaults();
            base.TearDown();
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        private TextAsset Level(string path)
        {
            var asset = new TextAsset(File.ReadAllText(path)) { name = Path.GetFileNameWithoutExtension(path) };
            _assets.Add(asset);
            return asset;
        }

        /// <summary>Lays the board and HUD out for a phone screen of the target size with the given insets (px).</summary>
        private Rect UsePhone(int width, int height, float density, float bottomInset, float topInset)
        {
            if (_target.width != width || _target.height != height)
            {
                _camera.targetTexture = null;
                _target.Release();
                _target.width = width;
                _target.height = height;
                _camera.targetTexture = _target;
            }
            var safe = new Rect(0, bottomInset, width, height - bottomInset - topInset);
            _view.Bind(_view.Board, _camera, safe, BoardLayoutSettings.Default);
            _hud.Layout(safe, density);
            Canvas.ForceUpdateCanvases();
            return safe;
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
        public IEnumerator SettingsButton_OpensThePanel_WithTheHapticsToggle_AndCloseShutsIt()
        {
            yield return null; // Start: bootstrap loads w1_l001, HUD builds
            yield return null;
            UsePhone(1080, 2400, 3f, 63f, 110f);

            Assert.IsFalse(_hud.IsSettingsOpen, "Closed at start.");
            Assert.IsFalse(_hud.HapticsToggle.gameObject.activeInHierarchy, "The haptics toggle is no longer on the HUD row.");
            Assert.AreNotSame(_hud.Canvas, _hud.HapticsToggle.GetComponentInParent<Canvas>(), "It lives on the settings canvas.");

            yield return ClickButton(_hud.SettingsButton);
            Assert.IsTrue(_hud.IsSettingsOpen, "The settings button opens the panel.");
            Assert.IsTrue(_hud.HapticsToggle.gameObject.activeInHierarchy, "Haptics toggle shown in the panel.");
            Assert.IsTrue(_view.InputBlocked, "The board ignores the pointer under the panel.");
            Assert.AreEqual(GameHud.HapticsOnLabel, _hud.HapticsLabel);

            yield return ClickButton(_hud.HapticsToggle);
            Assert.IsFalse(_settings.HapticsEnabled, "A real click turns haptics off.");
            Assert.AreEqual(GameHud.HapticsOffLabel, _hud.HapticsLabel);
            Assert.AreEqual(0, _store.GetInt(SettingsService.HapticsKey, 1), "Saved where it always was (settings store).");
            Assert.IsTrue(_hud.IsSettingsOpen, "Toggling keeps the panel open.");

            yield return ClickButton(_hud.HapticsToggle);
            Assert.IsTrue(_settings.HapticsEnabled);
            Assert.AreEqual(1, _store.GetInt(SettingsService.HapticsKey, 0));

            yield return ClickButton(_hud.SettingsCloseButton);
            Assert.IsFalse(_hud.IsSettingsOpen, "Close shuts the panel.");
            Assert.IsFalse(_view.InputBlocked, "Board input is back.");
        }

        [UnityTest]
        public IEnumerator Settings_TapOnTheScrim_Closes()
        {
            yield return null;
            yield return null;
            var safe = UsePhone(1080, 2400, 3f, 63f, 110f);
            yield return ClickButton(_hud.SettingsButton);
            Assert.IsTrue(_hud.IsSettingsOpen);

            var card = ScreenRect(_hud.SettingsCard);
            yield return Click(new Vector2(safe.center.x, (card.yMax + safe.yMax) * 0.5f)); // above the card
            Assert.IsFalse(_hud.IsSettingsOpen, "A tap outside the card closes it.");
            Assert.IsFalse(_view.InputBlocked);
        }

        [UnityTest]
        public IEnumerator TopBar_ShowsTheLevelNumber_AndALevelLoadClosesSettings()
        {
            yield return null;
            yield return null;
            UsePhone(1080, 2400, 3f, 63f, 110f);
            Assert.AreEqual("Level 1", _hud.LevelText);

            _hud.OpenSettings();
            Assert.IsTrue(_bootstrap.Next());
            Assert.AreEqual("Level 2", _hud.LevelText);
            Assert.IsFalse(_hud.IsSettingsOpen, "A level load (dev level select, Next) closes the panel.");
            Assert.IsFalse(_view.InputBlocked);
        }

        /// <summary>The three Game view presets, with simulated insets (bottom, top in px).</summary>
        [UnityTest]
        public IEnumerator Bars_StayInTheSafeArea_ClearOfTheBoard_AndTouchSized(
            [Values(0, 1, 2)] int phone)
        {
            yield return null;
            yield return null;
            int w = phone == 2 ? 720 : 1080;
            int h = phone == 1 ? 1920 : phone == 0 ? 2400 : 1600;
            float density = phone == 2 ? 2f : 3f;
            float bottomInset = phone == 0 ? 63f : phone == 2 ? 42f : 0f;
            float topInset = phone == 0 ? 110f : phone == 2 ? 74f : 72f;
            var safe = UsePhone(w, h, density, bottomInset, topInset);
            _hud.OpenSettings();
            Canvas.ForceUpdateCanvases();

            var playfield = _view.Layout.Playfield;
            var pieces = new List<(string, RectTransform, bool)>
            {
                ("level", _hud.LevelRect, false),
                ("counter", _hud.CounterRect, false),
                ("settings", (RectTransform)_hud.SettingsButton.transform, true),
            };
            foreach (var slot in _hud.BoosterSlots) pieces.Add((slot.name, slot.rectTransform, true));
            foreach (var (name, rect, touch) in pieces)
            {
                var r = ScreenRect(rect);
                string context = $"{w}x{h}: {name} {r}";
                Assert.GreaterOrEqual(r.yMin, safe.yMin - 0.5f, $"{context} above the gesture bar");
                Assert.LessOrEqual(r.yMax, safe.yMax + 0.5f, $"{context} below the notch");
                Assert.GreaterOrEqual(r.xMin, safe.xMin - 0.5f, context);
                Assert.LessOrEqual(r.xMax, safe.xMax + 0.5f, context);
                Assert.IsFalse(r.Overlaps(playfield), $"{context} overlaps the board band {playfield}");
                if (touch)
                {
                    Assert.GreaterOrEqual(r.width / density, 47.9f, $"{context} width dp");
                    Assert.GreaterOrEqual(r.height / density, 47.9f, $"{context} height dp");
                }
            }

            foreach (var button in new[] { _hud.HapticsToggle, _hud.SettingsCloseButton })
            {
                var r = ScreenRect((RectTransform)button.transform);
                Assert.GreaterOrEqual(r.height / density, 47.9f, $"{w}x{h}: {button.name} height dp");
                Assert.IsTrue(safe.Contains(r.min) && safe.Contains(r.max), $"{w}x{h}: {button.name} {r} inside the safe area");
            }
            _hud.CloseSettings();
        }
    }
}
