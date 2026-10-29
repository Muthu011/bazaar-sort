using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DG.Tweening;
using Game.Core;
using Game.Levels;
using Game.Services;
using Game.UI;
using Game.View;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Issue #21 game feel end to end: pick-up squash and shadow, the arc move, clear burst and clear tally (no coin since PR #63),
    /// reveal bounce, Win stars and confetti, button press, haptics (through the fake device and settings), and the
    /// zero-allocation check over all of it with DOTween's own update path included.
    /// </summary>
    public class GameFeelPlayModeTests
    {
        private const float Frame = 1f / 60f;
        private const string W1L001 = "Assets/Levels/World1/w1_l001.json";
        private const string W1L002 = "Assets/Levels/World1/w1_l002.json";
        private const string W1L005 = "Assets/Levels/World1/w1_l005.json";

        private readonly List<GameObject> _objects = new List<GameObject>();
        private Camera _camera;
        private BoardView _view;
        private FakeHaptics _device;
        private FakeSettingsService _settings;

        [SetUp]
        public void SetUp()
        {
            _camera = Make("Test Camera").AddComponent<Camera>();
            _view = Make("Board View").AddComponent<BoardView>();
            _device = new FakeHaptics();
            _settings = new FakeSettingsService();
            var haptics = new Haptics(_device, _settings);
            _view.Haptics = haptics;
            GameServices.Install(_settings, haptics);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) Object.Destroy(go);
            _objects.Clear();
            GameServices.ResetToDefaults();
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        private void Bind(Board board)
        {
            _view.Bind(board, _camera, new Rect(0, 0, _camera.pixelWidth, _camera.pixelHeight), BoardLayoutSettings.Default);
        }

        private BoardEffects AddEffects()
        {
            var effects = Make("Board Effects").AddComponent<BoardEffects>();
            effects.Configure(_view);
            return effects;
        }

        private GameHud AddHud(LevelBootstrap levels = null)
        {
            var hud = Make("HUD").AddComponent<GameHud>();
            hud.Configure(_view, levels, _camera);
            return hud;
        }

        private ResultPanels AddPanels(LevelBootstrap levels = null)
        {
            var panels = Make("Result Panels").AddComponent<ResultPanels>();
            panels.Configure(_view, levels, _camera);
            return panels;
        }

        private LevelBootstrap AddBootstrap(params string[] paths)
        {
            var levels = new TextAsset[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                levels[i] = new TextAsset(File.ReadAllText(paths[i])) { name = Path.GetFileNameWithoutExtension(paths[i]) };
            }
            var bootstrap = Make("Level Bootstrap").AddComponent<LevelBootstrap>();
            bootstrap.Configure(levels, _view, _camera);
            return bootstrap;
        }

        /// <summary>s1 [T T _], s2 [T W _] behind [W W I], s3 [I I _].</summary>
        private static Board RevealBoard()
        {
            return new Board(
                new Shelf(new[] { "tumbler", "tumbler", null }),
                new Shelf(new[] { "tumbler", "tawa", null }, new[] { "tawa", "tawa", "idli_stand" }),
                new Shelf(new[] { "idli_stand", "idli_stand", null }));
        }

        /// <summary>s1 [W _ _] hiding a tumbler triple, s2 [W W _]: one move reveals, clears twice and wins.</summary>
        private static Board ChainBoard()
        {
            return new Board(
                new Shelf(new[] { "tawa", null, null }, new[] { "tumbler", "tumbler", "tumbler" }),
                new Shelf(new[] { "tawa", "tawa", null }));
        }

        /// <summary>s1 full [T W I], s2 [T W _], s3 [I I _]: anything onto s1 is refused (TargetFull).</summary>
        private static Board FullShelfBoard()
        {
            return new Board(
                new Shelf(new[] { "tumbler", "tawa", "idli_stand" }),
                new Shelf(new[] { "tumbler", "tawa", null }),
                new Shelf(new[] { "idli_stand", "idli_stand", null }, new[] { "tumbler", "tawa", "masala_dabba" }));
        }

        private static void AssertNear(Vector2 expected, Vector2 actual, string message, float tolerance = 0.01f)
        {
            Assert.Less(Vector2.Distance(expected, actual), tolerance, $"{message}: expected {expected} but was {actual}");
        }

        private IEnumerator WaitUntil(Func<bool> done, float seconds = 3f)
        {
            float waited = 0f;
            while (!done() && waited < seconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ---- Pick-up ----

        [UnityTest]
        public IEnumerator PickUp_SquashesWithAShadow_ThenSettlesLifted()
        {
            Bind(RevealBoard());
            yield return null;

            _view.ShowSelection(1, 1);
            var squash = _view.GetFrontTileScale2(1, 1);
            Assert.AreEqual(1.1f, squash.x, 0.01f, "Squashed wide on pick-up.");
            Assert.AreEqual(0.9f, squash.y, 0.01f, "...and short.");
            Assert.IsTrue(_view.IsPickShadowVisible, "Soft shadow under the item.");
            var home = _view.GetFrontTileHome(1, 1);
            Assert.Less(_view.PickShadowPixelPosition.y, home.y, "Shadow sits below the tile's rest position.");
            Assert.AreEqual(home.x, _view.PickShadowPixelPosition.x, 0.01f);

            _view.AdvanceAnimations(FeelMotion.PickUpSeconds);
            Assert.IsFalse(_view.IsPickingUp);
            Assert.AreEqual(Vector2.one, _view.GetFrontTileScale2(1, 1), "Settled at rest size.");
            Assert.Greater(_view.GetFrontTilePixelPosition(1, 1).y, home.y, "Still lifted.");

            _view.ClearSelection();
            Assert.IsFalse(_view.IsPickShadowVisible, "Shadow goes with the selection.");
            AssertNear(home, _view.GetFrontTilePixelPosition(1, 1), "Put back down");
        }

        [UnityTest]
        public IEnumerator Drag_ShadowFollowsTheTile_AndGoesOnRelease()
        {
            Bind(RevealBoard());
            yield return null;

            var home = _view.GetFrontTileHome(2, 0);
            var at = home + new Vector2(150f, 200f);
            _view.BeginDrag(2, 0, at);
            Assert.IsTrue(_view.IsPickShadowVisible);
            Assert.AreEqual(at.x, _view.PickShadowPixelPosition.x, 0.01f, "Under the dragged tile.");
            Assert.Less(_view.PickShadowPixelPosition.y, at.y);
            _view.AdvanceAnimations(FeelMotion.PickUpSeconds);
            Assert.AreEqual(BoardView.DragScale, _view.GetFrontTileScale(2, 0), 0.001f, "Drag size once settled.");

            _view.EndDrag(2, 0, DropFeedback.ReturnHome);
            Assert.IsFalse(_view.IsPickShadowVisible);
        }

        // ---- Move arc ----

        [UnityTest]
        public IEnumerator Move_FliesOnAnArcFromTheLiftedTile_AndLandsInTheSlot()
        {
            var board = RevealBoard();
            Bind(board);
            yield return null;

            _view.ShowSelection(2, 0);
            var lifted = _view.GetFrontTilePixelPosition(2, 0);
            Assert.IsTrue(board.TryMove(2, 0, 1)); // idli stand onto s2's free slot 2: no clear
            _view.ClearSelection();
            Assert.IsTrue(_view.IsShelfFlying(1));
            AssertNear(lifted, _view.GetFrontTilePixelPosition(1, 2), "Starts where the player saw it");
            Assert.AreEqual("I", _view.GetFrontLetter(1, 2));

            var to = _view.GetFrontTileHome(1, 2);
            _view.AdvanceAnimations(FeelMotion.ArcSeconds * 0.5f);
            var mid = _view.GetFrontTilePixelPosition(1, 2);
            var line = Vector2.LerpUnclamped(lifted, to, FeelMotion.ArcProgress(FeelMotion.ArcSeconds * 0.5f));
            Assert.Greater(mid.y, line.y + 10f, "Above the straight line: an arc.");
            Assert.Greater(_view.GetFrontTileScale(1, 2), 1f, "Grows a little mid-flight.");
            Assert.IsTrue(_view.IsFlightShadowVisible(1));
            Assert.Greater(_view.GetFrontSortingOrder(1, 2), _view.GetFrontSortingOrder(1, 0), "Flies over the other tiles.");

            _view.AdvanceAnimations(FeelMotion.ArcSeconds * 0.5f + 0.001f);
            Assert.IsFalse(_view.IsShelfAnimating(1), "Done in ArcSeconds.");
            AssertNear(to, _view.GetFrontTilePixelPosition(1, 2), "Lands exactly in the slot");
            Assert.AreEqual(1f, _view.GetFrontTileScale(1, 2));
            Assert.AreEqual(_view.GetFrontSortingOrder(1, 0), _view.GetFrontSortingOrder(1, 2), "Back to the front row's order.");
            Assert.IsFalse(_view.IsFlightShadowVisible(1));
            Assert.IsNull(_view.GetFrontLetter(2, 0), "Source slot is empty.");
        }

        // ---- Clear: burst and tally ----

        [UnityTest]
        public IEnumerator Clear_Bursts_CountsOnce_FliesNoCoin_AndResetsOnLoad()
        {
            // The clear tally (hud.Counter.Cleared) counts every clear at once; a clear flies no coin (PR #63). The pill
            // text is the wallet (issue #44), which clears never change: without saved progress it reads 0 throughout.
            var bootstrap = AddBootstrap(W1L001, W1L002);
            var effects = AddEffects();
            var hud = AddHud(bootstrap);
            yield return null; // Start: level loads, HUD builds
            yield return null;
            Assert.AreEqual(0, hud.Counter.Cleared);
            Assert.AreEqual("0", hud.CountText);

            var board = _view.Board;
            Assert.IsTrue(board.TryMove(1, 1, 0)); // tumbler triple on s1
            _view.AdvanceAnimations(FeelMotion.ArcSeconds + ClearRevealMotion.ClearPopSeconds + 0.001f);
            Assert.AreEqual(1, effects.BurstCount, "Burst at the top of the pop.");
            Assert.GreaterOrEqual(effects.Burst.particleCount, ParticleFx.BurstItemSparks, "Sparks emitted.");
            Assert.AreEqual(0, hud.CoinsInFlight, "A clear flies no coin (PR #63).");
            Assert.AreEqual(1, hud.Counter.Cleared, "The clear is counted at once.");
            Assert.AreEqual(1, _device.LightCount, "Light haptic on the clear.");
            Assert.AreEqual("0", hud.CountText, "A clear earns no coins: the wallet is unchanged.");
            yield return WaitUntil(() => !_view.IsAnimating);
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                Assert.IsNull(_view.GetFrontLetter(0, slot));
                Assert.AreEqual(1f, _view.GetFrontTileScale(0, slot));
                AssertNear(_view.GetFrontTileHome(0, slot), _view.GetFrontTilePixelPosition(0, slot), $"s1 slot {slot} home");
            }

            Assert.AreEqual(1, hud.Counter.Cleared, "Still one clear once it has played out.");

            bootstrap.Retry();
            Assert.AreEqual(0, hud.Counter.Cleared, "Retry resets the counter.");
            Assert.IsTrue(_view.Board.TryMove(1, 1, 0));
            _view.CompleteAnimations(); // skipped pop: counts at once
            Assert.AreEqual(1, hud.Counter.Cleared);
            Assert.AreEqual(0, hud.CoinsInFlight);
            Assert.IsTrue(bootstrap.Next());
            Assert.AreEqual(0, hud.Counter.Cleared, "Next resets the counter.");
            Assert.AreEqual("0", hud.CountText);
        }

        [UnityTest]
        public IEnumerator Clear_SkippedByTheNextMove_StillCountsOnce()
        {
            var hud = AddHud();
            Bind(RevealBoard());
            yield return null;
            yield return null;

            Assert.IsTrue(_view.Board.TryMove(1, 0, 0)); // tumblers fly to s1 and will clear
            Assert.IsTrue(_view.Board.TryMove(2, 0, 0)); // idli stand onto s1 at once: the clear snaps
            Assert.AreEqual(1, hud.Counter.Cleared, "Counted at once.");
            Assert.AreEqual(0, hud.CoinsInFlight);
            yield return WaitUntil(() => !_view.IsAnimating);
            Assert.AreEqual(1, hud.Counter.Cleared, "Never counted twice.");
        }

        // ---- Reveal ----

        [UnityTest]
        public IEnumerator Reveal_BouncesPastTheFrontSlot_ThenRestsExactlyInIt()
        {
            var board = RevealBoard();
            Bind(board);
            yield return null;

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.IsTrue(board.TryMove(1, 1, 0)); // s2 empties: its next layer slides forward
            Assert.IsTrue(_view.IsShelfRevealing(1));
            var hidden = _view.GetHiddenTileHome(1, 0);
            var front = _view.GetFrontTileHome(1, 0);
            float total = Vector2.Distance(hidden, front);
            var axis = (front - hidden) / total;
            float peak = 0f;
            for (int i = 0; i < 30 && _view.IsShelfRevealing(1); i++)
            {
                _view.AdvanceAnimations(Frame * 0.5f);
                if (!_view.IsShelfRevealing(1)) break;
                float along = Vector2.Dot(_view.GetFrontTilePixelPosition(1, 0) - hidden, axis) / total;
                peak = Mathf.Max(peak, along);
            }
            Assert.Greater(peak, 1.01f, "Overshoots the front slot: a small bounce.");
            Assert.LessOrEqual(peak, ClearRevealMotion.RevealPeakProgress + 0.01f);
            Assert.IsFalse(_view.IsShelfAnimating(1));
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                AssertNear(_view.GetFrontTileHome(1, slot), _view.GetFrontTilePixelPosition(1, slot), $"Slot {slot} rests in its front slot");
                Assert.AreEqual(1f, _view.GetFrontTileScale(1, slot), 0.0001f);
            }
        }

        // ---- Win: stars, confetti, buttons ----

        [UnityTest]
        public IEnumerator Win_StarsDropOneByOne_ThenConfetti_AndButtonsPressAndStillWork()
        {
            var panels = AddPanels();
            Bind(ChainBoard());
            yield return null; // panels build
            int next = 0;
            panels.NextClicked += () => next++;

            Assert.IsTrue(_view.Board.TryMove(0, 0, 1));
            yield return WaitUntil(() => _view.Outcome != BoardOutcome.None);
            Assert.IsTrue(panels.IsWinVisible);
            Assert.AreEqual(3, panels.WinStars.Length, "Three star slots.");
            Assert.AreEqual(3, panels.WinStarsLit, "No level list wired: all three lit.");
            Assert.IsFalse(panels.AreStarsAtRest, "Stars are still dropping in.");

            var landed = new float[3] { -1f, -1f, -1f };
            float t = 0f;
            while (panels.AreStarsPlaying && t < 3f)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < 3; i++)
                {
                    if (landed[i] < 0f && panels.WinStars[i].color.a >= 0.999f) landed[i] = t;
                }
            }
            Assert.IsTrue(panels.AreStarsAtRest, "All 3 stars shown, full size, in place after the drop.");
            Assert.Less(landed[0], landed[1], "Star 2 after star 1.");
            Assert.Less(landed[1], landed[2], "Star 3 after star 2.");
            Assert.AreEqual(1, panels.ConfettiBursts, "Confetti once the stars are in.");
            Assert.Greater(panels.Confetti.particleCount, 0);

            // Press down, spring back.
            var button = panels.WinNextButton;
            var data = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            yield return WaitUntil(() => button.transform.localScale.x <= UiFeel.PressedScale + 0.001f, 1f);
            Assert.AreEqual(UiFeel.PressedScale, button.transform.localScale.x, 0.001f, "Pressed down to 0.92.");
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            yield return WaitUntil(() => button.transform.localScale.x >= 0.999f, 1f);
            Assert.AreEqual(1f, button.transform.localScale.x, 0.001f, "Back up on release.");

            foreach (var b in panels.AllButtons) Assert.IsNotNull(b.GetComponent<ButtonPress>(), $"{b.name} presses down.");

            button.onClick.Invoke();
            Assert.AreEqual(1, next, "Next still works.");
            Assert.IsFalse(panels.IsWinVisible);
        }

        // ---- Haptics ----

        private PointerGesture Gesture() => new PointerGesture(new MoveInput(_view.Board), _view);

        private void Tap(PointerGesture gesture, Vector2 at)
        {
            gesture.PointerDown(at);
            gesture.PointerUp(at);
        }

        [UnityTest]
        public IEnumerator WrongMove_BuzzesExactlyOnce()
        {
            Bind(FullShelfBoard());
            yield return null;
            var gesture = Gesture();

            Tap(gesture, _view.Layout.GetFrontSlotRect(1, 0).center); // select s2's tumbler
            Assert.AreEqual(0, _device.Total, "Selecting is silent.");
            Tap(gesture, _view.Layout.GetShelfRect(0).center);         // onto full s1: refused
            Assert.AreEqual(InputOutcomeKind.Rejected, gesture.LastOutcome.Kind);
            Assert.IsTrue(_view.IsShaking, "Soft shake.");
            Assert.AreEqual(1, _device.WrongMoveCount, "One buzz for one wrong move.");
            Assert.AreEqual(0, _device.LightCount);

            // A refused drag: snap back with shake, one more buzz.
            var from = _view.Layout.GetFrontSlotRect(1, 1).center;
            var to = _view.Layout.GetShelfRect(0).center;
            gesture.PointerDown(from);
            gesture.PointerMove(to);
            gesture.PointerUp(to);
            Assert.AreEqual(InputOutcomeKind.Rejected, gesture.LastOutcome.Kind);
            Assert.AreEqual(2, _device.WrongMoveCount);
        }

        [UnityTest]
        public IEnumerator WrongMove_IsSilent_WhenHapticsAreOff()
        {
            _settings.HapticsEnabled = false;
            Bind(FullShelfBoard());
            yield return null;
            var gesture = Gesture();

            Tap(gesture, _view.Layout.GetFrontSlotRect(1, 0).center);
            Tap(gesture, _view.Layout.GetShelfRect(0).center);
            Assert.AreEqual(InputOutcomeKind.Rejected, gesture.LastOutcome.Kind);
            Assert.IsTrue(_view.IsShaking, "Still shakes.");
            Assert.AreEqual(0, _device.Total, "No buzz with haptics off.");

            Assert.IsTrue(_view.Board.TryMove(2, 0, 1)); // a legal move and later a clear: also silent
            _view.CompleteAnimations();
            Assert.AreEqual(0, _device.Total);
        }

        [UnityTest]
        public IEnumerator HapticsToggle_FlipsTheSetting_AndShowsIt()
        {
            var hud = AddHud();
            Bind(RevealBoard());
            yield return null;
            yield return null;

            Assert.AreEqual(GameHud.HapticsOnLabel, hud.HapticsLabel);
            hud.HapticsToggle.onClick.Invoke();
            Assert.IsFalse(_settings.HapticsEnabled);
            Assert.AreEqual(GameHud.HapticsOffLabel, hud.HapticsLabel);
            hud.HapticsToggle.onClick.Invoke();
            Assert.IsTrue(_settings.HapticsEnabled);
            Assert.AreEqual(GameHud.HapticsOnLabel, hud.HapticsLabel);
            Assert.IsNotNull(hud.HapticsToggle.GetComponent<ButtonPress>());
        }

        // ---- Allocation and frame time ----

        /// <summary>
        /// DOTween's own per-frame update (what its DOTweenComponent.Update calls), so the measured block runs the real
        /// tween update path synchronously. TweenManager is internal: found once by reflection, called through a typed
        /// delegate (no boxing).
        /// </summary>
        private static Action<UpdateType, float, float> TweenUpdate()
        {
            var manager = typeof(DOTween).Assembly.GetType("DG.Tweening.Core.TweenManager");
            var method = manager.GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null,
                new[] { typeof(UpdateType), typeof(float), typeof(float) }, null);
            Assert.IsNotNull(method, "DOTween TweenManager.Update(UpdateType, float, float)");
            return (Action<UpdateType, float, float>)Delegate.CreateDelegate(typeof(Action<UpdateType, float, float>), method);
        }

        [UnityTest]
        public IEnumerator Feel_AllocatesNothing_OverArcClearRevealAndStarDrop()
        {
            var effects = AddEffects();
            var hud = AddHud();
            var panels = AddPanels();
            Bind(ChainBoard());
            yield return null;
            yield return null;
            var tweens = TweenUpdate();

            // Warm-up: one whole chain (JIT, first tween startups, first particle emits, first canvas rebuilds).
            Assert.IsTrue(_view.Board.TryMove(0, 0, 1));
            for (int i = 0; i < 150; i++)
            {
                _view.AdvanceAnimations(Frame);
                tweens(UpdateType.Normal, Frame, Frame);
            }
            yield return null;
            Assert.IsTrue(panels.AreStarsAtRest, "Warm-up reached the stars.");

            Bind(ChainBoard());
            panels.Hide();
            hud.ResetCounter();
            yield return null;
            Assert.IsTrue(_view.Board.TryMove(0, 0, 1)); // flight + reveal, clear, clear (bursts), Won, stars
            int burstsBefore = effects.BurstCount;

            // 150 frames at 60 fps: flight 0.25 s + clear 0.35 s, stars 0.83 s, confetti.
            Assert.That(() =>
            {
                for (int i = 0; i < 150; i++)
                {
                    _view.AdvanceAnimations(Frame);
                    tweens(UpdateType.Normal, Frame, Frame);
                }
            }, Is.Not.AllocatingGCMemory());

            Assert.AreEqual(BoardOutcome.Won, _view.Outcome);
            Assert.AreEqual(2, effects.BurstCount - burstsBefore, "Both clears burst inside the measured frames.");
            Assert.AreEqual(2, hud.Counter.Cleared, "Both clears counted inside the measured frames.");
            Assert.IsTrue(panels.IsWinVisible);
            Assert.IsTrue(panels.AreStarsAtRest, "Star drop finished inside the measured frames.");
            Assert.GreaterOrEqual(panels.ConfettiBursts, 2, "Confetti started inside the measured frames.");
        }

        [UnityTest]
        public IEnumerator WinCoins_FlyIntoThePill_AllocatingNothing()
        {
            // The game scene wiring: bootstrap with progress, HUD, and the Win card connected to the HUD (PR #63).
            var bootstrap = AddBootstrap(W1L001, W1L002);
            bootstrap.Progress = new ProgressService(new FakeProgressStore(), _ => { });
            var hud = AddHud(bootstrap);
            var panels = Make("Result Panels").AddComponent<ResultPanels>();
            panels.Configure(_view, bootstrap, _camera, hud);
            yield return null;
            yield return null;
            var tweens = TweenUpdate();
            float flight = UiFeel.StarsSeconds + UiFeel.CoinsSecondsFor(UiFeel.CoinPool) + 0.2f;
            int frames = Mathf.CeilToInt(flight / Frame);

            // Warm-up: a whole first win and its coin flight (JIT, first tween startups, first canvas work).
            WinW1L001OnView();
            Assert.IsTrue(panels.IsWinVisible);
            for (int i = 0; i < frames; i++) tweens(UpdateType.Normal, Frame, Frame);
            Assert.AreEqual("20", hud.CountText, "Warm-up landed its coins.");
            yield return null;

            // Measured: a second first win (fresh progress), from the Win card on, while its coins fly and land.
            bootstrap.Progress = new ProgressService(new FakeProgressStore(), _ => { });
            bootstrap.Retry();
            Assert.AreEqual("0", hud.CountText);
            WinW1L001OnView();
            Assert.IsTrue(panels.IsWinVisible);
            Assert.IsTrue(panels.IsWinCoinsLineVisible);
            Assert.AreEqual("0", hud.CountText, "Held until the coins land.");
            Assert.That(() =>
            {
                for (int i = 0; i < frames; i++) tweens(UpdateType.Normal, Frame, Frame);
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(0, hud.CoinsInFlight);
            Assert.AreEqual("20", hud.CountText, "Every coin landed inside the measured frames.");
        }

        /// <summary>w1_l001's winning line (pack-1.md), its animations stepped by hand until the outcome card shows.</summary>
        private void WinW1L001OnView()
        {
            var board = _view.Board;
            Assert.IsTrue(board.TryMove(1, 1, 0));
            Assert.IsTrue(board.TryMove(2, 2, 1));
            Assert.IsTrue(board.TryMove(3, 0, 2));
            Assert.IsTrue(board.IsWon);
            for (int i = 0; i < 300 && _view.IsAnimating; i++) _view.AdvanceAnimations(Frame);
            _view.AdvanceAnimations(Frame);
        }

        [UnityTest]
        public IEnumerator Feel_FrameTimeAndGc_AreMeasured_OnABusyChain()
        {
            AddEffects();
            AddHud();
            AddPanels();
            var result = LevelLoader.Load(File.ReadAllText(W1L005), "w1_l005");
            Assert.IsTrue(result.Success);
            var board = result.Board;
            Bind(board);
            yield return null;
            yield return null;

            // w1_l005's winning line (pack-1.md), one move every 6 frames: flights, clears and reveals overlap.
            int[] line = { 3,1,5, 0,0,1, 4,1,0, 0,0,1, 0,1,2, 1,2,2, 0,2,1, 1,0,0, 3,0,1, 0,0,2, 1,0,0, 0,0,2, 1,1,0, 0,0,1, 1,0,2, 4,0,1, 2,1,1, 3,2,2 };
            var frameMs = new List<double>(512);
            var loopMs = new List<double>(512);
            var idleLoopMs = new List<double>(64);
            var gc = new List<long>(512);
            var idleGc = new List<long>(64);
            // "PlayerLoop" is the game's own loop (scripts, animation, particles, UI, rendering submit), without the
            // editor's own work and the wait for the 60 fps target that the frame interval includes.
            using (var loop = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop", 1))
            using (var alloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                // Baseline: the same scene at rest.
                for (int i = 0; i < 40; i++)
                {
                    yield return null;
                    if (i < 3) continue;
                    if (loop.Valid) idleLoopMs.Add(loop.LastValue / 1e6);
                    if (alloc.Valid) idleGc.Add(alloc.LastValue);
                }

                int move = 0;
                int frame = 0;
                while (frame < 400 && (move < line.Length || _view.Outcome == BoardOutcome.None || frame < 300))
                {
                    if (move < line.Length && frame % 6 == 0)
                    {
                        board.TryMove(line[move], line[move + 1], line[move + 2]);
                        move += 3;
                    }
                    yield return null;
                    frame++;
                    if (frame < 3) continue;
                    frameMs.Add(Time.unscaledDeltaTime * 1000.0);
                    if (loop.Valid) loopMs.Add(loop.LastValue / 1e6);
                    if (alloc.Valid) gc.Add(alloc.LastValue);
                }
            }
            Assert.IsTrue(board.IsWon, "The line wins.");
            Debug.Log($"[#21 frame] busy chain on w1_l005, editor play mode: frame interval {Stats(frameMs)} ms; " +
                $"PlayerLoop {Stats(loopMs)} ms (idle PlayerLoop {Stats(idleLoopMs)} ms); " +
                $"GC Allocated In Frame, whole editor frame: busy {StatsLong(gc)} bytes, idle {StatsLong(idleGc)} bytes");
            Assert.Greater(frameMs.Count, 100);
        }

        private static string Stats(List<double> values)
        {
            if (values.Count == 0) return "n/a";
            var sorted = new List<double>(values);
            sorted.Sort();
            double sum = 0;
            foreach (var v in sorted) sum += v;
            return $"n={sorted.Count} mean={sum / sorted.Count:F2} p95={sorted[(int)(sorted.Count * 0.95)]:F2} max={sorted[sorted.Count - 1]:F2}";
        }

        private static string StatsLong(List<long> values)
        {
            if (values.Count == 0) return "n/a";
            long max = 0, sum = 0;
            int nonZero = 0;
            foreach (var v in values)
            {
                sum += v;
                if (v > max) max = v;
                if (v > 0) nonZero++;
            }
            return $"n={values.Count} mean={sum / values.Count} max={max} nonzero-frames={nonZero}";
        }
    }
}
