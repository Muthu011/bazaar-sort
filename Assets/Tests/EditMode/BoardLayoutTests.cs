using System.Collections.Generic;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="BoardLayout"/>: every shelf and tile stays inside the safe area, nothing overlaps, for 4..14
    /// shelves on 16:9 and 20:9 portrait phones (with and without notch insets).
    /// </summary>
    public class BoardLayoutTests
    {
        private const float Epsilon = 0.01f;

        private static IEnumerable<TestCaseData> Screens()
        {
            // name, screen width, screen height, safe-area insets (left, bottom, right, top)
            yield return new TestCaseData("16:9 1080x1920", 1080, 1920, Vector4.zero).SetName("{0}");
            yield return new TestCaseData("20:9 1080x2400", 1080, 2400, Vector4.zero).SetName("{0}");
            yield return new TestCaseData("16:9 720x1280", 720, 1280, Vector4.zero).SetName("{0}");
            yield return new TestCaseData("20:9 720x1600", 720, 1600, Vector4.zero).SetName("{0}");
            yield return new TestCaseData("20:9 1080x2400 notch", 1080, 2400, new Vector4(0, 63, 0, 110)).SetName("{0}");
        }

        private static Rect SafeArea(int width, int height, Vector4 insets)
        {
            return new Rect(insets.x, insets.y, width - insets.x - insets.z, height - insets.y - insets.w);
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - Epsilon && inner.xMax <= outer.xMax + Epsilon
                && inner.yMin >= outer.yMin - Epsilon && inner.yMax <= outer.yMax + Epsilon;
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            return a.xMin < b.xMax - Epsilon && b.xMin < a.xMax - Epsilon
                && a.yMin < b.yMax - Epsilon && b.yMin < a.yMax - Epsilon;
        }

        [TestCaseSource(nameof(Screens))]
        public void AllShelfCounts_FitInsideSafeAreaWithoutOverlap(string name, int width, int height, Vector4 insets)
        {
            var safe = SafeArea(width, height, insets);
            var screen = new Rect(0, 0, width, height);

            for (int count = 4; count <= 14; count++)
            {
                var layout = BoardLayout.Compute(count, Shelf.SlotCount, safe, BoardLayoutSettings.Default);
                string context = $"{name}, {count} shelves";

                Assert.AreEqual(count, layout.ShelfCount, context);
                Assert.Greater(layout.TileSize, 0f, context);
                Assert.IsTrue(Contains(safe, layout.Playfield), $"{context}: playfield {layout.Playfield} outside safe area {safe}");

                for (int i = 0; i < count; i++)
                {
                    var shelf = layout.GetShelfRect(i);
                    Assert.IsTrue(Contains(layout.Playfield, shelf), $"{context}: shelf {i} {shelf} outside playfield {layout.Playfield}");
                    Assert.IsTrue(Contains(safe, shelf), $"{context}: shelf {i} {shelf} outside safe area");
                    Assert.IsTrue(Contains(screen, shelf), $"{context}: shelf {i} {shelf} off-screen");

                    for (int j = i + 1; j < count; j++)
                    {
                        Assert.IsFalse(Overlaps(shelf, layout.GetShelfRect(j)), $"{context}: shelves {i} and {j} overlap");
                    }

                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        var front = layout.GetFrontSlotRect(i, slot);
                        var hidden = layout.GetHiddenSlotRect(i, slot);
                        Assert.IsTrue(Contains(shelf, front), $"{context}: shelf {i} front slot {slot} {front} outside shelf {shelf}");
                        Assert.IsTrue(Contains(shelf, hidden), $"{context}: shelf {i} hidden slot {slot} {hidden} outside shelf {shelf}");
                        Assert.IsFalse(Overlaps(front, hidden), $"{context}: shelf {i} slot {slot} front and hidden tiles overlap");
                        for (int other = slot + 1; other < Shelf.SlotCount; other++)
                        {
                            Assert.IsFalse(Overlaps(front, layout.GetFrontSlotRect(i, other)), $"{context}: shelf {i} front slots {slot} and {other} overlap");
                        }
                    }
                }
            }
        }

        [TestCaseSource(nameof(Screens))]
        public void TileSize_ShrinksOrStays_AsShelvesAreAdded(string name, int width, int height, Vector4 insets)
        {
            var safe = SafeArea(width, height, insets);
            float previous = float.MaxValue;
            for (int count = 4; count <= 14; count++)
            {
                float tile = BoardLayout.Compute(count, Shelf.SlotCount, safe, BoardLayoutSettings.Default).TileSize;
                Assert.LessOrEqual(tile, previous + Epsilon, $"{name}: {count} shelves got bigger tiles than {count - 1}");
                previous = tile;
            }
        }

        [TestCaseSource(nameof(Screens))]
        public void HitTargets_AreAtLeast48Px_ForEveryShelfCount(string name, int width, int height, Vector4 insets)
        {
            var safe = SafeArea(width, height, insets);
            for (int count = 4; count <= 14; count++)
            {
                var layout = BoardLayout.Compute(count, Shelf.SlotCount, safe, BoardLayoutSettings.Default);
                for (int i = 0; i < count; i++)
                {
                    string context = $"{name}, {count} shelves, shelf {i}";
                    var shelfHit = layout.GetShelfHitRect(i);
                    Assert.GreaterOrEqual(shelfHit.width, 48f, $"{context}: drop target width");
                    Assert.GreaterOrEqual(shelfHit.height, 48f, $"{context}: drop target height");
                    Assert.IsTrue(Contains(shelfHit, layout.GetShelfRect(i)), $"{context}: drop target covers the shelf");

                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        var slotHit = layout.GetSlotHitRect(i, slot);
                        Assert.GreaterOrEqual(slotHit.width, 48f, $"{context} slot {slot}: pick target width {slotHit.width}");
                        Assert.GreaterOrEqual(slotHit.height, 48f, $"{context} slot {slot}: pick target height {slotHit.height}");
                        Assert.IsTrue(Contains(slotHit, layout.GetFrontSlotRect(i, slot)), $"{context} slot {slot}: pick target covers the tile");
                        Assert.IsTrue(Contains(shelfHit, slotHit), $"{context} slot {slot}: pick target inside drop target");
                        for (int other = slot + 1; other < Shelf.SlotCount; other++)
                        {
                            Assert.IsFalse(Overlaps(slotHit, layout.GetSlotHitRect(i, other)), $"{context}: pick targets {slot} and {other} overlap");
                        }
                    }

                    for (int j = i + 1; j < count; j++)
                    {
                        Assert.IsFalse(Overlaps(shelfHit, layout.GetShelfHitRect(j)), $"{context}: drop targets {i} and {j} overlap");
                    }
                }
            }
        }

        [TestCaseSource(nameof(Screens))]
        public void HitTest_FindsEveryTile_AndMissesTheBackground(string name, int width, int height, Vector4 insets)
        {
            var safe = SafeArea(width, height, insets);
            for (int count = 4; count <= 14; count++)
            {
                var layout = BoardLayout.Compute(count, Shelf.SlotCount, safe, BoardLayoutSettings.Default);
                for (int i = 0; i < count; i++)
                {
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        var tile = layout.GetFrontSlotRect(i, slot);
                        Assert.AreEqual(BoardTarget.At(i, slot), layout.HitTest(tile.center), $"{name}, {count} shelves: tile centre");
                        Assert.AreEqual(BoardTarget.At(i, slot), layout.HitTest(layout.GetHiddenSlotRect(i, slot).center),
                            $"{name}, {count} shelves: the peeking hidden tile above counts as the same column");
                    }
                }

                // The HUD strip at the very top of the safe area is not a shelf.
                Assert.AreEqual(BoardTarget.None, layout.HitTest(new Vector2(safe.center.x, safe.yMax - 1f)), $"{name}, {count} shelves: HUD");
            }
        }

        [Test]
        public void HitTest_InGapBetweenRows_PicksNearestShelf()
        {
            var layout = BoardLayout.Compute(4, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default);
            Assert.AreEqual(1, layout.Columns, "Precondition: one column.");
            float x = layout.GetFrontSlotRect(0, 1).center.x;
            float gapTop = layout.GetShelfRect(0).yMin;
            float gapBottom = layout.GetShelfRect(1).yMax;
            Assert.AreEqual(0, layout.HitTest(new Vector2(x, gapTop - 1f)).Shelf, "Just below shelf 0");
            Assert.AreEqual(1, layout.HitTest(new Vector2(x, gapBottom + 1f)).Shelf, "Just above shelf 1");
        }

        [Test]
        public void FourShelves_On16By9_UseOneColumn()
        {
            var layout = BoardLayout.Compute(4, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default);
            Assert.AreEqual(1, layout.Columns);
            Assert.AreEqual(4, layout.Rows);
        }

        [Test]
        public void FourteenShelves_On16By9_UseTwoColumns()
        {
            var layout = BoardLayout.Compute(14, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default);
            Assert.AreEqual(2, layout.Columns);
            Assert.AreEqual(7, layout.Rows);
        }

        [Test]
        public void ShelvesFillRowsTopToBottom_LeftToRight()
        {
            var layout = BoardLayout.Compute(6, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default);
            Assert.AreEqual(2, layout.Columns, "Precondition: 6 shelves on 16:9 use two columns.");
            Assert.Less(layout.GetShelfRect(0).x, layout.GetShelfRect(1).x, "Shelf 1 is right of shelf 0.");
            Assert.AreEqual(layout.GetShelfRect(0).y, layout.GetShelfRect(1).y, Epsilon, "Shelves 0 and 1 share a row.");
            Assert.Greater(layout.GetShelfRect(0).y, layout.GetShelfRect(2).y, "Shelf 2 is below shelf 0.");
        }

        [Test]
        public void ShortLastRow_IsCentred()
        {
            var layout = BoardLayout.Compute(7, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default);
            Assert.AreEqual(2, layout.Columns, "Precondition: 7 shelves on 16:9 use two columns.");
            Assert.AreEqual(layout.Playfield.center.x, layout.GetShelfRect(6).center.x, Epsilon);
        }

        [Test]
        public void Grid_IsCentredInPlayfield()
        {
            var layout = BoardLayout.Compute(4, Shelf.SlotCount, new Rect(0, 0, 1080, 2400), BoardLayoutSettings.Default);
            float top = layout.GetShelfRect(0).yMax;
            float bottom = layout.GetShelfRect(3).yMin;
            Assert.AreEqual(layout.Playfield.center.y, (top + bottom) * 0.5f, Epsilon);
        }

        [Test]
        public void Playfield_KeepsHudAndBoosterReserves()
        {
            var safe = new Rect(0, 0, 1080, 1920);
            var settings = BoardLayoutSettings.Default;
            var layout = BoardLayout.Compute(8, Shelf.SlotCount, safe, settings);
            Assert.AreEqual(safe.yMax - safe.height * settings.TopReserveFraction, layout.Playfield.yMax, Epsilon);
            Assert.AreEqual(safe.yMin + safe.height * settings.BottomReserveFraction, layout.Playfield.yMin, Epsilon);
        }

        [Test]
        public void Compute_RejectsBadInput()
        {
            var safe = new Rect(0, 0, 1080, 1920);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BoardLayout.Compute(0, 3, safe, BoardLayoutSettings.Default));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BoardLayout.Compute(4, 0, safe, BoardLayoutSettings.Default));
            Assert.Throws<System.ArgumentException>(() => BoardLayout.Compute(4, 3, new Rect(0, 0, 0, 100), BoardLayoutSettings.Default));
        }
    }
}
