using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Levels;
using Game.View;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Clear and reveal feedback on <see cref="BoardView"/> (issue #6): the animations run from Core events, end in
    /// the board's real state, hold back Won/Stuck until they finish, and allocate nothing while they play.
    /// Synchronous checks step the animations with <see cref="BoardView.AdvanceAnimations"/> inside one frame, so they
    /// are exact; the frame-driven checks let <c>Update</c> run them.
    /// </summary>
    public class ClearRevealPlayModeTests
    {
        private const string LevelPath = "Assets/Levels/World1/w1_l001.json";
        private const float Frame = 1f / 60f;

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

        private void Bind(Board board)
        {
            _view.Bind(board, _camera, new Rect(0, 0, _camera.pixelWidth, _camera.pixelHeight), BoardLayoutSettings.Default);
        }

        private static Board LoadW1L001()
        {
            var result = LevelLoader.Load(File.ReadAllText(LevelPath), "w1_l001.json");
            Assert.IsTrue(result.Success, "w1_l001 should load.");
            return result.Board;
        }

        /// <summary>s1 [T T _], s2 [T W _] behind [W W I], s3 [I I _]: s2's second layer is revealed by emptying s2.</summary>
        private static Board RevealBoard()
        {
            return new Board(
                new Shelf(new[] { "tumbler", "tumbler", null }),
                new Shelf(new[] { "tumbler", "tawa", null }, new[] { "tawa", "tawa", "idli_stand" }),
                new Shelf(new[] { "idli_stand", "idli_stand", null }));
        }

        /// <summary>
        /// s1 [W _ _] hiding a tumbler triple, s2 [W W _]. Moving s1's tawa to s2 reveals the triple on s1 (which then
        /// clears) and clears the tawas on s2: the move wins with a reveal-then-clear chain.
        /// </summary>
        private static Board ChainBoard()
        {
            return new Board(
                new Shelf(new[] { "tawa", null, null }, new[] { "tumbler", "tumbler", "tumbler" }),
                new Shelf(new[] { "tawa", "tawa", null }));
        }

        private static void AssertNear(Vector2 expected, Vector2 actual, string message)
        {
            Assert.Less(Vector2.Distance(expected, actual), 0.01f, $"{message}: expected {expected} but was {actual}");
        }

        // ---- Clear ----

        [UnityTest]
        public IEnumerator Clear_PopsThenFades_AndTilesEndHiddenAndReset()
        {
            var board = LoadW1L001();
            Bind(board);
            yield return null;

            Assert.IsTrue(board.TryMove(1, 1, 0)); // tumbler triple on s1 clears
            Assert.IsTrue(_view.IsShelfAnimating(0), "The clear plays on s1.");
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                Assert.AreEqual("T", _view.GetFrontLetter(0, slot), $"Slot {slot} still shows the tumbler as it starts to pop.");
            }

            _view.AdvanceAnimations(ClearRevealMotion.ClearPopSeconds);
            Assert.Greater(_view.GetFrontTileScale(0, 0), 1.1f, "Pops up.");
            Assert.AreEqual(1f, _view.GetFrontTileAlpha(0, 0), 0.01f, "Opaque at the peak.");

            _view.AdvanceAnimations(ClearRevealMotion.ClearFadeSeconds * 0.5f);
            Assert.Less(_view.GetFrontTileScale(0, 0), ClearRevealMotion.ClearPeakScale, "Shrinks after the peak.");
            Assert.Less(_view.GetFrontTileAlpha(0, 0), 1f, "Fades.");

            _view.AdvanceAnimations(ClearRevealMotion.ClearFadeSeconds * 0.5f + 0.001f);
            Assert.IsFalse(_view.IsShelfAnimating(0), "Done after ClearSeconds.");
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                Assert.IsNull(_view.GetFrontLetter(0, slot), $"s1 slot {slot} hidden once cleared.");
                Assert.AreEqual(1f, _view.GetFrontTileScale(0, slot), $"s1 slot {slot} scale reset for reuse.");
                AssertNear(_view.GetFrontTileHome(0, slot), _view.GetFrontTilePixelPosition(0, slot), $"s1 slot {slot} back home");
            }
            Assert.AreEqual(board.ItemCount, _view.VisibleFrontTileCount);
        }

        [UnityTest]
        public IEnumerator Clear_DrivenByFrames_FinishesInUnderHalfASecond()
        {
            var board = LoadW1L001();
            Bind(board);
            yield return null;

            Assert.IsTrue(board.TryMove(1, 1, 0));
            float started = Time.time;
            while (_view.IsAnimating && Time.time - started < 2f) yield return null;

            Assert.IsFalse(_view.IsAnimating);
            Assert.Less(Time.time - started, 0.5f + 0.1f, "Clear finishes in about ClearSeconds (plus a frame or two).");
            Assert.AreEqual(0, CountVisible(0));
        }

        // ---- Reveal ----

        [UnityTest]
        public IEnumerator Reveal_SlidesFromHiddenSlotToFrontSlot()
        {
            var board = RevealBoard();
            Bind(board);
            yield return null;

            Assert.IsTrue(board.TryMove(1, 0, 0)); // tumblers clear on s1
            Assert.IsTrue(board.TryMove(1, 1, 0)); // s2 front empties: its hidden layer slides forward
            Assert.IsTrue(_view.IsShelfRevealing(1));

            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                AssertNear(_view.GetHiddenTileHome(1, slot), _view.GetFrontTilePixelPosition(1, slot), $"Slot {slot} starts at its hidden spot");
                Assert.AreEqual(BoardLayoutSettings.Default.HiddenStripHeight, _view.GetFrontTileScale(1, slot), 0.001f, "Starts hidden-tile size.");
            }

            _view.AdvanceAnimations(ClearRevealMotion.RevealSeconds * 0.5f);
            var mid = _view.GetFrontTilePixelPosition(1, 0);
            float total = Vector2.Distance(_view.GetHiddenTileHome(1, 0), _view.GetFrontTileHome(1, 0));
            Assert.Greater(Vector2.Distance(_view.GetHiddenTileHome(1, 0), mid), 0.1f * total, "Has moved forward.");
            Assert.Greater(Vector2.Distance(_view.GetFrontTileHome(1, 0), mid), 0.01f * total, "Not there yet.");

            _view.AdvanceAnimations(ClearRevealMotion.RevealSeconds);
            Assert.IsFalse(_view.IsShelfAnimating(1));
            string[] expected = { "W", "W", "I" };
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                Assert.AreEqual(expected[slot], _view.GetFrontLetter(1, slot));
                AssertNear(_view.GetFrontTileHome(1, slot), _view.GetFrontTilePixelPosition(1, slot), $"Slot {slot} ends in its front slot");
                Assert.AreEqual(1f, _view.GetFrontTileScale(1, slot), 0.0001f);
                Assert.AreEqual(PlaceholderPalette.Get(board.GetShelf(1).GetFrontItem(slot)).Color, _view.GetFrontColor(1, slot), "Full brightness.");
            }
        }

        [UnityTest]
        public IEnumerator Reveal_DrivenByFrames_EndsInFrontSlots()
        {
            var board = RevealBoard();
            Bind(board);
            yield return null;

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.IsTrue(board.TryMove(1, 1, 0));
            float started = Time.time;
            while (_view.IsAnimating && Time.time - started < 2f) yield return null;

            Assert.IsFalse(_view.IsAnimating);
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                AssertNear(_view.GetFrontTileHome(1, slot), _view.GetFrontTilePixelPosition(1, slot), $"Slot {slot}");
                Assert.AreEqual(1f, _view.GetFrontTileScale(1, slot), 0.0001f);
            }
        }

        // ---- Chains and outcome ----

        [UnityTest]
        public IEnumerator Chain_RevealThenClear_ThenWinIsShownAfterAnimations()
        {
            var board = ChainBoard();
            Bind(board);
            int raised = 0;
            BoardOutcome shown = BoardOutcome.None;
            _view.OutcomeReady += outcome =>
            {
                raised++;
                shown = outcome;
            };
            yield return null;

            Assert.IsTrue(board.TryMove(0, 0, 1));
            Assert.IsTrue(board.IsWon, "Precondition: the move wins.");
            Assert.IsTrue(_view.IsShelfRevealing(0), "s1 reveals first...");
            Assert.IsTrue(_view.IsShelfAnimating(1), "...while s2 clears.");
            Assert.IsTrue(_view.InputLocked, "Board input stops at Won.");
            Assert.AreEqual(BoardOutcome.None, _view.Outcome, "Win panel waits for the animations.");

            _view.AdvanceAnimations(ClearRevealMotion.RevealSeconds + 0.001f);
            Assert.IsTrue(_view.IsShelfAnimating(0), "The revealed triple then clears.");
            Assert.IsFalse(_view.IsShelfRevealing(0));
            Assert.AreEqual(0, raised);

            _view.AdvanceAnimations(ClearRevealMotion.ClearSeconds);
            Assert.IsFalse(_view.IsAnimating);
            Assert.AreEqual(1, raised, "OutcomeReady once animations end.");
            Assert.AreEqual(BoardOutcome.Won, shown);
            Assert.AreEqual(BoardOutcome.Won, _view.Outcome);
            Assert.AreEqual(0, _view.VisibleFrontTileCount);

            _view.AdvanceAnimations(1f);
            Assert.AreEqual(1, raised, "Raised only once.");
        }

        [UnityTest]
        public IEnumerator MoveOntoAnimatingShelf_SnapsItToItsFinalStateFirst()
        {
            var board = RevealBoard();
            Bind(board);
            yield return null;

            Assert.IsTrue(board.TryMove(1, 0, 0)); // s1 clears
            Assert.IsTrue(_view.IsShelfAnimating(0));
            Assert.IsTrue(board.TryMove(2, 0, 0)); // idli stand onto the clearing shelf
            Assert.IsFalse(_view.IsShelfAnimating(0), "Clear snapped to its end.");
            Assert.AreEqual("I", _view.GetFrontLetter(0, 0));
            Assert.IsNull(_view.GetFrontLetter(0, 1));
            Assert.AreEqual(1f, _view.GetFrontTileScale(0, 0));
            Assert.AreEqual(1f, _view.GetFrontTileAlpha(0, 0), 0.01f);
        }

        // ---- Allocations ----

        [UnityTest]
        public IEnumerator Animations_AllocateNothing_WhileTheyPlay()
        {
            // Warm up one full chain so first-call costs (JIT, static init) are out of the way.
            var warm = ChainBoard();
            Bind(warm);
            yield return null;
            Assert.IsTrue(warm.TryMove(0, 0, 1));
            for (int i = 0; i < 60; i++) _view.AdvanceAnimations(Frame);

            var board = ChainBoard();
            Bind(board);
            yield return null;
            Assert.IsTrue(board.TryMove(0, 0, 1)); // reveal, clear, clear, then Won
            Assert.IsTrue(_view.IsAnimating);

            // 40 frames at 60 fps covers the reveal + clear chain, step changes and the OutcomeReady raise.
            Assert.That(() =>
            {
                for (int i = 0; i < 40; i++) _view.AdvanceAnimations(Frame);
            }, Is.Not.AllocatingGCMemory());
            Assert.IsFalse(_view.IsAnimating, "The chain finished inside the measured frames.");
            Assert.AreEqual(BoardOutcome.Won, _view.Outcome);
        }

        [UnityTest]
        public IEnumerator Animations_GcAllocatedPerFrame_IsMeasured()
        {
            var warm = ChainBoard();
            Bind(warm);
            yield return null;
            Assert.IsTrue(warm.TryMove(0, 0, 1));
            while (_view.IsAnimating) yield return null;

            var board = RevealBoard();
            Bind(board);
            yield return null;

            var idle = new List<long>(256);
            var animating = new List<long>(256);
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                // Baseline: the same board at rest, so editor and test-runner allocations can be told apart.
                yield return null;
                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                    if (recorder.Valid) idle.Add(recorder.LastValue);
                }

                Assert.IsTrue(board.TryMove(1, 0, 0)); // clear on s1
                Assert.IsTrue(board.TryMove(1, 1, 0)); // reveal on s2
                yield return null; // the move frame's own allocations are not animation frames
                while (_view.IsAnimating)
                {
                    yield return null;
                    if (recorder.Valid) animating.Add(recorder.LastValue);
                }
            }

            // Whole-frame numbers include the editor and test runner, so they are reported, not asserted; the exact
            // zero-allocation check on the animation code is Animations_AllocateNothing_WhileTheyPlay.
            Debug.Log($"[#6 GC] idle: {Describe(idle)}; clear+reveal: {Describe(animating)} (whole editor frame, GC Allocated In Frame)");
            Assert.Greater(animating.Count, 0, "Measured some animation frames.");
        }

        private static string Describe(List<long> frames)
        {
            long max = 0;
            long sum = 0;
            long min = long.MaxValue;
            foreach (long bytes in frames)
            {
                sum += bytes;
                if (bytes > max) max = bytes;
                if (bytes < min) min = bytes;
            }
            long mean = frames.Count == 0 ? 0 : sum / frames.Count;
            return $"frames={frames.Count} min={min} mean={mean} max={max} bytes";
        }

        private int CountVisible(int shelf)
        {
            int count = 0;
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                if (_view.GetFrontLetter(shelf, slot) != null) count++;
            }
            return count;
        }
    }
}
