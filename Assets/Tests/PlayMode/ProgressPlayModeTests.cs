using System.Collections;
using System.IO;
using System.Linq;
using Game.Levels;
using Game.Services;
using Game.UI;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Progress end to end (issue #22), with the real <see cref="JsonFileProgressStore"/> in a temp folder (never the
    /// player's save): a win is on disk the moment Core raises Won; a simulated restart (every game object destroyed,
    /// a new service reading the same file) continues at the next unplayed level; a save with every level won opens on
    /// "More levels coming soon", whose Replay goes to w1_l001.
    /// </summary>
    public class ProgressPlayModeTests
    {
        private string _dir;
        private string _path;
        private TextAsset[] _levels;
        private GameObject _cameraObject;
        private GameObject _gameObject;
        private GameObject _panelsObject;
        private Camera _camera;
        private BoardView _view;
        private LevelBootstrap _bootstrap;
        private ResultPanels _panels;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "bazaar-sort-progress-play-" + System.Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_dir, JsonFileProgressStore.DefaultFileName);
            var paths = LevelOrder.Sort(Directory.GetFiles("Assets/Levels", "*.json", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))).ToArray();
            _levels = paths.Select(p => new TextAsset(File.ReadAllText(p)) { name = Path.GetFileNameWithoutExtension(p) }).ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            Quit();
            foreach (var level in _levels) Object.Destroy(level);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>Opens the "app": a fresh service over the save file, then camera, view, bootstrap and result panels.</summary>
        private IEnumerator Launch()
        {
            _cameraObject = new GameObject("Test Camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _gameObject = new GameObject("Board View");
            _view = _gameObject.AddComponent<BoardView>();
            _gameObject.AddComponent<BoardInput>().Configure(_view);
            _bootstrap = _gameObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(_levels, _view, _camera);
            _bootstrap.Progress = new ProgressService(new JsonFileProgressStore(_path), _ => Assert.Fail("No warning expected."));
            _panelsObject = new GameObject("Result Panels");
            _panels = _panelsObject.AddComponent<ResultPanels>();
            _panels.Configure(_view, _bootstrap, _camera);
            yield return null; // Start: bootstrap continues, panels build
            yield return null;
        }

        /// <summary>Closes the "app": every object goes, nothing stays in memory but the file.</summary>
        private void Quit()
        {
            if (_panelsObject != null) Object.DestroyImmediate(_panelsObject);
            if (_gameObject != null) Object.DestroyImmediate(_gameObject);
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
            _panelsObject = _gameObject = _cameraObject = null;
        }

        private void WinW1L001()
        {
            // Winning line from Assets/Levels/World1/pack-1.md: (1,1,0) (2,2,1) (3,0,2).
            Assert.AreEqual("w1_l001", _bootstrap.CurrentLevelName);
            var board = _view.Board;
            board.TryMove(1, 1, 0);
            board.TryMove(2, 2, 1);
            board.TryMove(3, 0, 2);
            Assert.IsTrue(board.IsWon);
        }

        /// <summary>
        /// w1_l001 (minMoves 3) won after <paramref name="detours"/> pairs of wasted moves: the masala dabba on s4 goes
        /// to s1 and straight back, which clears nothing, then the winning line.
        /// </summary>
        private void WinW1L001WithDetours(int detours)
        {
            Assert.AreEqual("w1_l001", _bootstrap.CurrentLevelName);
            var board = _view.Board;
            for (int i = 0; i < detours; i++)
            {
                Assert.IsTrue(board.TryMove(3, 0, 0), "s4 dabba onto s1");
                Assert.IsTrue(board.TryMove(0, 2, 3), "and back to s4");
            }
            Assert.IsFalse(board.IsWon);
            WinW1L001();
        }

        private IEnumerator WaitForWinCard()
        {
            for (float t = 0f; !_panels.IsWinVisible && t < 5f; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsTrue(_panels.IsWinVisible, "Win card shown.");
        }

        private void Seed(int wonCount)
        {
            var progress = new ProgressService(new JsonFileProgressStore(_path), _ => { });
            var ids = _levels.Select(l => l.name).ToArray();
            for (int i = 0; i < wonCount; i++) ProgressRules.RecordWin(ids, progress, i, ProgressRules.MaxStars);
        }

        [UnityTest]
        public IEnumerator FirstLaunch_StartsAtLevelOne()
        {
            yield return Launch();
            Assert.AreEqual(0, _bootstrap.CurrentIndex);
            Assert.IsFalse(_bootstrap.IsAwaitingReplay);
            Assert.IsFalse(File.Exists(_path), "Nothing is written before the first win.");
        }

        [UnityTest]
        public IEnumerator WinIsSavedAtOnce_AndSurvivesARestart_ContinuingAtLevelTwo()
        {
            yield return Launch();
            WinW1L001();
            Assert.IsTrue(File.Exists(_path), "Saved the moment the board is won, before the Win panel.");

            Quit(); // e.g. the player swipes the app away on the Win panel
            yield return null;
            yield return Launch();

            Assert.AreEqual(1, _bootstrap.CurrentIndex, "Continue: the next unplayed level.");
            Assert.AreEqual("w1_l002", _bootstrap.CurrentLevelName);
            Assert.AreEqual(3, _bootstrap.Progress.GetStars("w1_l001"));
            Assert.AreEqual("w1_l002", _bootstrap.Progress.HighestUnlocked);
            Assert.IsFalse(_panels.IsMoreLevelsVisible);
        }

        // ---- Stars and coins (issue #44) ----

        [UnityTest]
        public IEnumerator Win_InTheMinimum_Earns3Stars_And20Coins_SavedAndOnTheWinCard()
        {
            yield return Launch();
            WinW1L001();
            Assert.AreEqual(3, _bootstrap.MovesMade);
            Assert.AreEqual(3, _bootstrap.LastWinStars);
            Assert.AreEqual(20, _bootstrap.LastWinCoins);
            yield return WaitForWinCard();
            Assert.AreEqual(3, _panels.WinStarsLit);

            Quit();
            yield return null;
            yield return Launch();
            Assert.AreEqual(3, _bootstrap.Progress.GetStars("w1_l001"));
            Assert.AreEqual(20, _bootstrap.Progress.Coins, "Coins are saved with progress.");
        }

        [UnityTest]
        public IEnumerator Win_WithExtraMoves_EarnsFewerStars_AndBestStarsAreKept()
        {
            yield return Launch();
            WinW1L001WithDetours(2); // 7 moves: over 4.5, at most 7.5 -> 2 stars
            Assert.AreEqual(7, _bootstrap.MovesMade);
            Assert.AreEqual(2, _bootstrap.LastWinStars);
            Assert.AreEqual(15, _bootstrap.LastWinCoins);
            yield return WaitForWinCard();
            Assert.AreEqual(2, _panels.WinStarsLit, "Two lit, one empty.");

            _bootstrap.Retry();
            Assert.AreEqual(0, _bootstrap.MovesMade, "Retry starts the move count again.");
            WinW1L001WithDetours(3); // 9 moves: over 7.5 -> 1 star
            Assert.AreEqual(1, _bootstrap.LastWinStars);
            Assert.AreEqual(10, _bootstrap.LastWinCoins);
            yield return WaitForWinCard();
            Assert.AreEqual(1, _panels.WinStarsLit);

            _bootstrap.Retry();
            WinW1L001();
            Assert.AreEqual(3, _bootstrap.LastWinStars);

            Quit();
            yield return null;
            yield return Launch();
            Assert.AreEqual(3, _bootstrap.Progress.GetStars("w1_l001"), "Best stars, raised by the last win.");
            Assert.AreEqual(15 + 10 + 20, _bootstrap.Progress.Coins, "Every win, replays included, adds its coins.");
        }

        [UnityTest]
        public IEnumerator Hud_ShowsTheWallet_AndFollowsWinsAndSpends()
        {
            Seed(2); // two 3-star wins: 40 coins
            yield return Launch();
            var hudObject = new GameObject("HUD");
            try
            {
                var hud = hudObject.AddComponent<GameHud>();
                hud.Configure(_view, _bootstrap, _camera);
                yield return null; // HUD builds
                Assert.AreEqual("40", hud.CountText);

                _bootstrap.ReplayFromFirst();
                WinW1L001();
                Assert.AreEqual("60", hud.CountText, "A win's coins show at once.");

                Assert.IsTrue(_bootstrap.Progress.TrySpendCoins(50));
                Assert.AreEqual("10", hud.CountText, "A spend shows at once.");
                Assert.IsFalse(_bootstrap.Progress.TrySpendCoins(11));
                Assert.AreEqual("10", hud.CountText);
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
            }
        }

        [UnityTest]
        public IEnumerator Restart_AfterFiveWins_ContinuesAtLevelSix()
        {
            Seed(5);
            yield return Launch();
            Assert.AreEqual("w1_l006", _bootstrap.CurrentLevelName);
        }

        [UnityTest]
        public IEnumerator Restart_AfterReplayingLevelOne_StillContinuesAtTheFirstUnplayedLevel()
        {
            Seed(3);
            yield return Launch();
            Assert.AreEqual("w1_l004", _bootstrap.CurrentLevelName);
            _bootstrap.ReplayFromFirst();
            WinW1L001();
            Quit();
            yield return null;
            yield return Launch();
            Assert.AreEqual("w1_l004", _bootstrap.CurrentLevelName);
            Assert.AreEqual("w1_l004", _bootstrap.Progress.HighestUnlocked, "Replaying never lowers the unlock.");
        }

        [UnityTest]
        public IEnumerator EveryLevelWon_OpensOnMoreLevelsSoon_ThenReplayGoesToLevelOne()
        {
            Seed(_levels.Length);
            yield return Launch();

            Assert.AreEqual(_levels.Length - 1, _bootstrap.CurrentIndex, "The last level sits under the card.");
            Assert.IsTrue(_bootstrap.IsAwaitingReplay);
            Assert.IsTrue(_panels.IsMoreLevelsVisible, "More levels coming soon, with Replay.");
            Assert.IsFalse(_panels.IsWinVisible);
            Assert.IsTrue(_view.InputLocked, "The board under the card takes no moves.");

            _panels.MoreLevelsReplayButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, _bootstrap.CurrentIndex);
            Assert.IsFalse(_panels.IsMoreLevelsVisible);
            Assert.IsFalse(_bootstrap.IsAwaitingReplay);
            Assert.IsFalse(_view.InputLocked, "Level 1 plays normally.");
        }

        [UnityTest]
        public IEnumerator EveryLevelWon_ShowsMoreLevelsSoon_WhenThePanelsStartAfterTheBootstrap()
        {
            Seed(_levels.Length);
            _cameraObject = new GameObject("Test Camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _gameObject = new GameObject("Board View");
            _view = _gameObject.AddComponent<BoardView>();
            _bootstrap = _gameObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(_levels, _view, _camera);
            _bootstrap.Progress = new ProgressService(new JsonFileProgressStore(_path), _ => { });
            yield return null; // bootstrap starts alone
            Assert.IsTrue(_bootstrap.IsAwaitingReplay);

            _panelsObject = new GameObject("Result Panels");
            _panels = _panelsObject.AddComponent<ResultPanels>();
            _panels.Configure(_view, _bootstrap, _camera);
            yield return null;
            Assert.IsTrue(_panels.IsMoreLevelsVisible);
        }

        [UnityTest]
        public IEnumerator CorruptSave_StartsAtLevelOne_WithoutCrashing()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_path, "{\"version\":1,\"levels\":[{\"id\":\"w1_l0");
            var warnings = 0;
            _cameraObject = new GameObject("Test Camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _gameObject = new GameObject("Board View");
            _view = _gameObject.AddComponent<BoardView>();
            _bootstrap = _gameObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(_levels, _view, _camera);
            _bootstrap.Progress = new ProgressService(new JsonFileProgressStore(_path), _ => warnings++);
            yield return null;
            Assert.AreEqual(0, _bootstrap.CurrentIndex);
            Assert.AreEqual(1, warnings);
            WinW1L001();
            Assert.AreEqual(1, warnings, "Still one warning.");
            Assert.AreEqual(ProgressLoadStatus.Loaded, new JsonFileProgressStore(_path).Load(out _, out _), "A good save replaced it.");
        }

        [UnityTest]
        public IEnumerator WithoutProgress_StartsAtTheConfiguredLevel_AndSavesNothing()
        {
            Seed(3);
            _cameraObject = new GameObject("Test Camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _gameObject = new GameObject("Board View");
            _view = _gameObject.AddComponent<BoardView>();
            _bootstrap = _gameObject.AddComponent<LevelBootstrap>();
            _bootstrap.Configure(_levels, _view, _camera); // code-wired, progress off
            yield return null;
            Assert.IsNull(_bootstrap.Progress);
            Assert.AreEqual(0, _bootstrap.CurrentIndex);
        }
    }
}
