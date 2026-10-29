using System.Collections.Generic;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Issue #40, the portrait phone layout: on 1080x2400 (20:9), 1080x1920 (16:9) and 720x1600, with and without
    /// simulated notch and gesture-bar insets, the board sits in the middle band of the safe area, 2 or 3 shelves per
    /// row filling the width, and item touch areas keep 48 dp (levels 1 to 20: up to 8 shelves) and the 44 dp floor
    /// (9 to 14 shelves). Also: the player settings are portrait only.
    /// </summary>
    public class PortraitLayoutTests
    {
        private const float Epsilon = 0.01f;

        /// <summary>A phone: screen size, density (px per dp) and safe-area insets in px (left, bottom, right, top).</summary>
        public readonly struct Phone
        {
            public Phone(string name, int width, int height, float density, Vector4 insets)
            {
                Name = name;
                Width = width;
                Height = height;
                Density = density;
                Insets = insets;
            }

            public string Name { get; }
            public int Width { get; }
            public int Height { get; }
            public float Density { get; }
            public Vector4 Insets { get; }

            public Rect SafeArea => new Rect(Insets.x, Insets.y, Width - Insets.x - Insets.z, Height - Insets.y - Insets.w);

            public override string ToString() => Name;
        }

        /// <summary>
        /// The three Game view presets. Insets: 20:9 phones have a camera cutout (110 px at 3x, 74 px at 2x, about 37 dp)
        /// and a gesture bar (63 px / 42 px, 21 dp). 16:9 phones have no cutout and the game starts full screen
        /// (androidStartInFullscreen hides the status bar; on-screen buttons sit outside the window), so their safe area
        /// is the whole screen.
        /// </summary>
        public static IEnumerable<Phone> Phones()
        {
            yield return new Phone("1080x2400 @3", 1080, 2400, 3f, Vector4.zero);
            yield return new Phone("1080x2400 @3 notch+gesture", 1080, 2400, 3f, new Vector4(0, 63, 0, 110));
            yield return new Phone("1080x1920 @3", 1080, 1920, 3f, Vector4.zero);
            yield return new Phone("720x1600 @2", 720, 1600, 2f, Vector4.zero);
            yield return new Phone("720x1600 @2 notch+gesture", 720, 1600, 2f, new Vector4(0, 42, 0, 74));
        }

        private static BoardLayout Layout(Phone phone, int shelves)
        {
            return BoardLayout.Compute(shelves, Shelf.SlotCount, phone.SafeArea, BoardLayoutSettings.Default, phone.Density);
        }

        [TestCaseSource(nameof(Phones))]
        public void Board_SitsInTheMiddleBand_ClearOfTheBarsAndInsets(Phone phone)
        {
            var safe = phone.SafeArea;
            var settings = BoardLayoutSettings.Default;
            for (int count = 4; count <= 14; count++)
            {
                var layout = Layout(phone, count);
                var band = layout.Playfield;
                string context = $"{phone}, {count} shelves";
                Assert.AreEqual(safe.yMax - safe.height * settings.TopReserveFraction, band.yMax, Epsilon, $"{context}: top bar above the band");
                Assert.AreEqual(safe.yMin + safe.height * settings.BottomReserveFraction, band.yMin, Epsilon, $"{context}: bottom bar below the band");
                Assert.LessOrEqual(band.yMax, phone.Height - phone.Insets.w + Epsilon, $"{context}: below the notch");
                Assert.GreaterOrEqual(band.yMin, phone.Insets.y - Epsilon, $"{context}: above the gesture bar");
                for (int i = 0; i < count; i++)
                {
                    var shelf = layout.GetShelfRect(i);
                    Assert.GreaterOrEqual(shelf.yMin, band.yMin - Epsilon, $"{context}: shelf {i} above the bottom bar");
                    Assert.LessOrEqual(shelf.yMax, band.yMax + Epsilon, $"{context}: shelf {i} below the top bar");
                    Assert.GreaterOrEqual(shelf.xMin, band.xMin - Epsilon, $"{context}: shelf {i} left");
                    Assert.LessOrEqual(shelf.xMax, band.xMax + Epsilon, $"{context}: shelf {i} right");
                }

                // The grid is centred in the band.
                float top = layout.GetShelfRect(0).yMax;
                float bottom = layout.GetShelfRect(count - 1).yMin;
                Assert.AreEqual(band.center.y, (top + bottom) * 0.5f, 0.5f, $"{context}: centred vertically");
            }
        }

        [TestCaseSource(nameof(Phones))]
        public void Shelves_TwoOrThreePerRow_AndFillTheWidth(Phone phone)
        {
            for (int count = 4; count <= 14; count++)
            {
                var layout = Layout(phone, count);
                string context = $"{phone}, {count} shelves";
                Assert.That(layout.Columns, Is.InRange(2, 3), $"{context}: shelves per row");

                float left = float.MaxValue, right = float.MinValue, top = float.MinValue, bottom = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    var r = layout.GetShelfRect(i);
                    left = Mathf.Min(left, r.xMin);
                    right = Mathf.Max(right, r.xMax);
                    top = Mathf.Max(top, r.yMax);
                    bottom = Mathf.Min(bottom, r.yMin);
                }
                var band = layout.Playfield;
                bool fillsWidth = right - left >= band.width - 0.5f;
                bool fillsHeight = top - bottom >= band.height - 0.5f;
                Assert.IsTrue(fillsWidth || fillsHeight, $"{context}: the shelves grow until they touch the band's sides or ends");

                // Levels 1 to 20 (at most 8 shelves) always fill the width on these phones.
                if (count <= 8) Assert.IsTrue(fillsWidth, $"{context}: fills the width ({right - left:F1} of {band.width:F1} px)");
            }
        }

        [TestCaseSource(nameof(Phones))]
        public void ItemTouchAreas_48dpUpTo8Shelves_44dpFloorUpTo14(Phone phone)
        {
            for (int count = 4; count <= 14; count++)
            {
                var layout = Layout(phone, count);
                float minDp = count <= 8 ? BoardLayout.TargetTouchDp : BoardLayout.FloorTouchDp;
                float smallest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        var touch = layout.GetSlotHitRect(i, slot);
                        Assert.IsTrue(touch.Contains(layout.GetFrontSlotRect(i, slot).center), $"{phone}, {count}: touch area on its tile");
                        smallest = Mathf.Min(smallest, Mathf.Min(touch.width, touch.height));
                    }
                }
                Assert.GreaterOrEqual(smallest / phone.Density, minDp - Epsilon,
                    $"{phone}, {count} shelves: smallest touch side {smallest / phone.Density:F1} dp");
            }
        }

        /// <summary>The numbers for the PR (tile dp, columns, band) are written to the test output.</summary>
        [TestCaseSource(nameof(Phones))]
        public void LayoutTable_IsWrittenToTheTestOutput(Phone phone)
        {
            var settings = BoardLayoutSettings.Default;
            var safe = phone.SafeArea;
            TestContext.WriteLine($"{phone}: safe {safe.width}x{safe.height} px; top bar {safe.height * settings.TopReserveFraction / phone.Density:F1} dp, " +
                $"bottom bar {safe.height * settings.BottomReserveFraction / phone.Density:F1} dp");
            for (int count = 4; count <= 14; count++)
            {
                var layout = Layout(phone, count);
                float smallest = float.MaxValue;
                for (int i = 0; i < count; i++)
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                    {
                        var t = layout.GetSlotHitRect(i, slot);
                        smallest = Mathf.Min(smallest, Mathf.Min(t.width, t.height));
                    }
                TestContext.WriteLine($"  {count,2} shelves: {layout.Columns} x {layout.Rows}, tile {layout.TileSize:F1} px = " +
                    $"{layout.TileSize / phone.Density:F1} dp, smallest touch {smallest / phone.Density:F1} dp");
                Assert.Greater(layout.TileSize, 0f);
            }
        }

        [Test]
        public void MinColumns_BelowOne_CountsAsOne_AndNeverExceedsTheShelfCount()
        {
            var settings = BoardLayoutSettings.Default;
            settings.MinColumns = 0;
            var one = BoardLayout.Compute(4, Shelf.SlotCount, new Rect(0, 0, 1080, 2400), settings);
            Assert.AreEqual(1, one.Columns, "Without a minimum, a tall phone picks one column for 4 shelves (larger tiles).");

            settings = BoardLayoutSettings.Default;
            settings.MinColumns = 5;
            var two = BoardLayout.Compute(2, Shelf.SlotCount, new Rect(0, 0, 1080, 2400), settings);
            Assert.AreEqual(2, two.Columns, "Clamped to the shelf count.");
        }

        [Test]
        public void PlayerSettings_ArePortraitOnly()
        {
            Assert.AreEqual(UIOrientation.Portrait, PlayerSettings.defaultInterfaceOrientation);
            Assert.IsFalse(PlayerSettings.allowedAutorotateToLandscapeLeft, "No landscape left.");
            Assert.IsFalse(PlayerSettings.allowedAutorotateToLandscapeRight, "No landscape right.");
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortraitUpsideDown, "No upside-down portrait.");
        }
    }
}
