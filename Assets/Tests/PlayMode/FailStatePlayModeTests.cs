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
            ProgressRules.RecordWin(new[] { "w1_l001", "test_fail" }, progress, 0);
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
