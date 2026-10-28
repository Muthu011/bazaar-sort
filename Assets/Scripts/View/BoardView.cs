using Game.Core;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Draws a <see cref="Board"/> with placeholder shapes: a rounded panel per shelf, a coloured rounded tile with a
    /// letter per front item (<see cref="PlaceholderPalette"/>), and smaller dimmed tiles for the next hidden layer.
    ///
    /// Holds no rules. It reads the board's public state and redraws a shelf whenever a Core event names it.
    /// All GameObjects are made once in <see cref="Bind"/>; redraws only toggle and recolour them (no allocation).
    ///
    /// World units are screen pixels / <see cref="PixelsPerUnit"/>: the view sets up its orthographic camera so the
    /// pixel rectangles from <see cref="BoardLayout"/> map straight to world positions.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float PixelsPerUnit = 100f;

        private const int ShelfOrder = 0;
        private const int HiddenTileOrder = 1;
        private const int HiddenLabelOrder = 2;
        private const int FrontTileOrder = 3;
        private const int FrontLabelOrder = 4;
        private const int StatusOrder = 10;
        private const int LabelFontSize = 64;
        private const float LabelHeightInTile = 0.6f;

        internal const string WonText = "Cleared!";
        internal const string StuckText = "No moves left";

        private static readonly Color32 BackgroundColor = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 ShelfColor = new Color32(0xA1, 0x88, 0x7F, 0xFF);
        private static readonly Color32 StatusColor = new Color32(0xFF, 0xF8, 0xE1, 0xFF);
        private const float HiddenDim = 0.55f;

        private Board _board;
        private Camera _camera;
        private BoardLayoutSettings _settings;
        private BoardLayout _layout;
        private ShelfVisual[] _shelves;
        private TextMesh _status;
        private Font _font;

        /// <summary>The board being drawn, or null when unbound.</summary>
        public Board Board => _board;

        /// <summary>The current layout, or null when unbound.</summary>
        public BoardLayout Layout => _layout;

        /// <summary>
        /// Starts drawing <paramref name="board"/> through <paramref name="viewCamera"/>, laid out inside
        /// <paramref name="safeArea"/> (pixels of the camera's target). Unbinds any previous board first.
        /// </summary>
        public void Bind(Board board, Camera viewCamera, Rect safeArea, BoardLayoutSettings settings)
        {
            if (board == null) throw new System.ArgumentNullException(nameof(board));
            if (viewCamera == null) throw new System.ArgumentNullException(nameof(viewCamera));

            Unbind();
            _board = board;
            _camera = viewCamera;
            _settings = settings;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _shelves = new ShelfVisual[board.ShelfCount];
            for (int i = 0; i < _shelves.Length; i++)
            {
                _shelves[i] = new ShelfVisual(transform, i, _font);
            }
            _status = CreateLabel(transform, "Status", _font, StatusOrder);
            _status.text = string.Empty;
            _status.color = TextColor(StatusColor);

            _board.Moved += OnMoved;
            _board.Cleared += OnCleared;
            _board.LayerRevealed += OnLayerRevealed;
            _board.Won += OnWon;
            _board.Stuck += OnStuck;

            Relayout(safeArea);
            for (int i = 0; i < _shelves.Length; i++) RefreshShelf(i);
        }

        /// <summary>Stops listening to the board and destroys the shelf objects.</summary>
        public void Unbind()
        {
            if (_board != null)
            {
                _board.Moved -= OnMoved;
                _board.Cleared -= OnCleared;
                _board.LayerRevealed -= OnLayerRevealed;
                _board.Won -= OnWon;
                _board.Stuck -= OnStuck;
                _board = null;
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroySafely(transform.GetChild(i).gameObject);
            }
            _shelves = null;
            _status = null;
            _layout = null;
        }

        /// <summary>
        /// Recomputes the layout for <paramref name="safeArea"/> and the camera's current pixel size, then moves the
        /// existing objects. Call when the screen size or safe area changes.
        /// </summary>
        public void Relayout(Rect safeArea)
        {
            if (_board == null) return;

            float width = _camera.pixelWidth;
            float height = _camera.pixelHeight;
            _camera.orthographic = true;
            _camera.orthographicSize = height / (2f * PixelsPerUnit);
            _camera.transform.position = new Vector3(width / (2f * PixelsPerUnit), height / (2f * PixelsPerUnit), -10f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = BackgroundColor;

            _layout = BoardLayout.Compute(_board.ShelfCount, Shelf.SlotCount, safeArea, _settings);
            float labelScale = _layout.TileSize * LabelHeightInTile / PixelsPerUnit * 10f / LabelFontSize;

            for (int i = 0; i < _shelves.Length; i++)
            {
                var shelf = _shelves[i];
                Place(shelf.Background, _layout.GetShelfRect(i));
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    Place(shelf.Front[slot], _layout.GetFrontSlotRect(i, slot), labelScale);
                    Place(shelf.Hidden[slot], _layout.GetHiddenSlotRect(i, slot), labelScale * _settings.HiddenStripHeight);
                }
            }

            // Status line sits in the HUD reserve above the playfield.
            var playfield = _layout.Playfield;
            float statusY = (playfield.yMax + safeArea.yMax) * 0.5f;
            _status.transform.position = new Vector3(playfield.center.x / PixelsPerUnit, statusY / PixelsPerUnit, 0f);
            _status.characterSize = labelScale;
        }

        private void OnMoved(MoveInfo info)
        {
            RefreshShelf(info.FromShelf);
            RefreshShelf(info.ToShelf);
        }

        private void OnCleared(ClearInfo info) => RefreshShelf(info.ShelfIndex);

        private void OnLayerRevealed(LayerRevealInfo info) => RefreshShelf(info.ShelfIndex);

        private void OnWon() => _status.text = WonText;

        private void OnStuck() => _status.text = StuckText;

        /// <summary>Redraws one shelf from the board's current state.</summary>
        private void RefreshShelf(int index)
        {
            var shelf = _board.GetShelf(index);
            var visual = _shelves[index];
            bool hasHidden = shelf.HiddenLayerCount > 0;
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                visual.Front[slot].Show(shelf.GetFrontItem(slot), 1f);
                visual.Hidden[slot].Show(hasHidden ? shelf.GetHiddenItem(0, slot) : null, HiddenDim);
            }
        }

        private static void Place(SpriteRenderer renderer, Rect pixels)
        {
            renderer.transform.position = new Vector3(pixels.center.x / PixelsPerUnit, pixels.center.y / PixelsPerUnit, 0f);
            renderer.size = new Vector2(pixels.width / PixelsPerUnit, pixels.height / PixelsPerUnit);
        }

        private static void Place(Tile tile, Rect pixels, float labelScale)
        {
            Place(tile.Renderer, pixels);
            tile.Label.characterSize = labelScale;
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderSprites.RoundedSquare;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static TextMesh CreateLabel(Transform parent, string name, Font font, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = LabelFontSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = order;
            return label;
        }

        /// <summary>
        /// TextMesh's built-in shader does not convert vertex colours from sRGB in a Linear colour space project, so
        /// dark letters would come out grey. Convert here so labels match the sRGB palette like the sprites do.
        /// </summary>
        private static Color TextColor(Color32 srgb)
        {
            Color color = srgb;
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
        }

        private static void DestroySafely(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        // ---- Test hooks (Game.Tests.PlayMode) ----

        internal int ShelfVisualCount => _shelves == null ? 0 : _shelves.Length;

        internal string StatusText => _status == null ? null : _status.text;

        /// <summary>Number of visible front-row tiles across all shelves.</summary>
        internal int VisibleFrontTileCount
        {
            get
            {
                int count = 0;
                if (_shelves == null) return 0;
                foreach (var shelf in _shelves)
                {
                    foreach (var tile in shelf.Front)
                    {
                        if (tile.IsVisible) count++;
                    }
                }
                return count;
            }
        }

        /// <summary>Letter on a front tile, or null when the slot shows nothing.</summary>
        internal string GetFrontLetter(int shelf, int slot)
        {
            var tile = _shelves[shelf].Front[slot];
            return tile.IsVisible ? tile.Label.text : null;
        }

        internal Color32 GetFrontColor(int shelf, int slot) => _shelves[shelf].Front[slot].Renderer.color;

        /// <summary>The visuals for one shelf. Plain class: one MonoBehaviour per board, not per tile.</summary>
        private sealed class ShelfVisual
        {
            public readonly SpriteRenderer Background;
            public readonly Tile[] Front = new Tile[Shelf.SlotCount];
            public readonly Tile[] Hidden = new Tile[Shelf.SlotCount];

            public ShelfVisual(Transform parent, int index, Font font)
            {
                var root = new GameObject("Shelf " + index).transform;
                root.SetParent(parent, false);
                Background = CreateSprite(root, "Background", ShelfOrder);
                Background.color = ShelfColor;
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    Front[slot] = new Tile(root, "Front " + slot, font, FrontTileOrder, FrontLabelOrder);
                    Hidden[slot] = new Tile(root, "Hidden " + slot, font, HiddenTileOrder, HiddenLabelOrder);
                }
            }
        }

        /// <summary>One item tile: a tinted rounded square with a letter on it.</summary>
        private sealed class Tile
        {
            public readonly SpriteRenderer Renderer;
            public readonly TextMesh Label;

            public Tile(Transform parent, string name, Font font, int tileOrder, int labelOrder)
            {
                Renderer = CreateSprite(parent, name, tileOrder);
                Label = CreateLabel(Renderer.transform, "Label", font, labelOrder);
            }

            public bool IsVisible => Renderer.gameObject.activeSelf;

            /// <summary>Shows <paramref name="objectId"/>, or hides the tile when it is null.</summary>
            public void Show(string objectId, float brightness)
            {
                if (objectId == null)
                {
                    Renderer.gameObject.SetActive(false);
                    return;
                }

                var style = PlaceholderPalette.Get(objectId);
                Renderer.gameObject.SetActive(true);
                Renderer.color = Dim(style.Color, brightness);
                Label.text = style.Letter;
                Label.color = TextColor(Dim(style.LetterColor, brightness));
            }

            private static Color32 Dim(Color32 c, float brightness)
            {
                return new Color32((byte)(c.r * brightness), (byte)(c.g * brightness), (byte)(c.b * brightness), c.a);
            }
        }
    }
}
