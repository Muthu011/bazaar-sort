using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class GameRulesTests
    {
        [Test]
        public void MatchCount_IsThree()
        {
            Assert.AreEqual(3, GameRules.MatchCount);
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        public void IsMatch_OnlyTrueForExactlyMatchCount(int count, bool expected)
        {
            Assert.AreEqual(expected, GameRules.IsMatch(count));
        }
    }
}
