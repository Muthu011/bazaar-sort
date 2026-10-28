using System;
using Game.Solver;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>Writing only the solver block into level JSON, and the win-rate bands (issue #20).</summary>
    public class SolverBlockWriterTests
    {
        private const string WithoutBlock =
            "{\n" +
            "  \"schema\": 1,\n" +
            "  \"id\": \"w1_l999\",\n" +
            "  \"shelves\": [\n" +
            "    { \"id\": \"s1\", \"kind\": \"normal\", \"lockClears\": 0, \"layers\": [[\"tumbler\", null, null]] }\n" +
            "  ],\n" +
            "  \"trays\": [],\n" +
            "  \"wrapped\": []\n" +
            "}\n";

        private static readonly SolverBlock Block = new SolverBlock(true, 8, 0.0166, 0.8824, "e1e252aa25fc4f30");

        [Test]
        public void ToJson_IsOneLineWithRatesToThreeDecimals()
        {
            Assert.AreEqual(
                "{ \"solvable\": true, \"minMoves\": 8, \"deadEndRate\": 0.017, \"estWinRate\": 0.882, \"hash\": \"e1e252aa25fc4f30\" }",
                Block.ToJson());
            StringAssert.Contains("\"deadEndRate\": 0.0,", new SolverBlock(true, 1, 0, 1, "x").ToJson());
            StringAssert.Contains("\"estWinRate\": 1.0,", new SolverBlock(true, 1, 0, 1, "x").ToJson());
        }

        [Test]
        public void Write_MissingBlock_AppendsItAsTheLastFieldAndKeepsTheRest()
        {
            string written = SolverBlockWriter.Write(WithoutBlock, Block);

            string expected = WithoutBlock.Replace("  \"wrapped\": []\n}",
                "  \"wrapped\": [],\n  \"solver\": " + Block.ToJson() + "\n}");
            Assert.AreEqual(expected, written);
            Assert.AreEqual(Block, SolverBlockWriter.Read(written));
        }

        [Test]
        public void Write_ExistingBlock_ReplacesOnlyThatBlock()
        {
            string first = SolverBlockWriter.Write(WithoutBlock, Block);
            var updated = new SolverBlock(false, -1, 1, 0, "0123456789abcdef");

            string second = SolverBlockWriter.Write(first, updated);

            int at = first.IndexOf("\"solver\"", StringComparison.Ordinal);
            Assert.AreEqual(first.Substring(0, at), second.Substring(0, at));
            Assert.IsTrue(second.EndsWith("\"hash\": \"0123456789abcdef\" }\n}\n", StringComparison.Ordinal));
            Assert.AreEqual(updated, SolverBlockWriter.Read(second));
        }

        [Test]
        public void Write_SameBlockTwice_IsIdempotent()
        {
            string once = SolverBlockWriter.Write(WithoutBlock, Block);
            Assert.AreEqual(once, SolverBlockWriter.Write(once, Block));
        }

        [Test]
        public void Read_FileWithoutBlock_ReturnsNull()
        {
            Assert.IsNull(SolverBlockWriter.Read(WithoutBlock));
        }

        [TestCase(1, "easy", "levels 1-10")]
        [TestCase(6, "breather", "levels 1-10")]
        [TestCase(7, "medium", "levels 7 and 10")]
        [TestCase(9, "hard", "levels 1-10")]
        [TestCase(10, "boss", "levels 7 and 10")]
        [TestCase(11, "easy", "easy")]
        [TestCase(12, "medium", "medium")]
        [TestCase(15, "hard", "hard")]
        [TestCase(16, "breather", "breather")]
        [TestCase(20, "boss", "boss")]
        public void Bands_MapIndexAndRole(int index, string role, string band)
        {
            Assert.IsTrue(DifficultyBands.TryGetBand(index, role, out var found));
            Assert.AreEqual(band, found.Name);
        }

        [Test]
        public void Bands_TargetsFromLevelDesigner()
        {
            Assert.AreEqual(0.90, DifficultyBands.Onboarding.Min);
            Assert.AreEqual(0.70, DifficultyBands.Medium.Min);
            Assert.AreEqual(0.85, DifficultyBands.Medium.Max);
            Assert.AreEqual(0.35, DifficultyBands.Hard.Min);
            Assert.AreEqual(0.50, DifficultyBands.Hard.Max);
            Assert.AreEqual(DifficultyBands.Hard.Min, DifficultyBands.Boss.Min);
            Assert.AreEqual(DifficultyBands.Hard.Max, DifficultyBands.Boss.Max);
            Assert.IsTrue(DifficultyBands.Onboarding.Contains(0.90));
            Assert.IsFalse(DifficultyBands.Onboarding.Contains(0.899));
            Assert.AreEqual(0.75, DifficultyBands.OnboardingPeak.Min);
            Assert.AreEqual(1.00, DifficultyBands.OnboardingPeak.Max);
            Assert.IsFalse(DifficultyBands.OnboardingPeak.Contains(0.749));
        }

        [Test]
        public void Bands_UnknownRoleAfterLevelTen_HasNoBand()
        {
            Assert.IsFalse(DifficultyBands.TryGetBand(11, "tutorial", out _));
            Assert.IsFalse(DifficultyBands.TryGetBand(11, null, out _));
        }
    }
}
