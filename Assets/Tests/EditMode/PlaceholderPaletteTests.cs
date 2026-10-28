using System.Collections.Generic;
using Game.Levels;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="PlaceholderPalette"/>: one distinct colour and letter per catalogued object id.
    /// </summary>
    public class PlaceholderPaletteTests
    {
        /// <summary>Smallest allowed RGB distance between two object colours (0..441).</summary>
        private const float MinColorDistance = 75f;

        [Test]
        public void EveryCatalogObject_HasAStyle()
        {
            foreach (var id in ObjectCatalog.World1)
            {
                Assert.IsTrue(PlaceholderPalette.TryGet(id, out var style), $"No placeholder style for '{id}'.");
                Assert.AreEqual(id, style.ObjectId);
            }
        }

        [Test]
        public void EveryStyle_IsACatalogObject()
        {
            foreach (var entry in PlaceholderPalette.Entries)
            {
                Assert.IsTrue(ObjectCatalog.IsKnown(entry.ObjectId), $"Palette has '{entry.ObjectId}', which is not in ObjectCatalog.");
            }
        }

        [Test]
        public void ObjectIds_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var entry in PlaceholderPalette.Entries)
            {
                Assert.IsTrue(seen.Add(entry.ObjectId), $"'{entry.ObjectId}' appears twice.");
            }
        }

        [Test]
        public void Letters_AreSingleUpperCaseAndDistinct()
        {
            var seen = new Dictionary<string, string>();
            foreach (var entry in PlaceholderPalette.Entries)
            {
                Assert.AreEqual(1, entry.Letter.Length, $"'{entry.ObjectId}' letter '{entry.Letter}' is not one character.");
                Assert.IsTrue(char.IsUpper(entry.Letter[0]), $"'{entry.ObjectId}' letter '{entry.Letter}' is not upper case.");
                Assert.IsFalse(seen.TryGetValue(entry.Letter, out var other), $"'{entry.ObjectId}' and '{other}' share letter '{entry.Letter}'.");
                seen[entry.Letter] = entry.ObjectId;
            }
        }

        [Test]
        public void Colors_AreOpaqueAndClearlyDistinct()
        {
            var entries = PlaceholderPalette.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(255, entries[i].Color.a, $"'{entries[i].ObjectId}' colour is not opaque.");
                for (int j = i + 1; j < entries.Count; j++)
                {
                    float distance = Distance(entries[i].Color, entries[j].Color);
                    Assert.GreaterOrEqual(distance, MinColorDistance,
                        $"'{entries[i].ObjectId}' and '{entries[j].ObjectId}' colours are too close ({distance:0}).");
                }
            }
        }

        [Test]
        public void UnknownId_GetsTheFallbackStyle()
        {
            Assert.IsFalse(PlaceholderPalette.TryGet("not_an_object", out var style));
            Assert.AreEqual("?", style.Letter);
            Assert.AreEqual("?", PlaceholderPalette.Get(null).Letter);
        }

        [Test]
        public void LetterColor_IsDarkOnLightTilesAndLightOnDarkTiles()
        {
            Assert.AreEqual(20, PlaceholderPalette.Get("idli_stand").LetterColor.r, "White tile needs a dark letter.");
            Assert.AreEqual(255, PlaceholderPalette.Get("tawa").LetterColor.r, "Black tile needs a light letter.");
        }

        private static float Distance(Color32 a, Color32 b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
