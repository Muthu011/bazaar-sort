using System.Collections.Generic;
using System.Linq;
using Game.Levels;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class LevelValidatorTests
    {
        private const string Id = "t_level";

        /// <summary>Validates a level with the given <c>shelves</c> array body and optional extra top-level fields.</summary>
        private static IReadOnlyList<LevelError> ValidateShelves(string shelves, string extraFields = "")
        {
            string json = $"{{ \"schema\": 1, \"id\": \"{Id}\", {extraFields} \"shelves\": [ {shelves} ] }}";
            return LevelValidator.Validate(LevelParser.Parse(json));
        }

        private static string Shelf(string id, params string[] layers)
        {
            return $"{{ \"id\": \"{id}\", \"kind\": \"normal\", \"layers\": [ {string.Join(", ", layers)} ] }}";
        }

        private static LevelErrorCode[] Codes(IReadOnlyList<LevelError> errors) => errors.Select(e => e.Code).ToArray();

        // A minimal valid level: three tumblers spread over two shelves, one free slot.
        private static readonly string ValidShelves =
            Shelf("s1", "[\"tumbler\", \"tumbler\", null]") + ", " + Shelf("s2", "[\"tumbler\", null, null]");

        // ---------- Valid ----------

        [Test]
        public void Validate_MinimalLevel_HasNoErrors()
        {
            CollectionAssert.IsEmpty(ValidateShelves(ValidShelves));
        }

        [Test]
        public void Validate_TripleInDeeperHiddenLayer_IsAllowed()
        {
            var errors = ValidateShelves(
                Shelf("s1", "[\"tawa\", \"tawa\", null]", "[\"tumbler\", \"tumbler\", \"tumbler\"]", "[\"tawa\", null, null]"));
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void Validate_TripleTwoLayersBehindEmptyFrontButBehindNonEmptyLayer_IsAllowed()
        {
            // Layer 1 moves forward at start; the triple in layer 2 only shows up later, and then it clears.
            var errors = ValidateShelves(
                Shelf("s1", "[null, null, null]", "[\"tawa\", \"tawa\", null]", "[\"tumbler\", \"tumbler\", \"tumbler\"]") + ", " +
                Shelf("s2", "[\"tawa\", null, null]"));
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void Validate_EmptyFrontAndHiddenLayerCountsAsFullWhenTheLayerIsFull()
        {
            // s1's front is empty but layer 1 (full) moves forward, so the board starts with no free slot.
            var errors = ValidateShelves(
                Shelf("s1", "[null, null, null]", "[\"tumbler\", \"tawa\", \"tumbler\"]") + ", " +
                Shelf("s2", "[\"tawa\", \"tumbler\", \"tawa\"]"));
            CollectionAssert.AreEqual(new[] { LevelErrorCode.NoEmptyFrontSlot }, Codes(errors));
        }

        [Test]
        public void Validate_WholeShelfEmpty_ProvidesFreeSlots()
        {
            var errors = ValidateShelves(
                Shelf("s1", "[\"tumbler\", \"tumbler\", \"tawa\"]") + ", " +
                Shelf("s2", "[\"tawa\", \"tumbler\", \"tawa\"]") + ", " +
                Shelf("s3", "[null, null, null]", "[null, null, null]"));
            CollectionAssert.IsEmpty(errors);
        }

        // ---------- Schema, id, shelves ----------

        [Test]
        public void Validate_SchemaNotOne_IsError()
        {
            var level = LevelParser.Parse($"{{ \"schema\": 2, \"id\": \"{Id}\", \"shelves\": [ {ValidShelves} ] }}");
            var errors = LevelValidator.Validate(level);
            CollectionAssert.AreEqual(new[] { LevelErrorCode.UnsupportedSchema }, Codes(errors));
            Assert.AreEqual("t_level: schema 2 is not supported; expected 1", errors[0].Message);
        }

        [Test]
        public void Validate_SchemaMissing_IsError()
        {
            var level = LevelParser.Parse($"{{ \"id\": \"{Id}\", \"shelves\": [ {ValidShelves} ] }}");
            var errors = LevelValidator.Validate(level);
            Assert.AreEqual("t_level: field \"schema\" is missing; expected 1", errors.Single().Message);
        }

        [Test]
        public void Validate_IdMissing_IsErrorNamedAfterSource()
        {
            var level = LevelParser.Parse($"{{ \"schema\": 1, \"shelves\": [ {ValidShelves} ] }}");
            var errors = LevelValidator.Validate(level, "w1_l004.json");
            Assert.AreEqual("w1_l004.json: field \"id\" is missing or empty", errors.Single().Message);
            Assert.AreEqual(LevelErrorCode.MissingLevelId, errors[0].Code);
        }

        [Test]
        public void Validate_ShelvesMissing_IsError()
        {
            var errors = LevelValidator.Validate(LevelParser.Parse($"{{ \"schema\": 1, \"id\": \"{Id}\" }}"));
            Assert.AreEqual(LevelErrorCode.MissingShelves, errors.Single().Code);
            Assert.AreEqual("t_level: field \"shelves\" is missing; a level needs at least one shelf", errors[0].Message);
        }

        [Test]
        public void Validate_ShelvesEmpty_IsError()
        {
            var errors = ValidateShelves(string.Empty);
            Assert.AreEqual(LevelErrorCode.MissingShelves, errors.Single().Code);
            StringAssert.Contains("\"shelves\" is empty", errors[0].Message);
        }

        [Test]
        public void Validate_ShelfWithoutLayers_IsError()
        {
            var errors = ValidateShelves(ValidShelves + ", { \"id\": \"s3\" }, " + Shelf("s4"));
            CollectionAssert.AreEqual(new[] { LevelErrorCode.MissingLayers, LevelErrorCode.MissingLayers }, Codes(errors));
            Assert.AreEqual("t_level: shelf 2 (\"s3\"): has no layers; a shelf needs at least a front row", errors[0].Message);
            Assert.AreEqual(3, errors[1].ShelfIndex);
        }

        // ---------- Slot count ----------

        [Test]
        public void Validate_WrongSlotCount_ReportsShelfAndLayer()
        {
            var errors = ValidateShelves(
                Shelf("s1", "[\"tumbler\", \"tumbler\", null]", "[\"tumbler\", null]") + ", " +
                Shelf("s2", "[null, null, null, null]"));

            CollectionAssert.AreEqual(new[] { LevelErrorCode.WrongSlotCount, LevelErrorCode.WrongSlotCount }, Codes(errors));
            Assert.AreEqual("t_level: shelf 0 (\"s1\"), layer 1: has 2 slots; every layer needs exactly 3", errors[0].Message);
            Assert.AreEqual("t_level: shelf 1 (\"s2\"), layer 0: has 4 slots; every layer needs exactly 3", errors[1].Message);
        }

        // ---------- Item counts ----------

        [Test]
        public void Validate_ItemCountNotMultipleOfThree_ReportsEachObject()
        {
            var errors = ValidateShelves(
                Shelf("s1", "[\"tumbler\", \"tumbler\", null]", "[\"tawa\", \"tumbler\", \"tumbler\"]") + ", " +
                Shelf("s2", "[\"tawa\", null, null]"));

            CollectionAssert.AreEqual(
                new[] { LevelErrorCode.ItemCountNotMultipleOfThree, LevelErrorCode.ItemCountNotMultipleOfThree }, Codes(errors));
            Assert.AreEqual(
                "t_level: object \"tumbler\" appears 4 times; every object must appear a multiple of 3 times", errors[0].Message);
            Assert.AreEqual("tumbler", errors[0].ObjectId);
            Assert.AreEqual("tawa", errors[1].ObjectId);
            Assert.IsNull(errors[0].ShelfIndex);
        }

        [Test]
        public void Validate_SixOfAnObject_IsFine()
        {
            var errors = ValidateShelves(
                Shelf("s1", "[\"tumbler\", \"tumbler\", null]", "[\"tumbler\", \"tawa\", \"tumbler\"]") + ", " +
                Shelf("s2", "[\"tumbler\", \"tawa\", null]", "[\"tawa\", \"tumbler\", null]"));
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void Validate_ObjectsUsedByTrays_AreExemptFromMultipleOfThree()
        {
            // Tray-specific counting isn't implemented yet; tray objects are just skipped.
            var errors = ValidateShelves(
                Shelf("s1", "[\"tumbler\", \"tumbler\", null]") + ", " + Shelf("s2", "[\"tumbler\", \"tawa\", null]"),
                "\"trays\": [ { \"id\": \"t1\", \"set\": [\"tawa\"] } ],");
            CollectionAssert.IsEmpty(errors);
        }

        // ---------- Object ids ----------

        [Test]
        public void Validate_UnknownObjectId_ReportsShelfLayerSlot()
        {
            var errors = ValidateShelves(
                Shelf("s1", "[\"tumbler\", \"tumbler\", null]", "[null, \"Tumbler\", null]") + ", " +
                Shelf("s2", "[\"tumbler\", null, null]"));

            var error = errors.Single();
            Assert.AreEqual(LevelErrorCode.UnknownObjectId, error.Code);
            Assert.AreEqual("t_level: shelf 0 (\"s1\"), layer 1, slot 1: unknown object id \"Tumbler\"", error.Message);
            Assert.AreEqual(0, error.ShelfIndex);
            Assert.AreEqual(1, error.Layer);
            Assert.AreEqual(1, error.Slot);
            Assert.AreEqual("Tumbler", error.ObjectId);
        }

        [Test]
        public void Validate_EmptyStringObjectId_IsUnknownWithHint()
        {
            var errors = ValidateShelves(ValidShelves + ", " + Shelf("s3", "[\"\", null, null]"));
            Assert.AreEqual(
                "t_level: shelf 2 (\"s3\"), layer 0, slot 0: empty object id \"\"; use null for an empty slot",
                errors.Single().Message);
        }

        [Test]
        public void Catalog_ContainsWorld1Ids()
        {
            CollectionAssert.AreEqual(new[]
            {
                "tumbler", "pressure_cooker", "idli_stand", "tawa", "masala_dabba",
                "steel_tiffin", "coconut_scraper", "brass_kudam", "rolling_pin", "chai_kettle",
            }, ObjectCatalog.World1);
            Assert.IsTrue(ObjectCatalog.IsKnown("masala_dabba"));
            Assert.IsFalse(ObjectCatalog.IsKnown("dabba"));
            Assert.IsFalse(ObjectCatalog.IsKnown(null));
        }

        // ---------- Empty front slot ----------

        [Test]
        public void Validate_NoEmptyFrontSlot_IsError()
        {
            var errors = ValidateShelves(
                Shelf("s1", "[\"tumbler\", \"tawa\", \"tumbler\"]") + ", " + Shelf("s2", "[\"tawa\", \"tumbler\", \"tawa\"]"));
            Assert.AreEqual(
                "t_level: no empty front slot at start; at least one front slot must be empty so the first move is possible",
                errors.Single().Message);
        }

        [Test]
        public void Validate_EmptySlotOnlyInHiddenLayer_DoesNotCount()
        {
            var errors = ValidateShelves(
                Shelf("s1", "[\"tumbler\", \"tawa\", \"tumbler\"]", "[\"chai_kettle\", null, null]") + ", " +
                Shelf("s2", "[\"tawa\", \"tumbler\", \"tawa\"]", "[\"chai_kettle\", \"chai_kettle\", null]"));
            CollectionAssert.AreEqual(new[] { LevelErrorCode.NoEmptyFrontSlot }, Codes(errors));
        }

        // ---------- Starting triples (Muthu's rule on #3) ----------

        [Test]
        public void Validate_FrontRowTriple_NamesLevelShelfAndItem()
        {
            var errors = ValidateShelves(
                ValidShelves + ", " + Shelf("s3", "[\"tawa\", \"tawa\", \"tawa\"]"));

            var error = errors.Single();
            Assert.AreEqual(LevelErrorCode.StartingTriple, error.Code);
            Assert.AreEqual(Id, error.LevelId);
            Assert.AreEqual(2, error.ShelfIndex);
            Assert.AreEqual(0, error.Layer);
            Assert.AreEqual("tawa", error.ObjectId);
            Assert.AreEqual(
                "t_level: shelf 2 (\"s3\"), layer 0: starting front row is a ready-made triple of \"tawa\"; it would clear before the first move",
                error.Message);
        }

        [Test]
        public void Validate_EmptyFrontWithTripleDirectlyBehind_IsError()
        {
            var errors = ValidateShelves(
                ValidShelves + ", " + Shelf("s3", "[null, null, null]", "[\"tawa\", \"tawa\", \"tawa\"]"));

            var error = errors.Single();
            Assert.AreEqual(LevelErrorCode.StartingTriple, error.Code);
            Assert.AreEqual(2, error.ShelfIndex);
            Assert.AreEqual(1, error.Layer);
            Assert.AreEqual("tawa", error.ObjectId);
            Assert.AreEqual(
                "t_level: shelf 2 (\"s3\"), layer 1: front row is empty, so layer 1 moves forward at start and is a ready-made triple of \"tawa\"",
                error.Message);
        }

        [Test]
        public void Validate_EmptyFrontAndEmptyLayersThenTriple_IsError()
        {
            var errors = ValidateShelves(
                ValidShelves + ", " + Shelf("s3", "[null, null, null]", "[null, null, null]", "[\"tawa\", \"tawa\", \"tawa\"]"));
            var error = errors.Single();
            Assert.AreEqual(LevelErrorCode.StartingTriple, error.Code);
            Assert.AreEqual(2, error.Layer);
        }

        [Test]
        public void Validate_TripleBehindNonEmptyFront_IsAllowed()
        {
            var errors = ValidateShelves(
                ValidShelves + ", " + Shelf("s3", "[\"tawa\", null, null]", "[\"tawa\", \"tawa\", \"tawa\"]", "[\"tawa\", \"tawa\", null]"));
            CollectionAssert.IsEmpty(errors);
        }

        // ---------- Collecting ----------

        [Test]
        public void Validate_CollectsAllErrors_NotJustTheFirst()
        {
            var level = LevelParser.Parse(
                "{ \"schema\": 3, \"id\": \"t_level\", \"shelves\": [ " +
                Shelf("s1", "[\"tumbler\", \"tumbler\", \"tumbler\"]", "[\"spoon\", null]") + ", " +
                Shelf("s2", "[\"tawa\", null, null]") + " ] }");
            var errors = LevelValidator.Validate(level);

            CollectionAssert.AreEqual(new[]
            {
                LevelErrorCode.UnsupportedSchema,
                LevelErrorCode.WrongSlotCount,
                LevelErrorCode.UnknownObjectId,
                LevelErrorCode.StartingTriple,
                LevelErrorCode.ItemCountNotMultipleOfThree,
            }, Codes(errors));
            // s1's front row is well formed, so its triple is reported even though a layer behind it is not.
        }

        [Test]
        public void Validate_MalformedLayerBeforeTheStartingFront_SkipsFreeSpaceAndTripleChecks()
        {
            // s1's front row is empty and the layer that would move forward has 2 slots, so the starting front row
            // is unknown. Only the slot count is reported; no guess about free slots or triples.
            var errors = ValidateShelves(
                Shelf("s1", "[null, null, null]", "[\"tawa\", \"tawa\"]", "[\"tawa\", null, null]") + ", " +
                Shelf("s2", "[\"tumbler\", \"tumbler\", \"tawa\"]") + ", " +
                Shelf("s3", "[\"tawa\", \"tumbler\", \"tawa\"]"));
            CollectionAssert.AreEqual(new[] { LevelErrorCode.WrongSlotCount }, Codes(errors));
        }

        [Test]
        public void Validate_UnknownShelfKind_IsError()
        {
            var errors = ValidateShelves(
                ValidShelves + ", { \"id\": \"s3\", \"kind\": \"magic\", \"layers\": [[null, null, null]] }");
            var error = errors.Single();
            Assert.AreEqual(LevelErrorCode.UnknownShelfKind, error.Code);
            Assert.AreEqual(
                "t_level: shelf 2 (\"s3\"): unknown shelf kind \"magic\"; expected normal, locked or conveyor", error.Message);
        }

        [Test]
        public void Validate_LockedAndConveyorKinds_AreAccepted()
        {
            var errors = ValidateShelves(
                "{ \"id\": \"s1\", \"kind\": \"locked\", \"lockClears\": 2, \"layers\": [[\"tumbler\", \"tumbler\", null]] }, " +
                "{ \"id\": \"s2\", \"kind\": \"conveyor\", \"layers\": [[\"tumbler\", null, null]] }");
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void Validate_StartingTripleOfUnknownId_ReportsBoth()
        {
            // Both are real problems; the board would also clear this row before the first move.
            var errors = ValidateShelves(ValidShelves + ", " + Shelf("s3", "[\"spoon\", \"spoon\", \"spoon\"]"));
            CollectionAssert.AreEqual(new[]
            {
                LevelErrorCode.UnknownObjectId, LevelErrorCode.UnknownObjectId, LevelErrorCode.UnknownObjectId,
                LevelErrorCode.StartingTriple,
            }, Codes(errors));
        }

        [Test]
        public void Validate_Null_ThrowsArgumentNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => LevelValidator.Validate(null));
        }

        [Test]
        public void Validate_HandBuiltDataWithNulls_DoesNotThrow()
        {
            var level = new LevelData
            {
                Schema = 1,
                Id = Id,
                Shelves = new List<ShelfData> { null, new ShelfData { Layers = new List<List<string>> { null } } },
                Trays = null,
            };
            CollectionAssert.AreEqual(
                new[] { LevelErrorCode.MissingLayers, LevelErrorCode.WrongSlotCount }, Codes(LevelValidator.Validate(level)));
        }
    }
}
