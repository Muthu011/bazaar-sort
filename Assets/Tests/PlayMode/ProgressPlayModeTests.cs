using System.Collections;
using System.IO;
using System.Linq;
using Game.Levels;
using Game.Core;
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
        private GameObject _hudObject;
        private GameHud _hud;

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

        /// <summary>
        /// Opens the "app": a fresh service over the save file, then camera, view, bootstrap and result panels; with
        /// <paramref name="withHud"/> also the HUD, wired to the panels like the game scene (Win coins fly into its pill).
        /// </summary>
        private IEnumerator Launch(bool withHud = false)
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
            if (withHud)
            {
                _hudObject = new GameObject("HUD");
                _hud = _hudObject.AddComponent<GameHud>();
                _hud.Configure(_view, _bootstrap, _camera);
            }
            _panels.Configure(_view, _bootstrap, _camera, _hud);
            yield return null; // Start: bootstrap continues, panels build
            yield return null;
        }

        /// <summary>Closes the "app": every object goes, nothing stays in memory but the file.</summary>
        private void Quit()
        {
            if (_hudObject != null) Object.DestroyImmediate(_hudObject);
            _hudObject = null;
            _hud = null;
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
            Assert.AreEqual(15, _bootstrap.LastWinCoins, "First win, 2 stars: 10 + 5.");
            yield return WaitForWinCard();
            Assert.AreEqual(2, _panels.WinStarsLit, "Two lit, one empty.");
            Assert.IsTrue(_panels.IsWinCoinsLineVisible);
            Assert.AreEqual("+15", _panels.WinCoinsLabel.text);

            _bootstrap.Retry();
            Assert.AreEqual(0, _bootstrap.MovesMade, "Retry starts the move count again.");
            WinW1L001WithDetours(3); // 9 moves: over 7.5 -> 1 star
            Assert.AreEqual(1, _bootstrap.LastWinStars);
            Assert.AreEqual(0, _bootstrap.LastWinCoins, "Replay with fewer stars than the best (2): nothing.");
            yield return WaitForWinCard();
            Assert.AreEqual(1, _panels.WinStarsLit);
            Assert.IsFalse(_panels.IsWinCoinsLineVisible, "No coin line for 0 coins.");

            _bootstrap.Retry();
            WinW1L001();
            Assert.AreEqual(3, _bootstrap.LastWinStars);
            Assert.AreEqual(5, _bootstrap.LastWinCoins, "Replay with one star above the best: 5.");

            Quit();
            yield return null;
            yield return Launch();
            Assert.AreEqual(3, _bootstrap.Progress.GetStars("w1_l001"), "Best stars, raised by the last win.");
            Assert.AreEqual(15 + 0 + 5, _bootstrap.Progress.Coins, "Same total as a 3-star first win.");
        }

        [UnityTest]
        public IEnumerator Hud_ShowsTheWallet_AndFollowsWinsAndSpends()
        {
            // w1_l001 won with 1 star (10), w1_l002 with 3 (20): 30 coins.
            var seeded = new ProgressService(new JsonFileProgressStore(_path), _ => { });
            var ids = _levels.Select(l => l.name).ToArray();
            ProgressRules.RecordWin(ids, seeded, 0, 1);
            ProgressRules.RecordWin(ids, seeded, 1, 3);
            yield return Launch();
            var hudObject = new GameObject("HUD");
            try
            {
                // This HUD isn't wired to the result panels, so nothing holds a win's coins back for a flight.
                var hud = hudObject.AddComponent<GameHud>();
                hud.Configure(_view, _bootstrap, _camera);
                yield return null; // HUD builds
                Assert.AreEqual("30", hud.CountText);

                _bootstrap.ReplayFromFirst();
                WinW1L001(); // 3 stars over a best of 1: +10
                Assert.AreEqual("40", hud.CountText, "Without the Win card's flight a win's coins show at once.");

                _bootstrap.ReplayFromFirst();
                WinW1L001(); // 3 stars again: nothing new
                Assert.AreEqual("40", hud.CountText, "A replay with no new star adds nothing.");

                Assert.IsTrue(_bootstrap.Progress.TrySpendCoins(30));
                Assert.AreEqual("10", hud.CountText, "A spend shows at once.");
                Assert.IsFalse(_bootstrap.Progress.TrySpendCoins(11));
                Assert.AreEqual("10", hud.CountText);
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
            }
        }

        // ---- Win card coins into the HUD pill (PR #63) ----

        [UnityTest]
        public IEnumerator WinCard_ShowsPlusN_AndItsCoinsFlyIntoThePill_WhichCountsUpWithoutAJump()
        {
            yield return Launch(withHud: true);
            yield return null; // HUD builds
            Assert.AreEqual("0", _hud.CountText);

            WinW1L001(); // first win, 3 stars: 20
            Assert.AreEqual(20, _bootstrap.Progress.Coins, "Saved the moment the board is won.");
            Assert.AreEqual(20, new ProgressService(new JsonFileProgressStore(_path), _ => { }).Coins, "On disk too.");
            Assert.AreEqual("0", _hud.CountText, "The pill doesn't jump before the Win card.");
            Assert.AreEqual(20, _hud.HeldCoins);

            int lowest = int.MaxValue;
            int last = 0;
            int steps = 0;
            for (float t = 0f; !_panels.IsWinVisible && t < 5f; t += Time.unscaledDeltaTime)
            {
                Assert.AreEqual(0, _hud.ShownBalance, "Still the old balance while the clears play out.");
                yield return null;
            }
            Assert.IsTrue(_panels.IsWinVisible);
            Assert.IsTrue(_panels.IsWinCoinsLineVisible);
            Assert.AreEqual("+20", _panels.WinCoinsLabel.text);
            Assert.AreEqual(WinCoinsText.For(20), _panels.WinCoinsLabel.text, "From the keyed string.");

            bool sawFlight = false;
            for (float t = 0f; (_hud.HeldCoins > 0 || _hud.CoinsInFlight > 0) && t < 5f; t += Time.unscaledDeltaTime)
            {
                if (_hud.CoinsInFlight > 0)
                {
                    sawFlight = true;
                    Assert.IsTrue(_hud.IsPillLifted, "The pill rises over the card's backdrop while coins fly in.");
                }
                int shown = _hud.ShownBalance;
                Assert.GreaterOrEqual(shown, last, "Only counts up.");
                if (shown != last) steps++;
                last = shown;
                lowest = Mathf.Min(lowest, shown);
                yield return null;
            }
            if (_hud.ShownBalance != last) steps++; // the last landing ends the loop
            Assert.IsTrue(sawFlight, "Coins flew from the Win card.");
            Assert.AreEqual(0, lowest, "Started from the old balance.");
            Assert.AreEqual(UiFeel.CoinsFlownFor(20), steps, "One step per landing coin.");
            Assert.AreEqual(20, _hud.ShownBalance);
            Assert.AreEqual("20", _hud.CountText);
            Assert.AreEqual(0, _hud.HeldCoins);

            _panels.WinNextButton.onClick.Invoke();
            Assert.IsFalse(_hud.IsPillLifted, "Back under the backdrop on the next level.");
            Assert.AreEqual("20", _hud.CountText);
        }

        [UnityTest]
        public IEnumerator WinCard_ReplayWithNoNewStar_HasNoCoinLine_AndThePillStays()
        {
            Seed(1); // w1_l001 at 3 stars: 20 coins
            yield return Launch(withHud: true);
            yield return null;
            _bootstrap.ReplayFromFirst();
            Assert.AreEqual("20", _hud.CountText);
            WinW1L001();
            Assert.AreEqual(0, _bootstrap.LastWinCoins);
            yield return WaitForWinCard();
            Assert.IsFalse(_panels.IsWinCoinsLineVisible, "0 coins: the line is hidden, not \"+0\".");
            for (int i = 0; i < 90; i++)
            {
                Assert.AreEqual(0, _hud.CoinsInFlight);
                Assert.AreEqual("20", _hud.CountText);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator WinCard_LeftMidFlight_ShowsTheWholeBalanceAtOnce()
        {
            yield return Launch(withHud: true);
            yield return null;
            WinW1L001();
            yield return WaitForWinCard();
            for (float t = 0f; _hud.CoinsInFlight == 0 && t < 5f; t += Time.unscaledDeltaTime) yield return null;
            Assert.Greater(_hud.CoinsInFlight, 0);
            _panels.WinNextButton.onClick.Invoke();
            Assert.AreEqual(0, _hud.CoinsInFlight, "Coins dropped on the level load.");
            Assert.AreEqual("20", _hud.CountText, "Nothing is lost: the full wallet shows.");
            Assert.IsFalse(_hud.IsPillLifted);
        }

        [UnityTest]
        public IEnumerator Pill_WidensForFiveDigits_AndNeverShrinksTheText()
        {
            yield return Launch(withHud: true);
            yield return null;
            var progress = _bootstrap.Progress;
            int[] balances = { 7, 20, 999, 1250, 99999, 250000 };
            int wallet = 0;
            foreach (int balance in balances)
            {
                // Spend or earn straight through the service to reach the balance (no win needed).
                if (balance > wallet) progress.RecordWin("w9_test", 1, progress.HighestUnlocked, balance - wallet);
                else Assert.IsTrue(progress.TrySpendCoins(wallet - balance));
                wallet = balance;
                Canvas.ForceUpdateCanvases();
                string text = CoinBalanceText.For(balance);
                Assert.AreEqual(text, _hud.CountText);
                Assert.AreEqual(HudLayout.CounterWidthFor(text), _hud.CounterRect.sizeDelta.x, 0.001f, text);
                var label = _hud.CountLabel;
                Assert.IsFalse(label.resizeTextForBestFit, "Never shrinks to fit.");
                Assert.AreEqual(HudLayout.CounterFontDp, label.fontSize);
                Assert.LessOrEqual(label.preferredWidth, label.rectTransform.rect.width + 0.5f, $"{text} fits its box at full size.");
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
