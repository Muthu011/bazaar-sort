using System;
using Game.View;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary><see cref="LevelSequence"/>: Next walks the list and loops after the last level.</summary>
    public class LevelSequenceTests
    {
        [Test]
        public void Next_WalksWorld1InOrder()
        {
            Assert.AreEqual(1, LevelSequence.Next(0, 5));
            Assert.AreEqual(2, LevelSequence.Next(1, 5));
            Assert.AreEqual(3, LevelSequence.Next(2, 5));
            Assert.AreEqual(4, LevelSequence.Next(3, 5));
        }

        [Test]
        public void Next_AfterTheLastLevel_LoopsToTheFirst()
        {
            Assert.AreEqual(0, LevelSequence.Next(4, 5));
            Assert.AreEqual(0, LevelSequence.Next(0, 1));
        }

        [Test]
        public void Next_FromNoLevel_StartsAtTheFirst()
        {
            Assert.AreEqual(0, LevelSequence.Next(-1, 5));
            Assert.AreEqual(0, LevelSequence.Next(7, 5));
        }

        [Test]
        public void Next_WithNoLevels_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LevelSequence.Next(0, 0));
        }
    }
}
