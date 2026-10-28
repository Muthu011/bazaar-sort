using System;
using Game.View;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="LevelSequence"/>: TryGetNext walks the list in order and stops after the last level (no loop back to
    /// the first; Muthu, PR #17).
    /// </summary>
    public class LevelSequenceTests
    {
        private static int NextOf(int current, int count)
        {
            Assert.IsTrue(LevelSequence.TryGetNext(current, count, out int next), $"{current} of {count} has a next level");
            return next;
        }

        [Test]
        public void TryGetNext_WalksWorld1InOrder()
        {
            Assert.AreEqual(1, NextOf(0, 5));
            Assert.AreEqual(2, NextOf(1, 5));
            Assert.AreEqual(3, NextOf(2, 5));
            Assert.AreEqual(4, NextOf(3, 5));
            Assert.IsTrue(LevelSequence.HasNext(3, 5));
        }

        [Test]
        public void TryGetNext_AfterTheLastLevel_HasNoNext_AndNeverLoops()
        {
            Assert.IsFalse(LevelSequence.TryGetNext(4, 5, out int next), "L5 of 5 is the last level.");
            Assert.AreEqual(-1, next, "No index, in particular not the first level.");
            Assert.IsFalse(LevelSequence.HasNext(4, 5));
            Assert.IsFalse(LevelSequence.TryGetNext(0, 1, out next), "A one-level list has no next level.");
            Assert.AreEqual(-1, next);
        }

        [Test]
        public void First_IsIndexZero()
        {
            Assert.AreEqual(0, LevelSequence.First);
        }

        [Test]
        public void TryGetNext_FromNoLevel_StartsAtTheFirst()
        {
            Assert.AreEqual(0, NextOf(-1, 5));
            Assert.AreEqual(0, NextOf(7, 5));
        }

        [Test]
        public void TryGetNext_WithNoLevels_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LevelSequence.TryGetNext(0, 0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => LevelSequence.HasNext(0, 0));
        }
    }
}
