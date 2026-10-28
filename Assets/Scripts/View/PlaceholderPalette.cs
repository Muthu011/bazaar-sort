using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>A placeholder look for one object id: a tile colour and a one-letter label.</summary>
    public readonly struct PlaceholderStyle
    {
        public PlaceholderStyle(string objectId, Color32 color, string letter)
        {
            ObjectId = objectId;
            Color = color;
            Letter = letter;
        }

        public string ObjectId { get; }
        public Color32 Color { get; }

        /// <summary>One upper-case letter, kept as a string so the view never allocates when it sets the label.</summary>
        public string Letter { get; }

        /// <summary>Black or white, whichever reads better on <see cref="Color"/>.</summary>
        public Color32 LetterColor => RelativeLuminance(Color) > 0.45f ? new Color32(20, 20, 20, 255) : new Color32(255, 255, 255, 255);

        internal static float RelativeLuminance(Color32 c)
        {
            return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
        }
    }

    /// <summary>
    /// Placeholder colours and letters per object id, used until real art lands. One entry per id in
    /// <c>Game.Levels.ObjectCatalog</c>; tests check they stay in step and stay distinct.
    /// </summary>
    public static class PlaceholderPalette
    {
        private static readonly PlaceholderStyle[] _entries =
        {
            // World 1, Amma's Kitchen
            new PlaceholderStyle("tumbler",         new Color32(0xB0, 0xBE, 0xC5, 0xFF), "T"), // steel grey
            new PlaceholderStyle("pressure_cooker", new Color32(0xD3, 0x2F, 0x2F, 0xFF), "P"), // kumkum red
            new PlaceholderStyle("idli_stand",      new Color32(0xFF, 0xFF, 0xFF, 0xFF), "I"), // white
            new PlaceholderStyle("tawa",            new Color32(0x26, 0x26, 0x26, 0xFF), "W"), // black iron
            new PlaceholderStyle("masala_dabba",    new Color32(0xFD, 0xD8, 0x35, 0xFF), "M"), // turmeric yellow
            new PlaceholderStyle("steel_tiffin",    new Color32(0x00, 0x83, 0x8F, 0xFF), "S"), // peacock teal
            new PlaceholderStyle("coconut_scraper", new Color32(0x6D, 0x4C, 0x41, 0xFF), "N"), // coconut brown
            new PlaceholderStyle("brass_kudam",     new Color32(0xB8, 0x86, 0x0B, 0xFF), "K"), // brass gold
            new PlaceholderStyle("rolling_pin",     new Color32(0x2E, 0x7D, 0x32, 0xFF), "R"), // banana-leaf green
            new PlaceholderStyle("chai_kettle",     new Color32(0x6A, 0x1B, 0x9A, 0xFF), "C"), // purple
        };

        private static readonly Dictionary<string, PlaceholderStyle> _byId = BuildLookup();

        /// <summary>Shown for an id missing from the table, so a gap is obvious on screen rather than invisible.</summary>
        public static readonly PlaceholderStyle Unknown = new PlaceholderStyle(null, new Color32(0xFF, 0x00, 0xFF, 0xFF), "?");

        public static IReadOnlyList<PlaceholderStyle> Entries => _entries;

        public static bool TryGet(string objectId, out PlaceholderStyle style)
        {
            if (objectId != null && _byId.TryGetValue(objectId, out style)) return true;
            style = Unknown;
            return false;
        }

        /// <summary>The style for <paramref name="objectId"/>, or <see cref="Unknown"/>.</summary>
        public static PlaceholderStyle Get(string objectId)
        {
            TryGet(objectId, out var style);
            return style;
        }

        private static Dictionary<string, PlaceholderStyle> BuildLookup()
        {
            var map = new Dictionary<string, PlaceholderStyle>(StringComparer.Ordinal);
            foreach (var e in _entries) map[e.ObjectId] = e;
            return map;
        }
    }
}
