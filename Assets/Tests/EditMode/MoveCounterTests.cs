using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="MoveCounter"/> (issue #44, Muthu on PR #63): moves for the star rule only ever go up. Undone moves
    /// still count, so Undo (#53) must not lower the count; these tests fail if a way to do so is added.
    /// </summary>
    public class MoveCounterTests
    {
        [Test]
        public void Counts_EveryMove()
        {
            var moves = new MoveCounter();
            Assert.AreEqual(0, moves.Value);
            moves.Count();
            moves.Count();
            moves.Count();
            Assert.AreEqual(3, moves.Value);
        }

        [Test]
        public void Reset_IsTheOnlyWayBack_AndGoesToZero()
        {
            var moves = new MoveCounter();
            moves.Count();
            moves.Count();
            moves.Reset();
            Assert.AreEqual(0, moves.Value);
        }

        [Test]
        public void HasNoDecrement_OnlyCountAndReset()
        {
            // The whole public surface: a getter, Count and Reset. A new member (Undo, Decrement, a setter...) fails
            // here, so whoever adds Undo (#53) meets the rule "undone moves still count" first.
            var type = typeof(MoveCounter);
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).Select(m => m.Name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "Count", "Reset" }, methods);
            var value = type.GetProperty(nameof(MoveCounter.Value));
            Assert.IsNotNull(value);
            Assert.IsTrue(value.SetMethod == null || !value.SetMethod.IsPublic, "No public setter.");
            Assert.AreEqual(1, type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length);
        }

        [Test]
        public void Count_HoldsAtMax_InsteadOfWrappingNegative()
        {
            var moves = new MoveCounter();
            typeof(MoveCounter).GetProperty(nameof(MoveCounter.Value)).SetValue(moves, int.MaxValue);
            moves.Count();
            Assert.AreEqual(int.MaxValue, moves.Value);
        }

        [Test]
        public void OnABoard_CountsMoves_AndNotCancels()
        {
            var board = new Board(
                new Shelf(new[] { "tumbler", "tumbler", null }),
                new Shelf(new[] { "tumbler", "tawa", null }),
                new Shelf(new[] { "tawa", "tawa", null }));
            var moves = new MoveCounter();
            board.Moved += _ => moves.Count();
            Assert.IsFalse(board.TryMove(0, 0, 0), "A drop back on the same shelf is a cancel.");
            Assert.AreEqual(0, moves.Value);
            Assert.IsTrue(board.TryMove(1, 1, 2));
            Assert.AreEqual(1, moves.Value);
        }
    }
}
