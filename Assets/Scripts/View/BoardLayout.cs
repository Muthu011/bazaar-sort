using System;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Tunable proportions for <see cref="BoardLayout"/>. Lengths marked "tiles" are multiples of the tile (item)
    /// size, so the whole board scales together. Fractions are of the safe area.
    /// </summary>
    [Serializable]
    public struct BoardLayoutSettings
    {
        /// <summary>Safe-area height kept free at the top for the HUD (level, moves).</summary>
        public float TopReserveFraction;

        /// <summary>Safe-area height kept free at the bottom for the booster bar.</summary>
        public float BottomReserveFraction;

        /// <summary>Safe-area width kept free on each side.</summary>
        public float SideMarginFraction;

        /// <summary>Padding inside a shelf around the tiles and between tiles, in tiles.</summary>
        public float ShelfPadding;

        /// <summary>Height of the strip above the front row where the next hidden layer peeks out, in tiles.</summary>
        public float HiddenStripHeight;

        /// <summary>Horizontal gap between shelf columns, in tiles.</summary>
        public float ColumnGap;

        /// <summary>Vertical gap between shelf rows, in tiles.</summary>
        public float RowGap;

        /// <summary>Most shelf columns the layout will try.</summary>
        public int MaxColumns;

        public static BoardLayoutSettings Default => new BoardLayoutSettings
        {
            TopReserveFraction = 0.10f,
            BottomReserveFraction = 0.10f,
            SideMarginFraction = 0.04f,
            ShelfPadding = 0.12f,
            HiddenStripHeight = 0.35f,
            ColumnGap = 0.25f,
            RowGap = 0.35f,
            MaxColumns = 3,
        };
    }

    /// <summary>
    /// Where each shelf and slot goes on screen, in pixels (origin bottom-left, like <see cref="Screen.safeArea"/>).
    /// Pure math, no scene access, so Edit Mode tests can check every shelf count and aspect ratio.
    /// </summary>
    public sealed class BoardLayout
    {
        private readonly Rect[] _shelves;
        private readonly BoardLayoutSettings _settings;

        private BoardLayout(Rect[] shelves, float tileSize, int columns, int rows, Rect playfield, BoardLayoutSettings settings)
        {
            _shelves = shelves;
            TileSize = tileSize;
            Columns = columns;
            Rows = rows;
            Playfield = playfield;
            _settings = settings;
        }

        /// <summary>Side of one front-row tile, in pixels.</summary>
        public float TileSize { get; }

        public int Columns { get; }
        public int Rows { get; }

        /// <summary>The part of the safe area the shelves may use (safe area minus HUD, booster bar and margins).</summary>
        public Rect Playfield { get; }

        public int ShelfCount => _shelves.Length;

        /// <summary>Shelf rectangle (background) in pixels.</summary>
        public Rect GetShelfRect(int shelf) => _shelves[shelf];

        /// <summary>Front-row slot rectangle in pixels. Slots run left to right.</summary>
        public Rect GetFrontSlotRect(int shelf, int slot)
        {
            var s = _shelves[shelf];
            float pad = _settings.ShelfPadding * TileSize;
            return new Rect(s.x + pad + slot * (TileSize + pad), s.y + pad, TileSize, TileSize);
        }

        /// <summary>
        /// Rectangle for the next hidden layer's item in <paramref name="slot"/>: a smaller tile in the strip above the
        /// front row, so the player can see what comes next.
        /// </summary>
        public Rect GetHiddenSlotRect(int shelf, int slot)
        {
            var front = GetFrontSlotRect(shelf, slot);
            float size = _settings.HiddenStripHeight * TileSize;
            // Sits between the top of the front tile and the top padding of the shelf.
            return new Rect(front.center.x - size * 0.5f, front.yMax, size, size);
        }

        /// <summary>Shelf width in tiles for the given settings.</summary>
        public static float ShelfWidthInTiles(BoardLayoutSettings s, int slotCount)
        {
            return slotCount + (slotCount + 1) * s.ShelfPadding;
        }

        /// <summary>Shelf height in tiles for the given settings.</summary>
        public static float ShelfHeightInTiles(BoardLayoutSettings s)
        {
            return 1f + s.HiddenStripHeight + 2f * s.ShelfPadding;
        }

        /// <summary>
        /// Lays out <paramref name="shelfCount"/> shelves of <paramref name="slotCount"/> slots inside
        /// <paramref name="safeArea"/>. Tries 1..<see cref="BoardLayoutSettings.MaxColumns"/> columns and keeps the
        /// one with the largest tiles (fewer columns on a tie). Shelves fill rows top to bottom, left to right; a
        /// short last row is centred. The grid is centred in the playfield.
        /// </summary>
        public static BoardLayout Compute(int shelfCount, int slotCount, Rect safeArea, BoardLayoutSettings settings)
        {
            if (shelfCount < 1) throw new ArgumentOutOfRangeException(nameof(shelfCount));
            if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount));
            if (safeArea.width <= 0f || safeArea.height <= 0f) throw new ArgumentException("Safe area is empty.", nameof(safeArea));

            float side = safeArea.width * settings.SideMarginFraction;
            float top = safeArea.height * settings.TopReserveFraction;
            float bottom = safeArea.height * settings.BottomReserveFraction;
            var playfield = new Rect(safeArea.x + side, safeArea.y + bottom,
                safeArea.width - 2f * side, safeArea.height - top - bottom);

            float shelfW = ShelfWidthInTiles(settings, slotCount);
            float shelfH = ShelfHeightInTiles(settings);

            int bestColumns = 1;
            float bestTile = -1f;
            int maxColumns = Math.Max(1, Math.Min(settings.MaxColumns, shelfCount));
            for (int c = 1; c <= maxColumns; c++)
            {
                int r = (shelfCount + c - 1) / c;
                float widthTiles = c * shelfW + (c - 1) * settings.ColumnGap;
                float heightTiles = r * shelfH + (r - 1) * settings.RowGap;
                float tile = Math.Min(playfield.width / widthTiles, playfield.height / heightTiles);
                if (tile > bestTile)
                {
                    bestTile = tile;
                    bestColumns = c;
                }
            }

            int columns = bestColumns;
            int rows = (shelfCount + columns - 1) / columns;
            float t = bestTile;
            float w = shelfW * t;
            float h = shelfH * t;
            float gx = settings.ColumnGap * t;
            float gy = settings.RowGap * t;
            float gridH = rows * h + (rows - 1) * gy;
            float gridTop = playfield.center.y + gridH * 0.5f;

            var rects = new Rect[shelfCount];
            for (int i = 0; i < shelfCount; i++)
            {
                int row = i / columns;
                int col = i % columns;
                int inRow = Math.Min(columns, shelfCount - row * columns);
                float rowW = inRow * w + (inRow - 1) * gx;
                float x = playfield.center.x - rowW * 0.5f + col * (w + gx);
                float y = gridTop - (row + 1) * h - row * gy;
                rects[i] = new Rect(x, y, w, h);
            }

            return new BoardLayout(rects, t, columns, rows, playfield, settings);
        }
    }
}
