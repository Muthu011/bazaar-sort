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

        // Item touch areas in dp (issue #5 review): 48 dp wherever the gaps allow, 44 dp floor on 720p phones.
        private static IEnumerable<TestCaseData> DensityScreens()
        {
            // name, width, height, density, fewest shelves, most shelves, minimum dp
            yield return new TestCaseData("16:9 720x1280 @2.0", 720, 1280, 2.0f, 4, 14, BoardLayout.FloorTouchDp).SetName("{0} 44dp");
            yield return new TestCaseData("20:9 720x1600 @2.0", 720, 1600, 2.0f, 4, 14, BoardLayout.FloorTouchDp).SetName("{0} 44dp");
            yield return new TestCaseData("16:9 1080x1920 @2.625", 1080, 1920, 2.625f, 4, 14, BoardLayout.TargetTouchDp).SetName("{0} 48dp");
            yield return new TestCaseData("20:9 1080x2400 @2.625", 1080, 2400, 2.625f, 4, 14, BoardLayout.TargetTouchDp).SetName("{0} 48dp");
            yield return new TestCaseData("16:9 1080x1920 @3.0", 1080, 1920, 3.0f, 4, 8, BoardLayout.TargetTouchDp).SetName("{0} 48dp");
            yield return new TestCaseData("20:9 1080x2400 @3.0", 1080, 2400, 3.0f, 4, 8, BoardLayout.TargetTouchDp).SetName("{0} 48dp");
        }

        [TestCaseSource(nameof(DensityScreens))]
        public void ItemTouchAreas_MeetDpMinimum(string name, int width, int height, float density, int fewest, int most, float minDp)
        {
            var safe = new Rect(0, 0, width, height);
            float smallest = float.MaxValue;
            for (int count = fewest; count <= most; count++)
            {
                var layout = BoardLayout.Compute(count, Shelf.SlotCount, safe, BoardLayoutSettings.Default, density);
                for (int i = 0; i < count; i++)
                {
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        var touch = layout.GetSlotHitRect(i, slot);
                        string context = $"{name}, {count} shelves, shelf {i} slot {slot}";
                        Assert.GreaterOrEqual(touch.width / density, minDp - Epsilon, $"{context}: width {touch.width} px = {touch.width / density} dp");
                        Assert.GreaterOrEqual(touch.height / density, minDp - Epsilon, $"{context}: height {touch.height} px = {touch.height / density} dp");
                        smallest = Mathf.Min(smallest, Mathf.Min(touch.width, touch.height));
                    }
                }
            }
            TestContext.WriteLine($"{name}, {fewest}-{most} shelves: smallest item touch side {smallest:F1} px = {smallest / density:F1} dp");
        }

        [TestCaseSource(nameof(DensityScreens))]
        public void ItemTouchAreas_ContainTheirTile_NeverOverlap_AndHitTestFindsThem(string name, int width, int height, float density, int fewest, int most, float minDp)
        {
            var safe = new Rect(0, 0, width, height);
            for (int count = 4; count <= 14; count++)
            {
                var layout = BoardLayout.Compute(count, Shelf.SlotCount, safe, BoardLayoutSettings.Default, density);
                string context = $"{name}, {count} shelves";
                for (int i = 0; i < count; i++)
                {
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        var touch = layout.GetSlotHitRect(i, slot);
                        var tile = layout.GetFrontSlotRect(i, slot);
                        Assert.IsTrue(Contains(touch, tile), $"{context}: shelf {i} slot {slot} touch area {touch} does not contain tile {tile}");
                        Assert.IsTrue(Contains(layout.GetShelfHitRect(i), touch), $"{context}: shelf {i} slot {slot} touch area outside the shelf's drop area");

                        var expected = BoardTarget.At(i, slot);
                        Assert.AreEqual(expected, layout.HitTest(tile.center), $"{context}: tile centre");
                        Assert.AreEqual(expected, layout.HitTest(layout.GetHiddenSlotRect(i, slot).center), $"{context}: peeking hidden tile");
                        const float inset = 0.5f;
                        Assert.AreEqual(expected, layout.HitTest(new Vector2(touch.xMin + inset, touch.yMin + inset)), $"{context}: touch area bottom-left");
                        Assert.AreEqual(expected, layout.HitTest(new Vector2(touch.xMax - inset, touch.yMax - inset)), $"{context}: touch area top-right");

                        for (int j = i; j < count; j++)
                        {
                            for (int other = 0; other < Shelf.SlotCount; other++)
                            {
                                if (j == i && other <= slot) continue;
                                Assert.IsFalse(Overlaps(touch, layout.GetSlotHitRect(j, other)),
                                    $"{context}: touch areas of shelf {i} slot {slot} and shelf {j} slot {other} overlap");
                            }
                        }
                    }
                }

                Assert.AreEqual(BoardTarget.None, layout.HitTest(new Vector2(safe.center.x, safe.yMax - 1f)), $"{context}: HUD");
            }
        }

        [Test]
        public void Density_ChangesOnlyTouchAreas_NotShelvesOrTiles()
        {
            var safe = new Rect(0, 0, 720, 1280);
            for (int count = 4; count <= 14; count++)
            {
                var px = BoardLayout.Compute(count, Shelf.SlotCount, safe, BoardLayoutSettings.Default);
                var dp = BoardLayout.Compute(count, Shelf.SlotCount, safe, BoardLayoutSettings.Default, 2.0f);
                Assert.AreEqual(px.TileSize, dp.TileSize, $"{count} shelves: tile size");
                for (int i = 0; i < count; i++)
                {
                    Assert.AreEqual(px.GetShelfRect(i), dp.GetShelfRect(i), $"{count} shelves: shelf {i}");
                    Assert.AreEqual(px.GetShelfHitRect(i), dp.GetShelfHitRect(i), $"{count} shelves: shelf {i} drop area");
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        Assert.AreEqual(px.GetFrontSlotRect(i, slot), dp.GetFrontSlotRect(i, slot), $"{count} shelves: shelf {i} tile {slot}");
                        Assert.AreEqual(px.GetHiddenSlotRect(i, slot), dp.GetHiddenSlotRect(i, slot), $"{count} shelves: shelf {i} hidden {slot}");
                    }
                }
            }
        }

        [Test]
        public void NarrowMiddleItem_TakesRoomFromWiderNeighbours_UpToTheirPictures()
        {
            // 14 shelves on 720x1280: halfway splits leave the middle item under 48 dp at density 2.
            var layout = BoardLayout.Compute(14, Shelf.SlotCount, new Rect(0, 0, 720, 1280), BoardLayoutSettings.Default, 2.0f);
            var left = layout.GetFrontSlotRect(0, 0);
            var middle = layout.GetFrontSlotRect(0, 1);
            var right = layout.GetFrontSlotRect(0, 2);
            float halfwaySplit = (middle.xMax + right.xMin) * 0.5f - (left.xMax + middle.xMin) * 0.5f;
            Assert.Less(halfwaySplit, BoardLayout.TargetTouchDp * 2.0f, "Precondition: halfway split is under 48 dp.");

            var touch = layout.GetSlotHitRect(0, 1);
            Assert.Greater(touch.width, halfwaySplit + 1f, "Middle item grew past the halfway split.");
            Assert.GreaterOrEqual(touch.xMin, left.xMax - Epsilon, "Never over the left neighbour's picture.");
            Assert.LessOrEqual(touch.xMax, right.xMin + Epsilon, "Never over the right neighbour's picture.");
        }

        [Test]
        public void WideItems_KeepHalfwaySplits()
        {
            // 4 shelves on 1080x1920: every item is already over 48 dp, so touch areas split halfway between pictures.
            var layout = BoardLayout.Compute(4, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default, 2.625f);
            var a = layout.GetFrontSlotRect(0, 0);
            var b = layout.GetFrontSlotRect(0, 1);
            Assert.AreEqual((a.xMax + b.xMin) * 0.5f, layout.GetSlotHitRect(0, 0).xMax, Epsilon);
            Assert.AreEqual((a.xMax + b.xMin) * 0.5f, layout.GetSlotHitRect(0, 1).xMin, Epsilon);
        }

        [Test]
        public void Compute_RejectsNonPositiveDensity()
        {
            var safe = new Rect(0, 0, 1080, 1920);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BoardLayout.Compute(4, 3, safe, BoardLayoutSettings.Default, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BoardLayout.Compute(4, 3, safe, BoardLayoutSettings.Default, float.NaN));
        }

        [Test]
        public void HitTest_InGapBetweenRows_PicksNearestShelf()
        {
            // Issue #40: 4 shelves now sit 2 per row, so the shelf below shelf 0 is shelf 2 (was shelf 1 in one column).
            var layout = BoardLayout.Compute(4, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default);
            Assert.AreEqual(2, layout.Columns, "Precondition: two columns.");
            int below = layout.Columns;
            float x = layout.GetFrontSlotRect(0, 1).center.x;
            float gapTop = layout.GetShelfRect(0).yMin;
            float gapBottom = layout.GetShelfRect(below).yMax;
            Assert.AreEqual(0, layout.HitTest(new Vector2(x, gapTop - 1f)).Shelf, "Just below shelf 0");
            Assert.AreEqual(below, layout.HitTest(new Vector2(x, gapBottom + 1f)).Shelf, "Just above shelf 2");
        }

        [Test]
        public void FourShelves_On16By9_UseTwoColumns()
        {
            // Issue #40: shelves fill the width 2 or 3 per row (was 1 column x 4 rows).
            var layout = BoardLayout.Compute(4, Shelf.SlotCount, new Rect(0, 0, 1080, 1920), BoardLayoutSettings.Default);
            Assert.AreEqual(2, layout.Columns);
            Assert.AreEqual(2, layout.Rows);
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
