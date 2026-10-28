using System;
using System.Linq;
using Game.Levels;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class LevelParserTests
    {
        private const string FullLevel = @"{
            ""schema"": 1, ""id"": ""w1_l009"", ""world"": 1, ""index"": 9, ""role"": ""boss"",
            ""tutorial"": ""undo"", ""moveLimit"": 25, ""timeLimitSec"": null,
            ""shelves"": [
                { ""id"": ""s1"", ""kind"": ""locked"", ""lockClears"": 2,
                  ""layers"": [[""tumbler"", null, ""tawa""], [""tawa"", ""tawa"", ""tumbler""]] },
                { ""id"": ""s2"", ""kind"": ""normal"", ""lockClears"": 0, ""layers"": [[null, null, null]] }
            ],
            ""trays"": [ { ""id"": ""t1"", ""set"": [""idli_plate"", ""sambar_bowl""] } ],
            ""wrapped"": [""s1:1:0""],
            ""solver"": { ""solvable"": true, ""minMoves"": 4, ""hash"": ""abc"" },
            ""futureField"": { ""anything"": [1, 2, 3] }
        }";

        private static LevelFormatException ParseFails(string json, string sourceName = null)
        {
            return Assert.Throws<LevelFormatException>(() => LevelParser.Parse(json, sourceName));
        }

        // ---------- Reading fields ----------

        [Test]
        public void Parse_ReadsEveryField()
        {
            var level = LevelParser.Parse(FullLevel);

            Assert.AreEqual(1, level.Schema);
            Assert.AreEqual("w1_l009", level.Id);
            Assert.AreEqual(1, level.World);
            Assert.AreEqual(9, level.Index);
            Assert.AreEqual("boss", level.Role);
            Assert.AreEqual("undo", level.Tutorial);
            Assert.AreEqual(25, level.MoveLimit);
            Assert.IsNull(level.TimeLimitSec);

            Assert.AreEqual(2, level.Shelves.Count);
            var s1 = level.Shelves[0];
            Assert.AreEqual("s1", s1.Id);
            Assert.AreEqual("locked", s1.Kind);
            Assert.AreEqual(2, s1.LockClears);
            Assert.AreEqual(2, s1.Layers.Count);
            CollectionAssert.AreEqual(new[] { "tumbler", null, "tawa" }, s1.Layers[0]);
            CollectionAssert.AreEqual(new[] { "tawa", "tawa", "tumbler" }, s1.Layers[1]);
            CollectionAssert.AreEqual(new string[] { null, null, null }, level.Shelves[1].Layers[0]);

            Assert.AreEqual(1, level.Trays.Count);
            Assert.AreEqual("t1", level.Trays[0].Id);
            CollectionAssert.AreEqual(new[] { "idli_plate", "sambar_bowl" }, level.Trays[0].Set);
            CollectionAssert.AreEqual(new[] { "s1:1:0" }, level.Wrapped);
        }

        [Test]
        public void Parse_MissingOptionalFields_UseDefaults()
        {
            var level = LevelParser.Parse(@"{ ""schema"": 1, ""id"": ""x"", ""shelves"": [ { ""layers"": [] } ] }");

            Assert.IsNull(level.World);
            Assert.IsNull(level.Tutorial);
            Assert.IsNull(level.MoveLimit);
            Assert.AreEqual(0, level.Trays.Count);
            Assert.AreEqual(0, level.Wrapped.Count);
            Assert.IsNull(level.Shelves[0].Id);
            Assert.AreEqual("normal", level.Shelves[0].Kind);
            Assert.AreEqual(0, level.Shelves[0].LockClears);
            Assert.AreEqual(0, level.Shelves[0].Layers.Count);
        }

        [Test]
        public void Parse_MissingShelvesAndSchema_AreNullForTheValidator()
        {
            var level = LevelParser.Parse(@"{ ""id"": ""x"" }");
            Assert.IsNull(level.Schema);
            Assert.IsNull(level.Shelves);
        }

        [Test]
        public void Parse_WholeNumberWrittenAsDecimal_IsAccepted()
        {
            Assert.AreEqual(1, LevelParser.Parse(@"{ ""schema"": 1.0 }").Schema);
        }

        [Test]
        public void Parse_DateLikeString_StaysAString()
        {
            // Newtonsoft turns date-like strings into DateTime by default; the parser switches that off.
            var level = LevelParser.Parse(@"{ ""id"": ""2027-01-12T00:00:00"", ""role"": ""2027-01-12"" }");
            Assert.AreEqual("2027-01-12T00:00:00", level.Id);
            Assert.AreEqual("2027-01-12", level.Role);
        }

        [Test]
        public void Parse_Comments_AreIgnored()
        {
            var level = LevelParser.Parse("// level 1\n{ \"schema\": 1, /* inline */ \"id\": \"w1_l001\" }\n// end");
            Assert.AreEqual("w1_l001", level.Id);
        }

        [Test]
        public void Parse_Null_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => LevelParser.Parse(null));
        }

        // ---------- Malformed JSON ----------

        [Test]
        public void Parse_MalformedJson_ThrowsOneErrorWithLineAndPosition()
        {
            var e = ParseFails("{ \"schema\": 1,\n  \"id\": \"w1_l001\"\n  \"shelves\": [] }", "w1_l001.json");

            var error = e.Errors.Single();
            Assert.AreEqual(LevelErrorCode.MalformedJson, error.Code);
            Assert.AreEqual("w1_l001.json", error.LevelId);
            Assert.AreEqual(3, error.Line);
            Assert.AreEqual(2, error.Position);
            StringAssert.StartsWith("w1_l001.json: malformed JSON: ", error.Message);
            StringAssert.EndsWith(" (line 3, position 2)", error.Message);
            StringAssert.DoesNotContain("Path '", error.Message);
        }

        [Test]
        public void Parse_TruncatedJson_IsMalformed()
        {
            var e = ParseFails("{ \"schema\": 1, ", "w1_l099.json");
            Assert.AreEqual(LevelErrorCode.MalformedJson, e.Errors.Single().Code);
            StringAssert.StartsWith("w1_l099.json: malformed JSON: ", e.Errors[0].Message);
        }

        [Test]
        public void Parse_MalformedJsonWithoutSourceName_UsesPlaceholderName()
        {
            var e = ParseFails("[1,");
            StringAssert.StartsWith(LevelParser.NoIdName + ": malformed JSON", e.Errors.Single().Message);
        }

        [Test]
        public void Parse_EmptyText_IsMalformed()
        {
            var e = ParseFails("  \n ", "w1_l002.json");
            Assert.AreEqual("w1_l002.json: malformed JSON: the text is empty", e.Errors.Single().Message);
        }

        [Test]
        public void Parse_TrailingContent_IsMalformed()
        {
            var e = ParseFails("{ \"id\": \"a\" }\n{ \"id\": \"b\" }");
            Assert.AreEqual(LevelErrorCode.MalformedJson, e.Errors.Single().Code);
            Assert.AreEqual(2, e.Errors[0].Line);
        }

        [Test]
        public void Parse_DuplicateKey_IsMalformed()
        {
            var e = ParseFails("{ \"id\": \"a\", \"schema\": 1, \"schema\": 2 }");
            Assert.AreEqual(LevelErrorCode.MalformedJson, e.Errors.Single().Code);
            StringAssert.Contains("schema", e.Errors[0].Message);
        }

        // ---------- Wrong types ----------

        [Test]
        public void Parse_RootNotAnObject_IsWrongType()
        {
            var e = ParseFails("[]");
            Assert.AreEqual(LevelErrorCode.WrongType, e.Errors.Single().Code);
            StringAssert.Contains("must contain a JSON object, found an array", e.Errors[0].Message);
        }

        [Test]
        public void Parse_WrongTypes_CollectsEveryErrorWithLocation()
        {
            const string json = @"{
                ""schema"": ""1"", ""id"": ""w1_bad"", ""moveLimit"": 2.5,
                ""shelves"": [
                    { ""id"": ""s1"", ""layers"": [[""tumbler"", 5, null]] },
                    { ""id"": ""s2"", ""layers"": ""tumbler"" },
                    { ""id"": ""s3"", ""layers"": [ {""a"": 1} ] },
                    7
                ],
                ""wrapped"": [ 1 ]
            }";

            var e = ParseFails(json);

            Assert.IsTrue(e.Errors.All(x => x.Code == LevelErrorCode.WrongType));
            Assert.IsTrue(e.Errors.All(x => x.LevelId == "w1_bad"));
            Assert.IsTrue(e.Errors.All(x => x.Line.HasValue && x.Position.HasValue));
            CollectionAssert.AreEqual(new[]
            {
                "field \"schema\" must be a whole number, found string \"1\"",
                "field \"moveLimit\" must be a whole number, found number 2.5",
                "must be an object id string or null, found number 5",
                "field \"layers\" must be an array, found string \"tumbler\"",
                "must be an array of slots, found an object",
                "must be a JSON object, found number 7",
                "wrapped[0] must be a string like \"s3:1:0\", found number 1",
            }, e.Errors.Select(x => x.Detail).ToArray());

            var slotError = e.Errors[2];
            Assert.AreEqual(0, slotError.ShelfIndex);
            Assert.AreEqual("s1", slotError.ShelfId);
            Assert.AreEqual(0, slotError.Layer);
            Assert.AreEqual(1, slotError.Slot);
            Assert.AreEqual(4, slotError.Line);
            Assert.AreEqual(
                "w1_bad: shelf 0 (\"s1\"), layer 0, slot 1: must be an object id string or null, found number 5 (line 4, position 58)",
                slotError.Message);

            Assert.AreEqual("w1_bad: shelf 3: must be a JSON object, found number 7 (line 7, position 21)", e.Errors[5].Message);
        }

        [Test]
        public void Parse_NonStringId_IsWrongTypeAndFallsBackToSourceName()
        {
            var e = ParseFails(@"{ ""id"": 3 }", "w1_l003.json");
            Assert.AreEqual("w1_l003.json: field \"id\" must be a string, found number 3 (line 1, position 9)",
                e.Errors.Single().Message);
        }
    }
}
