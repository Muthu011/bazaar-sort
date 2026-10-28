using System;
using Game.Core;
using UnityEngine;
using Object = UnityEngine.Object;

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
    ///
    /// Input feedback (<see cref="IBoardInputView"/>): a marigold highlight behind a lifted tile for the tap
    /// selection, the dragged tile following the pointer above everything, and the glide back / shake from
    /// <see cref="TileMotion"/>. One tile moves at a time; starting a new one finishes the old one at once.
    ///
    /// Clear and reveal feedback (issue #6): the Core events Cleared and LayerRevealed queue steps per shelf, played
    /// one after another with <see cref="ClearRevealMotion"/> (pop and fade, slide forward). The queue is a fixed
    /// array per shelf made in <see cref="Bind"/>, steps are structs holding string references from the board, so
    /// playing them allocates nothing. Animations are visual only: input stays live, and anything that touches an
    /// animating shelf (a move from or to it, a selection, a drag, a shake) first snaps that shelf to its final state.
    /// Won and Stuck are shown (<see cref="OutcomeReady"/>) once every animation has finished; from the Won or Stuck
    /// event on, <see cref="InputLocked"/> is true.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardInputView
    {
        public const float PixelsPerUnit = 100f;

        private const int ShelfOrder = 0;
        private const int HiddenTileOrder = 1;
        private const int HiddenLabelOrder = 2;
        private const int HighlightOrder = 3;
        private const int FrontTileOrder = 4;
        private const int FrontLabelOrder = 5;
        private const int DragTileOrder = 20;
        private const int DragLabelOrder = 21;
        private const int StatusOrder = 30;
        private const int LabelFontSize = 64;
        private const float LabelHeightInTile = 0.6f;

        internal const string WonText = "Cleared!";
        internal const string StuckText = "No moves left";

        private static readonly Color32 BackgroundColor = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 ShelfColor = new Color32(0xA1, 0x88, 0x7F, 0xFF);
        private static readonly Color32 StatusColor = new Color32(0xFF, 0xF8, 0xE1, 0xFF);
        internal const float HiddenDim = 0.55f;

        /// <summary>Queued clear/reveal steps per shelf. A longer chain than this snaps the shelf to its end state.</summary>
        internal const int MaxQueuedSteps = 16;

        /// <summary>Marigold: reads on the shelf brown, the dark background and every placeholder tile colour.</summary>
        private static readonly Color32 HighlightColor = new Color32(0xFF, 0xB3, 0x00, 0xFF);

        /// <summary>Highlight side as a multiple of the tile side.</summary>
        internal const float HighlightScale = 1.22f;

        /// <summary>How far the selected tile lifts, in tiles.</summary>
        internal const float SelectedLiftTiles = 0.1f;

        /// <summary>Dragged tile scale.</summary>
        internal const float DragScale = 1.1f;

        private Board _board;
        private Camera _camera;
        private BoardLayoutSettings _settings;
        private BoardLayout _layout;
        private ShelfVisual[] _shelves;
        private TextMesh _status;
        private Font _font;
        private SpriteRenderer _highlight;

        private int _selectedShelf = -1;
        private int _selectedSlot = -1;
        private int _dragShelf = -1;
        private int _dragSlot = -1;

        // The one tile gliding back or shaking, if any (-1 when none).
        private int _motionShelf = -1;
        private int _motionSlot = -1;
        private Vector2 _motionFrom;
        private float _motionElapsed;
        private bool _motionGlide;
        private bool _motionShake;

        // Clear / reveal animations and the outcome they hold back.
        private int _animatingShelves;
        private BoardOutcome _pendingOutcome;
        private BoardOutcome _outcome;

        /// <summary>
        /// Raised once per board, after Won or Stuck, when every clear and reveal animation has finished: the moment to
        /// show the Win or Stuck panel.
        /// </summary>
        public event Action<BoardOutcome> OutcomeReady;

        /// <summary>The board being drawn, or null when unbound.</summary>
        public Board Board => _board;

        /// <summary>The current layout, or null when unbound.</summary>
        public BoardLayout Layout => _layout;

        /// <summary>True while any shelf is playing a clear or reveal.</summary>
        public bool IsAnimating => _animatingShelves > 0;

        /// <summary>The outcome shown through <see cref="OutcomeReady"/>, or None.</summary>
        public BoardOutcome Outcome => _outcome;

        /// <summary>True once the board is won or stuck: board input stops (the result panel takes over).</summary>
        public bool InputLocked => _pendingOutcome != BoardOutcome.None || _outcome != BoardOutcome.None;

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
            _highlight = CreateSprite(transform, "Selection Highlight", HighlightOrder);
            _highlight.color = HighlightColor;
            _highlight.gameObject.SetActive(false);
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
            _highlight = null;
            _layout = null;
            _selectedShelf = _selectedSlot = -1;
            _dragShelf = _dragSlot = -1;
            _motionShelf = _motionSlot = -1;
            _animatingShelves = 0;
            _pendingOutcome = BoardOutcome.None;
            _outcome = BoardOutcome.None;
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

            CompleteAnimations();
            _layout = BoardLayout.Compute(_board.ShelfCount, Shelf.SlotCount, safeArea, _settings, ScreenDensity.Current);
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

            // Every tile is back home now: drop any running motion, then re-apply the selection lift and the drag.
            _motionShelf = _motionSlot = -1;
            _highlight.size = Vector2.one * (_layout.TileSize * HighlightScale / PixelsPerUnit);
            ApplySelectionVisual();
            if (_dragShelf >= 0) SetDragOrder(_shelves[_dragShelf].Front[_dragSlot], true);

            // Status line sits in the HUD reserve above the playfield.
            var playfield = _layout.Playfield;
            float statusY = (playfield.yMax + safeArea.yMax) * 0.5f;
            _status.transform.position = new Vector3(playfield.center.x / PixelsPerUnit, statusY / PixelsPerUnit, 0f);
            _status.characterSize = labelScale;
        }

        // ---- Input feedback (IBoardInputView) ----

        /// <summary>Highlights and lifts the tile at (<paramref name="shelf"/>, <paramref name="slot"/>).</summary>
        public void ShowSelection(int shelf, int slot)
        {
            if (_board == null) return;
            FinishMotion();
            ClearSelection();
            CompleteShelf(shelf);
            _selectedShelf = shelf;
            _selectedSlot = slot;
            ApplySelectionVisual();
        }

        /// <summary>Removes the highlight and puts the selected tile back down.</summary>
        public void ClearSelection()
        {
            if (_board == null) return;
            if (_selectedShelf >= 0) ResetTile(_selectedShelf, _selectedSlot);
            _selectedShelf = _selectedSlot = -1;
            _highlight.gameObject.SetActive(false);
        }

        /// <summary>Lifts the tile above everything and starts following <paramref name="screenPosition"/>.</summary>
        public void BeginDrag(int shelf, int slot, Vector2 screenPosition)
        {
            if (_board == null) return;
            FinishMotion();
            ClearSelection();
            CompleteShelf(shelf);
            _dragShelf = shelf;
            _dragSlot = slot;
            SetDragOrder(_shelves[shelf].Front[slot], true);
            DragTo(screenPosition);
        }

        /// <summary>Moves the dragged tile's centre to <paramref name="screenPosition"/>.</summary>
        public void DragTo(Vector2 screenPosition)
        {
            if (_board == null || _dragShelf < 0) return;
            SetPixelPosition(_shelves[_dragShelf].Front[_dragSlot].Renderer.transform, screenPosition);
        }

        /// <summary>Ends the drag: reset at once when the move landed, otherwise glide home (and shake when refused).</summary>
        public void EndDrag(int shelf, int slot, DropFeedback feedback)
        {
            if (_board == null || shelf < 0) return;
            var tile = _shelves[shelf].Front[slot];
            Vector2 from = tile.Renderer.transform.position * PixelsPerUnit;
            _dragShelf = _dragSlot = -1;
            SetDragOrder(tile, false);

            if (feedback == DropFeedback.Landed)
            {
                ResetTile(shelf, slot);
                return;
            }
            StartMotion(shelf, slot, from, true, feedback == DropFeedback.SnapBackWithShake);
        }

        /// <summary>Shakes a tile in place: the "not allowed" cue for a refused tap move.</summary>
        public void Shake(int shelf, int slot)
        {
            if (_board == null || shelf < 0) return;
            FinishMotion();
            CompleteShelf(shelf);
            StartMotion(shelf, slot, HomePixel(shelf, slot), false, true);
        }

        private void Update()
        {
            AdvanceAnimations(Time.deltaTime);
            UpdateTileMotion();
        }

        private void UpdateTileMotion()
        {
            if (_motionShelf < 0) return;

            _motionElapsed += Time.deltaTime;
            if (_motionElapsed >= TileMotion.Duration(_motionGlide, _motionShake))
            {
                FinishMotion();
                return;
            }

            var position = TileMotion.Position(_motionFrom, HomePixel(_motionShelf, _motionSlot), _motionElapsed,
                _motionGlide, _motionShake, TileMotion.ShakeAmplitudeTiles * _layout.TileSize);
            SetPixelPosition(_shelves[_motionShelf].Front[_motionSlot].Renderer.transform, position);
        }

        private void StartMotion(int shelf, int slot, Vector2 from, bool glide, bool shake)
        {
            FinishMotion();
            _motionShelf = shelf;
            _motionSlot = slot;
            _motionFrom = from;
            _motionElapsed = 0f;
            _motionGlide = glide;
            _motionShake = shake;
        }

        private void FinishMotion()
        {
            if (_motionShelf < 0) return;
            int shelf = _motionShelf;
            int slot = _motionSlot;
            _motionShelf = _motionSlot = -1;
            ResetTile(shelf, slot);
        }

        private void ApplySelectionVisual()
        {
            if (_selectedShelf < 0) return;
            var lifted = HomePixel(_selectedShelf, _selectedSlot) + new Vector2(0f, SelectedLiftTiles * _layout.TileSize);
            SetPixelPosition(_shelves[_selectedShelf].Front[_selectedSlot].Renderer.transform, lifted);
            SetPixelPosition(_highlight.transform, lifted);
            _highlight.gameObject.SetActive(true);
        }

        /// <summary>Puts a front tile back at its slot, normal size and draw order.</summary>
        private void ResetTile(int shelf, int slot)
        {
            var tile = _shelves[shelf].Front[slot];
            if (tile.Animating) return; // a clear or reveal owns this tile's transform until its step ends
            SetDragOrder(tile, false);
            SetPixelPosition(tile.Renderer.transform, HomePixel(shelf, slot));
        }

        private Vector2 HomePixel(int shelf, int slot) => _layout.GetFrontSlotRect(shelf, slot).center;

        private static void SetDragOrder(Tile tile, bool dragging)
        {
            tile.SetOrder(dragging ? DragTileOrder : FrontTileOrder, dragging ? DragLabelOrder : FrontLabelOrder);
            tile.Renderer.transform.localScale = dragging ? new Vector3(DragScale, DragScale, 1f) : Vector3.one;
        }

        private static void SetPixelPosition(Transform target, Vector2 pixels)
        {
            target.position = new Vector3(pixels.x / PixelsPerUnit, pixels.y / PixelsPerUnit, 0f);
        }

        private void OnMoved(MoveInfo info)
        {
            CompleteShelf(info.FromShelf);
            CompleteShelf(info.ToShelf);
            RefreshShelf(info.FromShelf);
            RefreshShelf(info.ToShelf);
        }

        private void OnCleared(ClearInfo info)
        {
            var step = new AnimStep { IsReveal = false, Item = info.ItemId };
            Enqueue(info.ShelfIndex, step);
        }

        private void OnLayerRevealed(LayerRevealInfo info)
        {
            var shelf = _board.GetShelf(info.ShelfIndex);
            bool hasHidden = shelf.HiddenLayerCount > 0;
            var step = new AnimStep { IsReveal = true };
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                step.SetFront(slot, shelf.GetFrontItem(slot));
                step.SetHidden(slot, hasHidden ? shelf.GetHiddenItem(0, slot) : null);
            }
            Enqueue(info.ShelfIndex, step);
        }

        private void OnWon()
        {
            _status.text = WonText;
            _pendingOutcome = BoardOutcome.Won;
        }

        private void OnStuck()
        {
            _status.text = StuckText;
            _pendingOutcome = BoardOutcome.Stuck;
        }

        // ---- Clear / reveal animations ----

        /// <summary>
        /// Plays queued clears and reveals forward by <paramref name="seconds"/>, then raises <see cref="OutcomeReady"/>
        /// if the board is won or stuck and nothing is animating any more. <see cref="Update"/> calls it with
        /// <see cref="Time.deltaTime"/>; tests and the screenshot tool call it directly. Allocates nothing.
        /// </summary>
        public void AdvanceAnimations(float seconds)
        {
            if (_board == null) return;

            if (_animatingShelves > 0)
            {
                for (int i = 0; i < _shelves.Length; i++)
                {
                    var visual = _shelves[i];
                    if (visual.StepCount == 0) continue;
                    float remaining = seconds;
                    // A long frame may finish several steps; carry the leftover time into the next one.
                    while (visual.StepCount > 0)
                    {
                        float duration = StepDuration(visual.Steps[visual.StepHead]);
                        float left = duration - visual.StepElapsed;
                        if (remaining < left)
                        {
                            visual.StepElapsed += remaining;
                            ApplyStep(i, visual.StepElapsed);
                            break;
                        }
                        remaining -= left;
                        EndCurrentStep(i);
                    }
                }
            }

            if (_animatingShelves == 0 && _pendingOutcome != BoardOutcome.None)
            {
                _outcome = _pendingOutcome;
                _pendingOutcome = BoardOutcome.None;
                OutcomeReady?.Invoke(_outcome);
            }
        }

        /// <summary>Finishes every running clear and reveal at once (relayout, tests).</summary>
        public void CompleteAnimations()
        {
            if (_shelves == null) return;
            for (int i = 0; i < _shelves.Length; i++) CompleteShelf(i);
        }

        private static float StepDuration(in AnimStep step) =>
            step.IsReveal ? ClearRevealMotion.RevealSeconds : ClearRevealMotion.ClearSeconds;

        private void Enqueue(int shelf, AnimStep step)
        {
            var visual = _shelves[shelf];
            if (visual.StepCount >= MaxQueuedSteps)
            {
                // Pathological chain: drop the animation and show the end state.
                CompleteShelf(shelf);
                return;
            }

            int index = (visual.StepHead + visual.StepCount) % MaxQueuedSteps;
            visual.Steps[index] = step;
            visual.StepCount++;
            if (visual.StepCount == 1)
            {
                _animatingShelves++;
                BeginCurrentStep(shelf);
            }
        }

        private void BeginCurrentStep(int shelf)
        {
            var visual = _shelves[shelf];
            visual.StepElapsed = 0f;
            ref var step = ref visual.Steps[visual.StepHead];

            if (step.IsReveal)
            {
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    var tile = visual.Front[slot];
                    tile.Show(step.GetFront(slot), HiddenDim);
                    tile.Animating = tile.IsVisible;
                    visual.Hidden[slot].Show(step.GetHidden(slot), HiddenDim);
                }
            }
            else
            {
                // The tiles showing the cleared item are the ones that pop (the move or reveal before put them there).
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    var tile = visual.Front[slot];
                    tile.Animating = tile.IsVisible && tile.ObjectId == step.Item;
                    if (tile.Animating) SetDragOrder(tile, false);
                }
            }
            ApplyStep(shelf, 0f);
        }

        private void ApplyStep(int shelf, float elapsed)
        {
            var visual = _shelves[shelf];
            ref var step = ref visual.Steps[visual.StepHead];
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                var tile = visual.Front[slot];
                if (!tile.Animating) continue;
                var t = tile.Renderer.transform;

                if (step.IsReveal)
                {
                    var hidden = _layout.GetHiddenSlotRect(shelf, slot);
                    var front = _layout.GetFrontSlotRect(shelf, slot);
                    float hiddenScale = hidden.height / front.height;
                    SetPixelPosition(t, ClearRevealMotion.RevealPosition(hidden.center, front.center, elapsed));
                    float s = ClearRevealMotion.RevealScale(hiddenScale, elapsed);
                    t.localScale = new Vector3(s, s, 1f);
                    tile.SetTint(ClearRevealMotion.RevealBrightness(HiddenDim, elapsed), 1f);
                }
                else
                {
                    float s = ClearRevealMotion.ClearScale(elapsed);
                    t.localScale = new Vector3(s, s, 1f);
                    tile.SetTint(1f, ClearRevealMotion.ClearAlpha(elapsed));
                }
            }
        }

        private void EndCurrentStep(int shelf)
        {
            var visual = _shelves[shelf];
            bool wasReveal = visual.Steps[visual.StepHead].IsReveal;
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                var tile = visual.Front[slot];
                if (!tile.Animating) continue;
                tile.Animating = false;
                if (wasReveal)
                {
                    tile.SetTint(1f, 1f);
                }
                else
                {
                    tile.Show(null, 1f);
                }
                ResetTile(shelf, slot);
            }

            visual.Steps[visual.StepHead] = default;
            visual.StepHead = (visual.StepHead + 1) % MaxQueuedSteps;
            visual.StepCount--;

            if (visual.StepCount > 0)
            {
                BeginCurrentStep(shelf);
            }
            else
            {
                _animatingShelves--;
                RefreshShelf(shelf); // settle on the board's real state
            }
        }

        /// <summary>Drops any queued animation on <paramref name="shelf"/> and draws its current state at rest.</summary>
        private void CompleteShelf(int shelf)
        {
            var visual = _shelves[shelf];
            if (visual.StepCount == 0) return;

            for (int i = 0; i < MaxQueuedSteps; i++) visual.Steps[i] = default;
            visual.StepHead = 0;
            visual.StepCount = 0;
            _animatingShelves--;
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                visual.Front[slot].Animating = false;
                ResetTile(shelf, slot);
            }
            RefreshShelf(shelf);
        }

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

        internal int SelectedShelf => _selectedShelf;
        internal int SelectedSlot => _selectedSlot;
        internal bool IsHighlightVisible => _highlight != null && _highlight.gameObject.activeSelf;
        internal Vector2 HighlightPixelPosition => _highlight.transform.position * PixelsPerUnit;
        internal bool IsDragging => _dragShelf >= 0;
        internal bool IsTileMoving => _motionShelf >= 0;

        /// <summary>True while the moving tile is shaking (a refused move), false while it only glides or is idle.</summary>
        internal bool IsShaking => _motionShelf >= 0 && _motionShake;

        /// <summary>Centre of a front tile right now, in pixels.</summary>
        internal Vector2 GetFrontTilePixelPosition(int shelf, int slot) => _shelves[shelf].Front[slot].Renderer.transform.position * PixelsPerUnit;

        /// <summary>Where a front tile rests, in pixels.</summary>
        internal Vector2 GetFrontTileHome(int shelf, int slot) => HomePixel(shelf, slot);

        internal int GetFrontSortingOrder(int shelf, int slot) => _shelves[shelf].Front[slot].Renderer.sortingOrder;

        /// <summary>Uniform scale of a front tile right now.</summary>
        internal float GetFrontTileScale(int shelf, int slot) => _shelves[shelf].Front[slot].Renderer.transform.localScale.x;

        /// <summary>Opacity of a front tile right now.</summary>
        internal float GetFrontTileAlpha(int shelf, int slot) => _shelves[shelf].Front[slot].Renderer.color.a;

        /// <summary>True while <paramref name="shelf"/> has clears or reveals queued or playing.</summary>
        internal bool IsShelfAnimating(int shelf) => _shelves[shelf].StepCount > 0;

        /// <summary>True when the step playing on <paramref name="shelf"/> is a reveal (false for a clear or none).</summary>
        internal bool IsShelfRevealing(int shelf) =>
            _shelves[shelf].StepCount > 0 && _shelves[shelf].Steps[_shelves[shelf].StepHead].IsReveal;

        /// <summary>Where a hidden-strip tile rests, in pixels.</summary>
        internal Vector2 GetHiddenTileHome(int shelf, int slot) => _layout.GetHiddenSlotRect(shelf, slot).center;

        /// <summary>One queued clear or reveal. Strings are references to the board's ids: copying allocates nothing.</summary>
        private struct AnimStep
        {
            public bool IsReveal;

            /// <summary>Cleared object id (clear steps).</summary>
            public string Item;

            // Front row after the reveal and the hidden row behind it (reveal steps). Shelf.SlotCount is 3.
            private string _front0, _front1, _front2;
            private string _hidden0, _hidden1, _hidden2;

            public string GetFront(int slot) => slot == 0 ? _front0 : slot == 1 ? _front1 : _front2;
            public string GetHidden(int slot) => slot == 0 ? _hidden0 : slot == 1 ? _hidden1 : _hidden2;

            public void SetFront(int slot, string id)
            {
                if (slot == 0) _front0 = id;
                else if (slot == 1) _front1 = id;
                else _front2 = id;
            }

            public void SetHidden(int slot, string id)
            {
                if (slot == 0) _hidden0 = id;
                else if (slot == 1) _hidden1 = id;
                else _hidden2 = id;
            }
        }

        /// <summary>The visuals for one shelf. Plain class: one MonoBehaviour per board, not per tile.</summary>
        private sealed class ShelfVisual
        {
            public readonly SpriteRenderer Background;
            public readonly Tile[] Front = new Tile[Shelf.SlotCount];
            public readonly Tile[] Hidden = new Tile[Shelf.SlotCount];
            public readonly AnimStep[] Steps = new AnimStep[MaxQueuedSteps];
            public int StepHead;
            public int StepCount;
            public float StepElapsed;

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
                _labelRenderer = Label.GetComponent<MeshRenderer>();
            }

            private readonly MeshRenderer _labelRenderer;
            private Color32 _baseColor;
            private Color32 _baseLetterColor;

            /// <summary>Object id shown, or null when hidden.</summary>
            public string ObjectId { get; private set; }

            /// <summary>True while a clear or reveal step drives this tile.</summary>
            public bool Animating;

            public bool IsVisible => Renderer.gameObject.activeSelf;

            public void SetOrder(int tileOrder, int labelOrder)
            {
                Renderer.sortingOrder = tileOrder;
                _labelRenderer.sortingOrder = labelOrder;
            }

            /// <summary>Shows <paramref name="objectId"/>, or hides the tile when it is null.</summary>
            public void Show(string objectId, float brightness)
            {
                ObjectId = objectId;
                if (objectId == null)
                {
                    Renderer.gameObject.SetActive(false);
                    return;
                }

                var style = PlaceholderPalette.Get(objectId);
                _baseColor = style.Color;
                _baseLetterColor = style.LetterColor;
                Renderer.gameObject.SetActive(true);
                Label.text = style.Letter;
                SetTint(brightness, 1f);
            }

            /// <summary>Re-tints the shown object: <paramref name="brightness"/> 0..1 and opacity <paramref name="alpha"/> 0..1.</summary>
            public void SetTint(float brightness, float alpha)
            {
                var color = Dim(_baseColor, brightness);
                color.a = (byte)(alpha * 255f + 0.5f);
                Renderer.color = color;
                Color letter = TextColor(Dim(_baseLetterColor, brightness));
                letter.a = alpha;
                Label.color = letter;
            }

            private static Color32 Dim(Color32 c, float brightness)
            {
                return new Color32((byte)(c.r * brightness), (byte)(c.g * brightness), (byte)(c.b * brightness), c.a);
            }
        }
    }
}
