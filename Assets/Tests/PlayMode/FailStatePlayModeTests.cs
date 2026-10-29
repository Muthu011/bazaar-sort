using System.Collections;
using System.IO;
using Game.Core;
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
    /// Fail state end to end (issue #41): a designed fail played through real pointer input shows "Out of space!" with
    /// Retry and a hidden, unwired rewarded-ad Continue; the fail costs nothing (no save, no unlock change, same level),
    /// and Retry restores the start state. A stuck board that a booster could rescue (the #42 seam) shows no fail.
    /// </summary>
    public class FailStatePlayModeTests : InputTestFixture
    {
        private const string FirstLevelPath = "Assets/Levels/World1/w1_l001.json";

        /// <summary>
        /// Designed fail: moving s2's idli stand onto s1 fills s1 and reveals a full row on s2, so no front slot is free
        /// anywhere with items left. Valid level data (every id three times, one empty slot at start).
        /// </summary>
        private const string FailLevelJson = @"{
  ""schema"": 1, ""id"": ""test_fail"", ""world"": 1, ""index"": 98, ""role"": ""easy"", ""tutorial"": null,
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

        private sealed class AlwaysRescue : IBoosterRescue
        {
            public bool CanRescue(Board board) => true;
        }

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

        /// <summary>w1_l001 then the designed fail level; bootstrap and panels start on the next frame.</summary>
        private void Wire(IProgressService progress)
        {
            _levels = new[]
            {
                new TextAsset(File.ReadAllText(FirstLevelPath)) { name = "w1_l001" },
                new TextAsset(FailLevelJson) { name = "test_fail" },
            };
            _bootstrap = _viewObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(_levels, _view, _camera, 1);
            _bootstrap.Progress = progress;
            _panelsObject = new GameObject("Result Panels");
            _panels = _panelsObject.AddComponent<ResultPanels>();
            _panels.Configure(_view, _bootstrap, _camera);
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

        private IEnumerator WaitFrames(float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ---- Tests ----

        [UnityTest]
        public IEnumerator DesignedFail_ShowsOutOfSpace_WithRetry_AndAHiddenContinue_AtNoCost()
        {
            // The player has already won w1_l001, so the game continues at the fail level.
            var store = new FakeProgressStore();
            var progress = new ProgressService(store, _ => Assert.Fail("No warning expected."));
            ProgressRules.RecordWin(new[] { "w1_l001", "test_fail" }, progress, 0, ProgressRules.MaxStars);
            int savesBefore = store.SaveCount;
            string savedBefore = store.Json;
            string unlockedBefore = progress.HighestUnlocked;
            int starsBefore = progress.GetStars("w1_l001");

            Wire(progress);
            yield return null; // Start: bootstrap continues at test_fail, panels build
            yield return null;
            Assert.AreEqual("test_fail", _bootstrap.CurrentLevelName, "Precondition: the game continues at the fail level.");
            int failed = 0;
            _view.Board.Failed += () => failed++;

            yield return TapTap(1, 0, 0);
            Assert.AreEqual(1, failed, "Core raised Failed once for the losing move.");
            Assert.IsTrue(_view.Board.IsFailed);

            float waited = 0f;
            while (_view.Outcome == BoardOutcome.None && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
            Assert.AreEqual(BoardOutcome.Failed, _view.Outcome);
            Assert.IsTrue(_panels.IsFailVisible, "Fail panel shows once the animations finish.");
            Assert.IsFalse(_panels.IsWinVisible);
            Assert.IsFalse(_panels.IsMoreLevelsVisible);
            Assert.AreEqual("Out of space!", _panels.FailTitleLabel.text);

            // Retry is on screen; the rewarded-ad Continue exists but is hidden and wired to nothing (Slice 6).
            Assert.IsTrue(_panels.FailRetryButton.gameObject.activeInHierarchy, "Retry shows.");
            Assert.AreEqual("Retry", _panels.FailRetryButton.GetComponentInChildren<Text>(true).text);
            Assert.IsNotNull(_panels.FailContinueButton, "The Continue button exists.");
            Assert.AreEqual("Free Extra Slot", _panels.FailContinueButton.GetComponentInChildren<Text>(true).text);
            Assert.IsFalse(_panels.FailContinueButton.gameObject.activeSelf, "Continue is hidden for now.");
            Assert.IsFalse(_panels.IsContinueVisible);
            Assert.AreEqual(0, _panels.FailContinueButton.onClick.GetPersistentEventCount(), "Continue has no persistent wiring.");
            int activeButtons = 0;
            foreach (var button in _panelsObject.GetComponentsInChildren<Button>(false)) activeButtons++;
            Assert.AreEqual(1, activeButtons, "Only Retry can be tapped.");

            // Board input is locked under the panel.
            yield return Click(Tile(0, 0));
            Assert.AreEqual(-1, _view.SelectedShelf, "No selection through the panel.");

            // No penalty after a fail: nothing saved, nothing locked again, still the same level.
            Assert.AreEqual(savesBefore, store.SaveCount, "A fail writes no save.");
            Assert.AreEqual(savedBefore, store.Json, "The save is unchanged.");
            Assert.AreEqual(unlockedBefore, progress.HighestUnlocked, "The unlock is unchanged.");
            Assert.AreEqual(starsBefore, progress.GetStars("w1_l001"), "Earned stars are kept.");
            Assert.AreEqual(0, progress.GetStars("test_fail"));

            yield return ClickButton(_panels.FailRetryButton);
            Assert.IsFalse(_panels.IsFailVisible, "Panel hides on Retry.");
            Assert.AreEqual("test_fail", _bootstrap.CurrentLevelName, "Retry replays the same level.");
            Assert.AreEqual(savesBefore, store.SaveCount, "Retry writes no save either.");
            Assert.AreEqual(unlockedBefore, progress.HighestUnlocked);
            Assert.AreEqual(BoardOutcome.None, _view.Outcome);
            Assert.IsFalse(_view.InputLocked, "Board input is live again.");
            Assert.IsFalse(_view.Board.IsStuck, "Back to the start state.");
            var expected = LevelLoader.Load(FailLevelJson, "test_fail").Board;
            for (int i = 0; i < expected.ShelfCount; i++)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    Assert.AreEqual(expected.GetShelf(i).GetFrontItem(slot), _view.Board.GetShelf(i).GetFrontItem(slot),
                        $"Shelf {i} slot {slot} as at start");
                }
            }

            yield return Click(Tile(1, 0));
            Assert.AreEqual(1, _view.SelectedShelf, "Input works after Retry.");
        }

        [UnityTest]
        public IEnumerator FailCard_WithContinueShown_PutsFreeExtraSlotOnTop_AsTheMainButton_AndRetrySecondary()
        {
            const int width = 1080;
            const int height = 1920;
            const float density = 3f;
            var target = new RenderTexture(width, height, 24);
            _camera.targetTexture = target;
            var safe = new Rect(0, 0, width, height);

            _panelsObject = new GameObject("Result Panels");
            _panels = _panelsObject.AddComponent<ResultPanels>();
            _panels.Build(_camera, false);
            _panels.Layout(safe, density);
            _panels.Show(ResultPanel.Fail);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var retry = (RectTransform)_panels.FailRetryButton.transform;
            var cont = (RectTransform)_panels.FailContinueButton.transform;
            var retryImage = (Image)_panels.FailRetryButton.targetGraphic;
            var contImage = (Image)_panels.FailContinueButton.targetGraphic;

            // Hidden (the game today): Retry alone, main style, straight under the title.
            Assert.IsFalse(_panels.IsContinueVisible);
            Color mainColor = retryImage.color;
            float retryAloneY = retry.anchoredPosition.y;
            float aloneCardHeight = _panels.FailCard.sizeDelta.y;
            Assert.AreEqual(ResultPanelLayout.ButtonHeightDp, retry.sizeDelta.y, 0.01f);

            _panels.ShowContinueForPreview(true);
            Canvas.ForceUpdateCanvases();
            Assert.IsTrue(_panels.IsContinueVisible);

            // Order: Continue above Retry, and larger.
            Assert.Greater(cont.anchoredPosition.y, retry.anchoredPosition.y, "Free Extra Slot sits above Retry.");
            Assert.Greater(cont.sizeDelta.y, retry.sizeDelta.y, "Free Extra Slot is the larger button.");
            Assert.AreEqual(ResultPanelLayout.LeadButtonHeightDp, cont.sizeDelta.y, 0.01f);
            Assert.Greater(_panels.FailCard.sizeDelta.y, aloneCardHeight, "The card grows to fit both.");

            // Label and video icon.
            var label = _panels.FailContinueButton.GetComponentInChildren<Text>(true);
            Assert.AreEqual("Free Extra Slot", label.text);
            var icon = _panels.FailVideoIcon;
            Assert.IsNotNull(icon, "Video icon exists.");
            Assert.IsTrue(icon.gameObject.activeInHierarchy, "Video icon shows with the button.");
            Assert.AreSame(cont, icon.parent, "Icon sits on the Free Extra Slot button.");
            Assert.IsNotNull(icon.GetComponentInChildren<Image>(), "Icon is drawn.");
            Assert.AreEqual(2, icon.GetComponentsInChildren<Image>().Length, "Rounded frame plus play triangle.");
            float textLeft = label.rectTransform.anchoredPosition.x - label.preferredWidth * 0.5f;
            Assert.LessOrEqual(icon.anchoredPosition.x + ResultPanels.VideoIconWidthDp * 0.5f, textLeft + 0.5f,
                "Icon sits left of the text, beside it.");

            // Styles: Continue takes the main style; Retry turns secondary.
            Assert.AreEqual(mainColor, contImage.color, "Free Extra Slot is the main (primary) button.");
            Assert.AreNotEqual(mainColor, retryImage.color, "Retry is the secondary style.");
            Assert.AreEqual(new Color32(0x00, 0x83, 0x8F, 0xFF), (Color32)retryImage.color, "Retry uses the secondary teal.");

            // Both stay inside the safe area with touch-size buttons.
            foreach (var rect in new[] { _panels.FailCard, cont, retry })
            {
                var r = ScreenRect(rect);
                Assert.GreaterOrEqual(r.yMin, safe.yMin - 0.5f, rect.name);
                Assert.LessOrEqual(r.yMax, safe.yMax + 0.5f, rect.name);
                Assert.GreaterOrEqual(r.height / density, 48f - 0.01f, rect.name);
            }

            // Hidden again: back to Retry alone in the main style, same place as before.
            _panels.ShowContinueForPreview(false);
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(mainColor, retryImage.color);
            Assert.AreEqual(retryAloneY, retry.anchoredPosition.y, 0.01f);
            Assert.AreEqual(aloneCardHeight, _panels.FailCard.sizeDelta.y, 0.01f);

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

        [UnityTest]
        public IEnumerator StuckBoard_ThatABoosterCanRescue_ShowsNoFailPanel()
        {
            Wire(null);
            yield return null;
            yield return null;
            Assert.AreEqual("test_fail", _bootstrap.CurrentLevelName);
            _view.Board.Rescue = new AlwaysRescue(); // stand-in for issue #42's boosters
            int failed = 0;
            _view.Board.Failed += () => failed++;

            yield return TapTap(1, 0, 0);
            Assert.IsTrue(_view.Board.IsStuck, "Precondition: no legal move left.");
            yield return WaitFrames(1f);

            Assert.AreEqual(0, failed, "Not lost while a booster can help.");
            Assert.AreEqual(BoardOutcome.None, _view.Outcome);
            Assert.IsFalse(_panels.IsFailVisible, "No fail screen.");
        }
    }
}
