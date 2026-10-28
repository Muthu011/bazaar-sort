using System.IO;
using System.Linq;
using Game.Core;
using Game.Levels;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// End-to-end: level files on disk to a <see cref="Board"/>. Edit Mode tests run with the project root as the
    /// working directory, so paths are relative to it.
    /// </summary>
    public class LevelLoaderTests
    {
        private const string SampleDir = "Assets/Tests/EditMode/LevelSamples";

        private static LevelLoadResult LoadFile(string path)
        {
            return LevelLoader.Load(File.ReadAllText(path), Path.GetFileName(path));
        }

        private static LevelLoadResult LoadSample(string name) => LoadFile($"{SampleDir}/{name}.json");

        private static void AssertSingleError(LevelLoadResult result, LevelErrorCode code, string message)
        {
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Board);
            var errors = string.Join("\n", result.Errors.Select(e => e.Message));
            Assert.AreEqual(1, result.Errors.Count, errors);
            Assert.AreEqual(code, result.Errors[0].Code);
            Assert.AreEqual(message, result.Errors[0].Message);
        }

        // ---------- The real level ----------

        [Test]
        public void Load_World1Level1_BuildsBoard()
        {
            var result = LoadFile("Assets/Levels/World1/w1_l001.json");

            CollectionAssert.IsEmpty(result.Errors.Select(e => e.Message));
            Assert.IsTrue(result.Success);
            Assert.AreEqual("w1_l001", result.Level.Id);
            Assert.AreEqual("match3", result.Level.Tutorial);

            var board = result.Board;
            Assert.AreEqual(4, board.ShelfCount);
            Assert.AreEqual(9, board.ItemCount);
            Assert.AreEqual("s1", board.GetShelf(0).Id);
            Assert.AreEqual("tumbler", board.GetShelf(0).GetFrontItem(0));
            Assert.IsNull(board.GetShelf(0).GetFrontItem(2));
            Assert.AreEqual("masala_dabba", board.GetShelf(3).GetFrontItem(0));
            Assert.IsFalse(board.IsWon);
            Assert.IsTrue(board.HasLegalMove());
        }

        [Test]
        public void Load_World1Level1_TumblerToShelf1Clears()
        {
            var board = LoadFile("Assets/Levels/World1/w1_l001.json").Board;
            int clears = 0;
            board.Cleared += c => clears++;

            Assert.IsTrue(board.TryMove(1, 1, 0));
            Assert.AreEqual(1, clears);
            Assert.AreEqual(6, board.ItemCount);
        }

        [Test]
        public void Load_EveryInvalidSample_NamesItsLevelIdInEveryError()
        {
            foreach (var path in Directory.GetFiles(SampleDir, "invalid_*.json"))
            {
                var result = LoadFile(path);
                Assert.IsFalse(result.Success, path);
                CollectionAssert.IsNotEmpty(result.Errors, path);
                foreach (var error in result.Errors)
                {
                    StringAssert.StartsWith("sample_", error.Message, path);
                    Assert.AreEqual(result.Level.Id, error.LevelId, path);
                }
            }
        }

        // ---------- Valid sample ----------

        [Test]
        public void Load_ValidSample_BuildsBoardWithLayers()
        {
            var result = LoadSample("valid");

            CollectionAssert.IsEmpty(result.Errors.Select(e => e.Message));
            var board = result.Board;
            Assert.AreEqual(4, board.ShelfCount);
            Assert.AreEqual(15, board.ItemCount);
            Assert.AreEqual(30, result.Level.MoveLimit);

            Assert.AreEqual(2, board.GetShelf(0).HiddenLayerCount);
            Assert.AreEqual("chai_kettle", board.GetShelf(0).GetHiddenItem(1, 0));
            // s3's empty front row is filled from the layer behind when the board is built.
            Assert.AreEqual(0, board.GetShelf(2).HiddenLayerCount);
            Assert.AreEqual("tumbler", board.GetShelf(2).GetFrontItem(0));
        }

        // ---------- Invalid samples (issue #3 acceptance criteria) ----------

        [Test]
        public void Load_WrongSlotCount_IsRejected()
        {
            AssertSingleError(LoadSample("invalid_wrong_slot_count"), LevelErrorCode.WrongSlotCount,
                "sample_wrong_slot_count: shelf 3 (\"s4\"), layer 0: has 4 slots; every layer needs exactly 3");
        }

        [Test]
        public void Load_ItemCountNotMultipleOfThree_IsRejected()
        {
            AssertSingleError(LoadSample("invalid_item_count"), LevelErrorCode.ItemCountNotMultipleOfThree,
                "sample_item_count: object \"tumbler\" appears 4 times; every object must appear a multiple of 3 times");
        }

        [Test]
        public void Load_UnknownObjectId_IsRejected()
        {
            AssertSingleError(LoadSample("invalid_unknown_object"), LevelErrorCode.UnknownObjectId,
                "sample_unknown_object: shelf 3 (\"s4\"), layer 1, slot 0: unknown object id \"dabba\"");
        }

        [Test]
        public void Load_NoEmptyFrontSlot_IsRejected()
        {
            AssertSingleError(LoadSample("invalid_no_empty_front_slot"), LevelErrorCode.NoEmptyFrontSlot,
                "sample_no_empty_front_slot: no empty front slot at start; at least one front slot must be empty so the first move is possible");
        }

        // ---------- Starting triples (Muthu's rule on #3) ----------

        [Test]
        public void Load_StartingTripleInFrontRow_IsRejected()
        {
            AssertSingleError(LoadSample("invalid_starting_triple_front"), LevelErrorCode.StartingTriple,
                "sample_starting_triple_front: shelf 0 (\"s1\"), layer 0: starting front row is a ready-made triple of \"tumbler\"; it would clear before the first move");
        }

        [Test]
        public void Load_StartingTripleRevealedBehindEmptyFront_IsRejected()
        {
            AssertSingleError(LoadSample("invalid_starting_triple_revealed"), LevelErrorCode.StartingTriple,
                "sample_starting_triple_revealed: shelf 0 (\"s1\"), layer 2: front row is empty, so layer 2 moves forward at start and is a ready-made triple of \"tumbler\"");
        }

        // ---------- Other failures ----------

        [Test]
        public void Load_MalformedJson_ReturnsErrorNamedAfterSource()
        {
            var result = LevelLoader.Load("{ \"schema\": 1, ", "w1_l099.json");
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Level);
            Assert.AreEqual(LevelErrorCode.MalformedJson, result.Errors.Single().Code);
            StringAssert.StartsWith("w1_l099.json: malformed JSON: ", result.Errors[0].Message);
            Assert.AreEqual(1, result.Errors[0].Line);
        }

        [Test]
        public void Load_WrongFieldType_StopsBeforeValidation()
        {
            var result = LevelLoader.Load("{ \"schema\": 1, \"id\": \"w1_l098\", \"shelves\": [ { \"layers\": [[1, null, null]] } ] }");
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Level);
            Assert.AreEqual(LevelErrorCode.WrongType, result.Errors.Single().Code);
        }

        [Test]
        public void Load_ValidationErrors_KeepParsedLevel()
        {
            var result = LevelLoader.Load("{ \"schema\": 2, \"id\": \"w1_l050\", \"shelves\": [] }");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("w1_l050", result.Level.Id);
            CollectionAssert.AreEqual(
                new[] { LevelErrorCode.UnsupportedSchema, LevelErrorCode.MissingShelves },
                result.Errors.Select(e => e.Code).ToArray());
        }

        [Test]
        public void Load_Null_ThrowsArgumentNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => LevelLoader.Load(null));
        }
    }
}
