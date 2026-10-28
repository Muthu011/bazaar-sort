using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ShelfTests
    {
        [Test]
        public void Shelf_HasThreeFrontSlots()
        {
            Assert.AreEqual(3, Shelf.SlotCount);
        }

        [Test]
        public void Constructor_FrontRowAndHiddenLayers_FromLevelData()
        {
            var shelf = new Shelf(
                new[] { "tumbler", null, "dabba" },
                new[] { "idli", "idli", "tawa" },
                new[] { "kudam", null, null });

            Assert.AreEqual("tumbler", shelf.GetFrontItem(0));
            Assert.IsNull(shelf.GetFrontItem(1));
            Assert.AreEqual("dabba", shelf.GetFrontItem(2));
            Assert.AreEqual(2, shelf.HiddenLayerCount);
            Assert.AreEqual("idli", shelf.GetHiddenItem(0, 0));
            Assert.AreEqual("tawa", shelf.GetHiddenItem(0, 2));
            Assert.AreEqual("kudam", shelf.GetHiddenItem(1, 0));
            Assert.AreEqual(2, shelf.FrontItemCount);
            Assert.AreEqual(6, shelf.TotalItemCount);
            Assert.AreEqual(1, shelf.FirstFreeFrontSlot);
            Assert.IsTrue(shelf.HasFreeFrontSlot);
        }

        [Test]
        public void Constructor_EmptyStringIsEmptySlot()
        {
            var shelf = new Shelf(new[] { "", "a", null });
            Assert.IsNull(shelf.GetFrontItem(0));
            Assert.AreEqual(1, shelf.FrontItemCount);
        }

        [Test]
        public void Constructor_CopiesInput()
        {
            var front = new[] { "a", null, null };
            var shelf = new Shelf(front);
            front[0] = "b";
            Assert.AreEqual("a", shelf.GetFrontItem(0));
        }

        [Test]
        public void Constructor_KeepsId()
        {
            var shelf = new Shelf(new[] { new[] { "a", null, null } }, "s1");
            Assert.AreEqual("s1", shelf.Id);
        }

        [Test]
        public void Constructor_RejectsWrongSlotCount()
        {
            Assert.Throws<ArgumentException>(() => new Shelf(new[] { "a", "b" }));
            Assert.Throws<ArgumentException>(() => new Shelf(new[] { "a", null, null }, new[] { "a", "b", "c", "d" }));
        }

        [Test]
        public void Constructor_RejectsNoLayersOrNull()
        {
            Assert.Throws<ArgumentException>(() => new Shelf(new string[0][]));
            Assert.Throws<ArgumentNullException>(() => new Shelf((string[][])null));
            Assert.Throws<ArgumentException>(() => new Shelf(new[] { "a", null, null }, null));
        }

        [Test]
        public void FullFrontRow_HasNoFreeSlot()
        {
            var shelf = new Shelf(new[] { "a", "b", "c" });
            Assert.IsFalse(shelf.HasFreeFrontSlot);
            Assert.AreEqual(-1, shelf.FirstFreeFrontSlot);
        }

        [Test]
        public void Getters_RejectOutOfRange()
        {
            var shelf = new Shelf(new[] { "a", null, null }, new[] { "b", null, null });
            Assert.Throws<ArgumentOutOfRangeException>(() => shelf.GetFrontItem(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => shelf.GetFrontItem(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => shelf.GetHiddenItem(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => shelf.GetHiddenItem(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => shelf.GetHiddenItem(0, 3));
        }
    }
}
