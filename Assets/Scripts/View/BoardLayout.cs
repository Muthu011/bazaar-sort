using System;
using Game.Core;
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
        private readonly int _slotCount;

        // Left edges of each item's touch area plus the right edge of the last one, as x offsets from the shelf's
        // left edge. Every shelf has the same width, so one set serves them all and hit tests allocate nothing.
        private readonly float[] _touchEdges;

        private BoardLayout(Rect[] shelves, int slotCount, float tileSize, int columns, int rows, Rect playfield,
            BoardLayoutSettings settings, float density)
        {
            _slotCount = slotCount;
            _shelves = shelves;
            TileSize = tileSize;
            Columns = columns;
            Rows = rows;
            Playfield = playfield;
            _settings = settings;
            Density = density;
            _touchEdges = ComputeTouchEdges(slotCount, tileSize, settings, TargetTouchDp * density);
        }

        /// <summary>Screen density the touch areas were sized for: pixels per dp (see <see cref="ScreenDensity"/>).</summary>
        public float Density { get; }

        /// <summary>Side of one front-row tile, in pixels.</summary>
        public float TileSize { get; }

        public int Columns { get; }
        public int Rows { get; }

        /// <summary>The part of the safe area the shelves may use (safe area minus HUD, booster bar and margins).</summary>
        public Rect Playfield { get; }

        public int ShelfCount => _shelves.Length;

        /// <summary>Front-row slots per shelf.</summary>
        public int SlotsPerShelf => _slotCount;

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

        /// <summary>
        /// Smallest hit target side, in screen pixels (issue #5: touch targets at least 48 px). Hit rectangles smaller
        /// than this are grown around their centre.
        /// </summary>
        public const float MinTouchTargetPx = 48f;

        /// <summary>
        /// Size, in dp, each item's touch area grows towards (each dimension) by taking room from the gaps around it.
        /// Reached wherever the gaps allow; the shelf layout itself never changes to make room.
        /// </summary>
        public const float TargetTouchDp = 48f;

        /// <summary>
        /// Accepted floor, in dp, for item touch areas on 720p phones with the densest (14-shelf) layout, where the
        /// gaps cannot quite give <see cref="TargetTouchDp"/>.
        /// </summary>
        public const float FloorTouchDp = 44f;

        /// <summary>
        /// Where a press or drop counts as "on this shelf": the shelf rectangle grown by half the column gap on each
        /// side and half the row gap above and below, so neighbouring shelves' hit areas meet without overlapping and
        /// a drop in the gap still lands on the nearest shelf. At least <see cref="MinTouchTargetPx"/> each way.
        /// </summary>
        public Rect GetShelfHitRect(int shelf)
        {
            var s = _shelves[shelf];
            float gx = _settings.ColumnGap * TileSize * 0.5f;
            float gy = _settings.RowGap * TileSize * 0.5f;
            return AtLeast(new Rect(s.x - gx, s.y - gy, s.width + 2f * gx, s.height + 2f * gy), MinTouchTargetPx);
        }

        /// <summary>
        /// The touch area of the item in front slot <paramref name="slot"/>: where a press picks it up. It covers the
        /// item's picture and extends into the gaps around it: up and down to halfway into the row gap (the full height
        /// of the shelf hit area, so the peeking hidden tile above counts too), and sideways into the gaps between
        /// items. Sideways it starts halfway between neighbouring pictures; an item still narrower than
        /// <see cref="TargetTouchDp"/> then takes spare room from a wider neighbour, never past that neighbour's
        /// picture and never leaving it below the target. Touch areas on a shelf meet without overlapping. At least
        /// <see cref="MinTouchTargetPx"/> each way.
        /// </summary>
        public Rect GetSlotHitRect(int shelf, int slot)
        {
            var hit = GetShelfHitRect(shelf);
            float x = _shelves[shelf].x;
            float left = slot == 0 ? hit.xMin : x + _touchEdges[slot];
            float right = slot == _slotCount - 1 ? hit.xMax : x + _touchEdges[slot + 1];
            return AtLeast(new Rect(left, hit.y, right - left, hit.height), MinTouchTargetPx);
        }

        /// <summary>
        /// Sideways edges of the item touch areas (see <see cref="GetSlotHitRect"/>), as offsets from the shelf's left
        /// edge. Edge 0 and edge <paramref name="slotCount"/> are the shelf hit area's sides.
        /// </summary>
        private static float[] ComputeTouchEdges(int slotCount, float tile, BoardLayoutSettings settings, float targetPx)
        {
            float pad = settings.ShelfPadding * tile;
            float halfColumnGap = settings.ColumnGap * tile * 0.5f;
            float shelfWidth = ShelfWidthInTiles(settings, slotCount) * tile;

            // Tile i spans [pad + i * (tile + pad), that + tile].
            var edges = new float[slotCount + 1];
            edges[0] = -halfColumnGap;
            edges[slotCount] = shelfWidth + halfColumnGap;
            for (int i = 1; i < slotCount; i++) edges[i] = i * (tile + pad) + pad * 0.5f;

            for (int i = 0; i < slotCount; i++)
            {
                float need = targetPx - (edges[i + 1] - edges[i]);
                if (need <= 0f) continue;

                // How far each side may move: up to the neighbour's picture, and only by the neighbour's room above
                // the target, so the neighbour keeps its own picture and target.
                float leftRoom = i == 0 ? 0f
                    : Math.Max(0f, Math.Min(edges[i] - i * (tile + pad), (edges[i] - edges[i - 1]) - targetPx));
                float rightRoom = i == slotCount - 1 ? 0f
                    : Math.Max(0f, Math.Min(pad + (i + 1) * (tile + pad) - edges[i + 1], (edges[i + 2] - edges[i + 1]) - targetPx));

                float takeLeft = Math.Min(leftRoom, need * 0.5f);
                float takeRight = Math.Min(rightRoom, need - takeLeft);
                takeLeft = Math.Min(leftRoom, need - takeRight);
                edges[i] -= takeLeft;
                edges[i + 1] += takeRight;
            }

            return edges;
        }

        /// <summary>
        /// The shelf and slot column under <paramref name="point"/> (pixels, same space as the layout), or
        /// <see cref="BoardTarget.None"/>.
        /// </summary>
        public BoardTarget HitTest(Vector2 point)
        {
            for (int i = 0; i < _shelves.Length; i++)
            {
                if (!GetShelfHitRect(i).Contains(point)) continue;
                int slots = _slotCount;
                for (int slot = 0; slot < slots; slot++)
                {
                    if (GetSlotHitRect(i, slot).Contains(point)) return BoardTarget.At(i, slot);
                }
                return BoardTarget.At(i, -1);
            }
            return BoardTarget.None;
        }

        private static Rect AtLeast(Rect r, float min)
        {
            if (r.width < min) r = new Rect(r.center.x - min * 0.5f, r.y, min, r.height);
            if (r.height < min) r = new Rect(r.x, r.center.y - min * 0.5f, r.width, min);
            return r;
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
            return Compute(shelfCount, slotCount, safeArea, settings, 1f);
        }

        /// <summary>
        /// As <see cref="Compute(int,int,Rect,BoardLayoutSettings)"/>, with item touch areas sized for
        /// <paramref name="density"/> pixels per dp (<see cref="ScreenDensity.Current"/> at runtime). Density changes
        /// only the touch areas, never where shelves and tiles go. The overload without density uses 1 (dp = px).
        /// </summary>
        public static BoardLayout Compute(int shelfCount, int slotCount, Rect safeArea, BoardLayoutSettings settings, float density)
        {
            if (!(density > 0f)) throw new ArgumentOutOfRangeException(nameof(density));
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

            return new BoardLayout(rects, slotCount, t, columns, rows, playfield, settings, density);
        }
    }
}
