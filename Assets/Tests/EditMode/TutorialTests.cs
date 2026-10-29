using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Levels;
using Game.Services;
using Game.View;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Issue #57, first-play tutorials: the step logic (<see cref="TutorialGuide"/>), the level JSON key
    /// (<see cref="TutorialKeys"/>, validator), the save of completed tutorials, the input gate in
    /// <see cref="MoveInput"/>, and the cue timing.
    /// </summary>
    public class TutorialTests
    {
        private const string A = "tumbler";
        private const string B = "idli_stand";
        private const string C = "masala_dabba";

        private static LevelLoadResult LoadWorld1(int index)
        {
            string name = $"w1_l{index:000}";
            var result = LevelLoader.Load(File.ReadAllText($"Assets/Levels/World1/{name}.json"), name);
            Assert.IsTrue(result.Success, string.Join("\n", result.Errors.Select(e => e.Message)));
            return result;
        }

        // ---------- The levels carry the right key ----------

        [Test]
        public void Levels_L1L2L11_CarryTheirTutorialKeys()
        {
            Assert.AreEqual(TutorialKeys.Match3, LoadWorld1(1).Level.Tutorial);
            Assert.AreEqual(TutorialKeys.Layers, LoadWorld1(2).Level.Tutorial);
            Assert.AreEqual(TutorialKeys.Locked, LoadWorld1(11).Level.Tutorial);
        }

        [Test]
        public void Levels_L11_HasALockedShelf()
        {
            var board = LoadWorld1(11).Board;
            Assert.IsTrue(Enumerable.Range(0, board.ShelfCount).Any(i => board.GetShelf(i).IsLocked));
        }

        // ---------- Keys ----------

        [TestCase(null, TutorialKind.None)]
        [TestCase("match3", TutorialKind.Match3)]
        [TestCase("layers", TutorialKind.Layers)]
        [TestCase("locked", TutorialKind.Locked)]
        [TestCase("undo", TutorialKind.Undo)]
        [TestCase("extra_slot", TutorialKind.ExtraSlot)]
        public void Keys_ParseKnownValues(string key, TutorialKind expected)
        {
            Assert.IsTrue(TutorialKeys.TryParse(key, out var kind));
            Assert.AreEqual(expected, kind);
        }

        [TestCase("Match3")]
        [TestCase("lock")]
        [TestCase("")]
        public void Keys_RejectUnknownValues(string key)
        {
            Assert.IsFalse(TutorialKeys.TryParse(key, out var kind));
            Assert.AreEqual(TutorialKind.None, kind);
            Assert.AreEqual(TutorialKind.None, TutorialKeys.Parse(key));
        }

        // ---------- Validator ----------

        private const string TwoShelves =
            "{ \"id\": \"s1\", \"kind\": \"normal\", \"layers\": [[\"tumbler\", \"tumbler\", null]] }, " +
            "{ \"id\": \"s2\", \"kind\": \"normal\", \"layers\": [[\"tumbler\", null, null]] }";

        private static IReadOnlyList<LevelError> Validate(string tutorial, string shelves)
        {
            string json = $"{{ \"schema\": 1, \"id\": \"t\", \"tutorial\": {tutorial}, \"shelves\": [ {shelves} ] }}";
            return LevelValidator.Validate(LevelParser.Parse(json));
        }

        [TestCase("null")]
        [TestCase("\"match3\"")]
        [TestCase("\"undo\"")]
        [TestCase("\"extra_slot\"")]
        public void Validator_AcceptsKnownTutorialThatFits(string tutorial)
        {
            CollectionAssert.IsEmpty(Validate(tutorial, TwoShelves));
        }

        [Test]
        public void Validator_RejectsUnknownTutorial()
        {
            var errors = Validate("\"swap\"", TwoShelves);
            Assert.AreEqual(1, errors.Count);
            Assert.AreEqual(LevelErrorCode.UnknownTutorial, errors[0].Code);
            StringAssert.Contains("\"swap\"", errors[0].Message);
        }

        [Test]
        public void Validator_RejectsLockedTutorialWithoutLockedShelf()
        {
            var errors = Validate("\"locked\"", TwoShelves);
            Assert.AreEqual(LevelErrorCode.TutorialDoesNotFit, errors.Single().Code);
        }

        [Test]
        public void Validator_RejectsLayersTutorialWithoutHiddenLayer()
        {
            var errors = Validate("\"layers\"", TwoShelves);
            Assert.AreEqual(LevelErrorCode.TutorialDoesNotFit, errors.Single().Code);
        }

        [Test]
        public void Validator_AcceptsLockedTutorialWithLockedShelf()
        {
            string shelves = TwoShelves + ", " +
                "{ \"id\": \"s3\", \"kind\": \"normal\", \"layers\": [[\"tawa\", \"tawa\", null]] }, " +
                "{ \"id\": \"s4\", \"kind\": \"locked\", \"lockClears\": 1, \"layers\": [[\"tawa\", null, null]] }";
            CollectionAssert.IsEmpty(Validate("\"locked\"", shelves));
        }

        // ---------- Guide: match3 (L1) ----------

        [Test]
        public void Match3_OnL1_GuidesTheTumblerOntoTheTumblerPair()
        {
            var guide = new TutorialGuide(LoadWorld1(1).Board, TutorialKind.Match3);
            Assert.IsTrue(guide.IsActive);
            Assert.IsTrue(guide.Restricts);
            Assert.AreEqual(TutorialCue.HandPointer, guide.Cue);
            Assert.AreEqual(new GuidedMove(1, 1, 0), guide.Move);
        }

        [Test]
        public void Match3_OnlyTheGuidedItemAndTargetAreAllowed()
        {
            var board = LoadWorld1(1).Board;
            var guide = new TutorialGuide(board, TutorialKind.Match3);

            for (int shelf = 0; shelf < board.ShelfCount; shelf++)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    bool expected = shelf == 1 && slot == 1;
                    Assert.AreEqual(expected, guide.AllowsPick(shelf, slot), $"pick ({shelf},{slot})");
                }
            }

            Assert.IsTrue(guide.AllowsMove(1, 1, 0), "the guided move");
            Assert.IsTrue(guide.AllowsMove(1, 1, 1), "own shelf is a cancel");
            Assert.IsFalse(guide.AllowsMove(1, 1, 3), "another legal target");
            Assert.IsFalse(guide.AllowsMove(2, 2, 3), "another item");
        }

        [Test]
        public void Match3_CompletesOnceOnTheFirstClear_ThenAllowsEverything()
        {
            var board = LoadWorld1(1).Board;
            var guide = new TutorialGuide(board, TutorialKind.Match3);
            int completed = 0;
            guide.Completed += () => completed++;

            Assert.IsTrue(board.TryMove(1, 1, 0));
            Assert.AreEqual(1, completed);
            Assert.IsTrue(guide.IsComplete);
            Assert.IsFalse(guide.IsActive);
            Assert.IsFalse(guide.Restricts);
            Assert.AreEqual(TutorialCue.None, guide.Cue);
            Assert.IsTrue(guide.AllowsPick(2, 2));
            Assert.IsTrue(guide.AllowsMove(2, 2, 3));

            Assert.IsTrue(board.TryMove(2, 2, 3));
            Assert.IsTrue(board.TryMove(3, 0, 2)); // another clear
            Assert.AreEqual(1, completed, "Completed is raised once.");
        }

        [Test]
        public void Match3_MoveThatDoesNotClear_DoesNotComplete()
        {
            var board = LoadWorld1(1).Board;
            var guide = new TutorialGuide(board, TutorialKind.Match3);
            Assert.IsTrue(board.TryMove(2, 2, 3)); // around the gate (the board has no tutorial rules)
            Assert.IsTrue(guide.IsActive);
        }

        [Test]
        public void Match3_WithoutAOneMoveTriple_StartsComplete_AndNeverRestricts()
        {
            // A B _ / B A _ / A B _ : no shelf holds a pair, so no single move makes a triple.
            var board = new Board(new Shelf(new[] { A, B, null }), new Shelf(new[] { B, A, null }), new Shelf(new[] { A, B, null }));
            var guide = new TutorialGuide(board, TutorialKind.Match3);
            Assert.AreEqual(GuidedMove.None, guide.Move);
            Assert.IsTrue(guide.IsComplete);
            Assert.IsFalse(guide.Restricts);
            Assert.IsTrue(guide.AllowsPick(0, 0));
        }

        [Test]
        public void FindTripleMove_SkipsLockedShelves()
        {
            // Pair on a locked shelf can't receive; the open pair (shelf 2) is used.
            var board = new Board(
                Shelf.Locked(1, new[] { C, C, null }),
                new Shelf(new[] { C, B, A }),
                new Shelf(new[] { B, B, null }),
                new Shelf(new[] { A, A, null }));
            var move = TutorialGuide.FindTripleMove(board);
            Assert.AreEqual(2, move.ToShelf);
            Assert.AreEqual(new GuidedMove(1, 1, 2), move);
        }

        [Test]
        public void Dispose_StopsListening()
        {
            var board = LoadWorld1(1).Board;
            var guide = new TutorialGuide(board, TutorialKind.Match3);
            int completed = 0;
            guide.Completed += () => completed++;
            guide.Dispose();
            Assert.IsTrue(board.TryMove(1, 1, 0));
            Assert.AreEqual(0, completed);
        }

        // ---------- Guide: layers (L2) ----------

        [Test]
        public void Layers_OnL2_PointsAtTheShelfWithTheHiddenLayer_NoRestriction()
        {
            var board = LoadWorld1(2).Board;
            var guide = new TutorialGuide(board, TutorialKind.Layers);
            Assert.AreEqual(TutorialCue.LayerArrow, guide.Cue);
            Assert.AreEqual(0, guide.FocusShelf);
            Assert.IsFalse(guide.Restricts);
            Assert.IsTrue(guide.AllowsPick(2, 0));
            Assert.IsTrue(guide.AllowsMove(2, 0, 3));
        }

        [Test]
        public void Layers_CompletesWhenTheLayerIsRevealed()
        {
            var board = LoadWorld1(2).Board;
            var guide = new TutorialGuide(board, TutorialKind.Layers);
            int completed = 0;
            guide.Completed += () => completed++;

            Assert.IsTrue(board.TryMove(2, 0, 3)); // no reveal yet
            Assert.AreEqual(0, completed);
            Assert.IsTrue(board.TryMove(1, 1, 0)); // tumbler triple clears on s1, its hidden layer slides forward
            Assert.AreEqual(1, completed);
            Assert.IsTrue(guide.IsComplete);
        }

        [Test]
        public void Layers_WithoutHiddenLayer_StartsComplete()
        {
            var guide = new TutorialGuide(LoadWorld1(1).Board, TutorialKind.Layers);
            Assert.IsTrue(guide.IsComplete);
            Assert.AreEqual(-1, guide.FocusShelf);
        }

        // ---------- Guide: locked (L11) ----------

        [Test]
        public void Locked_OnL11_PointsAtTheLockedShelf_WithoutRestriction()
        {
            var board = LoadWorld1(11).Board;
            var guide = new TutorialGuide(board, TutorialKind.Locked);
            Assert.AreEqual(TutorialCue.LockedLine, guide.Cue);
            Assert.AreEqual(5, guide.FocusShelf);
            Assert.IsTrue(board.GetShelf(5).IsLocked);
            Assert.IsFalse(guide.Restricts);
            Assert.IsTrue(guide.AllowsPick(0, 0));
        }

        [Test]
        public void Locked_CompletesWhenTheShelfOpens()
        {
            // A A _ / A _ _ / locked(1): B C _ . Moving the lone A clears a triple and opens the lock.
            var board = new Board(new Shelf(new[] { A, A, null }), new Shelf(new[] { A, null, null }),
                Shelf.Locked(1, new[] { B, C, null }));
            var guide = new TutorialGuide(board, TutorialKind.Locked);
            Assert.AreEqual(2, guide.FocusShelf);
            int completed = 0;
            guide.Completed += () => completed++;

            Assert.IsTrue(board.TryMove(1, 0, 0));
            Assert.IsFalse(board.GetShelf(2).IsLocked);
            Assert.AreEqual(1, completed);
            Assert.IsTrue(guide.IsComplete);
        }

        [Test]
        public void Locked_WithoutLockedShelf_StartsComplete()
        {
            Assert.IsTrue(new TutorialGuide(LoadWorld1(1).Board, TutorialKind.Locked).IsComplete);
        }

        // ---------- Guide: not built yet ----------

        [TestCase(TutorialKind.Undo)]
        [TestCase(TutorialKind.ExtraSlot)]
        [TestCase(TutorialKind.None)]
        public void UnbuiltKinds_HaveNoCue_AndNeverComplete(TutorialKind kind)
        {
            var board = LoadWorld1(1).Board;
            var guide = new TutorialGuide(board, kind);
            Assert.IsFalse(guide.IsSupported);
            Assert.IsFalse(guide.IsActive);
            Assert.IsFalse(guide.IsComplete);
            Assert.AreEqual(TutorialCue.None, guide.Cue);
            Assert.IsTrue(guide.AllowsPick(0, 0));
        }

        // ---------- Save ----------

        [Test]
        public void Progress_MarkTutorialDone_SavesAndSurvivesReload()
        {
            var store = new FakeProgressStore();
            var service = new ProgressService(store, _ => Assert.Fail("no warning expected"));
            int changed = 0;
            service.Changed += () => changed++;
            Assert.IsFalse(service.IsTutorialDone("match3"));

            service.MarkTutorialDone("match3");
            Assert.IsTrue(service.IsTutorialDone("match3"));
            Assert.IsFalse(service.IsTutorialDone("layers"));
            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual(1, changed);

            service.MarkTutorialDone("match3");
            Assert.AreEqual(1, store.SaveCount, "A repeat saves nothing.");

            var reloaded = new ProgressService(store);
            Assert.IsTrue(reloaded.IsTutorialDone("match3"));
        }

        [Test]
        public void Progress_SaveFromBeforeTutorials_ReadsAsNoneDone()
        {
            const string oldSave = "{\"version\":1,\"highestUnlocked\":\"w1_l002\",\"levels\":[{\"id\":\"w1_l001\",\"stars\":3}]}";
            Assert.IsTrue(ProgressSerializer.TryRead(oldSave, out var data, out string problem), problem);
            CollectionAssert.IsEmpty(data.tutorialsDone);
            Assert.AreEqual(3, data.levels.Single().stars);
        }

        [Test]
        public void Progress_RepeatedOrEmptyTutorialKey_IsUnreadable()
        {
            Assert.IsFalse(ProgressSerializer.TryRead(
                "{\"version\":1,\"highestUnlocked\":\"\",\"levels\":[],\"tutorialsDone\":[\"match3\",\"match3\"]}", out _, out _));
            Assert.IsFalse(ProgressSerializer.TryRead(
                "{\"version\":1,\"highestUnlocked\":\"\",\"levels\":[],\"tutorialsDone\":[\"\"]}", out _, out _));
        }

        [Test]
        public void Progress_Reset_ForgetsTutorials()
        {
            var service = new ProgressService(new FakeProgressStore());
            service.MarkTutorialDone("layers");
            service.Reset();
            Assert.IsFalse(service.IsTutorialDone("layers"));
        }

        // ---------- Input gate (MoveInput) ----------

        private sealed class OnlyMove : IMoveGate
        {
            private readonly GuidedMove _move;
            public OnlyMove(GuidedMove move) => _move = move;
            public bool AllowsPick(int shelf, int slot) => shelf == _move.FromShelf && slot == _move.FromSlot;
            public bool AllowsMove(int fromShelf, int fromSlot, int toShelf) =>
                AllowsPick(fromShelf, fromSlot) && (toShelf == fromShelf || toShelf == _move.ToShelf);
        }

        // Shelf 0: A A _   Shelf 1: B A B   Shelf 2: C C B   Shelf 3: C _ _ ; allowed: (1,1) -> 0
        private static MoveInput GatedInput(out Board board)
        {
            board = new Board(
                new Shelf(new[] { A, A, null }),
                new Shelf(new[] { B, A, B }),
                new Shelf(new[] { C, C, B }),
                new Shelf(new[] { C, null, null }));
            return new MoveInput(board) { Gate = new OnlyMove(new GuidedMove(1, 1, 0)) };
        }

        [Test]
        public void Gate_TapOnOtherItem_IsIgnored()
        {
            var input = GatedInput(out _);
            Assert.AreEqual(InputOutcomeKind.Ignored, input.Tap(BoardTarget.At(2, 2)).Kind);
            Assert.IsFalse(input.HasSelection);
        }

        [Test]
        public void Gate_TapOnWrongShelf_IsIgnored_AndKeepsSelection()
        {
            var input = GatedInput(out var board);
            Assert.AreEqual(InputOutcomeKind.Selected, input.Tap(BoardTarget.At(1, 1)).Kind);
            Assert.AreEqual(InputOutcomeKind.Ignored, input.Tap(BoardTarget.At(3, 1)).Kind);
            Assert.IsTrue(input.HasSelection);
            Assert.AreEqual(A, board.GetShelf(1).GetFrontItem(1), "Nothing moved.");

            var moved = input.Tap(BoardTarget.At(0, -1));
            Assert.AreEqual(InputOutcomeKind.Moved, moved.Kind);
            Assert.IsTrue(board.GetShelf(0).IsFrontEmpty, "The tumbler triple cleared.");
        }

        [Test]
        public void Gate_TapOnOtherItemOfOwnShelf_Deselects()
        {
            var input = GatedInput(out _);
            input.Tap(BoardTarget.At(1, 1));
            Assert.AreEqual(InputOutcomeKind.Deselected, input.Tap(BoardTarget.At(1, 0)).Kind);
            Assert.IsFalse(input.HasSelection);
        }

        [Test]
        public void Gate_DragOfOtherItem_DoesNotStart()
        {
            var input = GatedInput(out _);
            Assert.AreEqual(InputOutcomeKind.Ignored, input.BeginDrag(BoardTarget.At(2, 2)).Kind);
            Assert.IsFalse(input.IsDragging);
        }

        [Test]
        public void Gate_DropOnWrongShelf_ReturnsHomeWithoutMove()
        {
            var input = GatedInput(out var board);
            Assert.AreEqual(InputOutcomeKind.DragStarted, input.BeginDrag(BoardTarget.At(1, 1)).Kind);
            Assert.AreEqual(InputOutcomeKind.ReturnedHome, input.Drop(BoardTarget.At(3, 0)).Kind);
            Assert.AreEqual(A, board.GetShelf(1).GetFrontItem(1));

            input.BeginDrag(BoardTarget.At(1, 1));
            Assert.AreEqual(InputOutcomeKind.Moved, input.Drop(BoardTarget.At(0, 2)).Kind);
        }

        [Test]
        public void Gate_Null_LetsEveryLegalMoveThrough()
        {
            var input = GatedInput(out _);
            input.Gate = null;
            Assert.AreEqual(InputOutcomeKind.Selected, input.Tap(BoardTarget.At(2, 2)).Kind);
            Assert.AreEqual(InputOutcomeKind.Moved, input.Tap(BoardTarget.At(3, 0)).Kind);
        }

        // ---------- Cue timing and text ----------

        [Test]
        public void HandPose_FadesInOnTheItem_GlidesToTheTarget_AndLoops()
        {
            var start = TutorialCueMotion.Hand(0f);
            Assert.AreEqual(0f, start.Travel);
            Assert.AreEqual(0f, start.Alpha);

            var pressed = TutorialCueMotion.Hand(0.45f);
            Assert.AreEqual(0f, pressed.Travel, 1e-4f);
            Assert.AreEqual(1f, pressed.Alpha, 1e-4f);
            Assert.AreEqual(TutorialCueMotion.PressScale, pressed.Scale, 1e-4f);

            var mid = TutorialCueMotion.Hand(0.85f);
            Assert.That(mid.Travel, Is.GreaterThan(0.2f).And.LessThan(0.8f));

            var arrived = TutorialCueMotion.Hand(1.3f);
            Assert.AreEqual(1f, arrived.Travel);

            Assert.AreEqual(0f, TutorialCueMotion.Hand(1.9f).Alpha, "pause between loops");
            var looped = TutorialCueMotion.Hand(TutorialCueMotion.HandPeriod + 0.85f);
            Assert.AreEqual(mid.Travel, looped.Travel, 1e-4f);
        }

        [Test]
        public void ArrowBob_StaysWithinItsHeight()
        {
            for (float t = 0f; t < 2f; t += 0.05f)
            {
                Assert.LessOrEqual(UnityEngine.Mathf.Abs(TutorialCueMotion.ArrowBob(t)), TutorialCueMotion.ArrowBobTiles + 1e-5f);
            }
        }

        [Test]
        public void LockedLine_IsOneShortLine()
        {
            Assert.AreEqual("Make 1 match to open", TutorialDirector.LockedLine(1));
            Assert.AreEqual("Make 2 matches to open", TutorialDirector.LockedLine(2));
            StringAssert.DoesNotContain("\n", TutorialDirector.LockedLine(1));
        }
    }
}
