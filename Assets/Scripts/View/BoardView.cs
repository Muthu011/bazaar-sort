using System;
using Game.Core;
using Game.Services;
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
    ///
    /// Locked shelves (issue #19): a dim scrim over the shelf and a placeholder padlock on its top-right corner showing
    /// the clears left (<see cref="Shelf.LockClearsLeft"/>). The number is re-read from the board on every Cleared and
    /// bumps (<see cref="LockMotion"/>) when it goes down; on ShelfUnlocked the padlock pops off (grows and fades) and
    /// the scrim fades with it. The view only shows the lock; the board refuses the moves.
    ///
    /// Game feel (issue #21, <see cref="FeelMotion"/>): a picked tile squashes and settles over a soft shadow; every
    /// move flies the item to its new slot on an arc (a Fly step queued on the target shelf, so a clear it causes pops
    /// after it lands); clears squash as they pop and raise <see cref="ClearShown"/> (burst, coin) at the top of the
    /// pop; reveals bounce into place. A refused move buzzes and a clear ticks through <see cref="IHaptics"/>, which
    /// respects the player's setting. All still on the one allocation-free clock, <see cref="AdvanceAnimations"/>.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardInputView
    {
        public const float PixelsPerUnit = 100f;

        private const int ShelfOrder = 0;
        private const int HiddenTileOrder = 1;
        private const int HiddenLabelOrder = 2;
        private const int HighlightOrder = 3;
        private const int ShadowOrder = 4;
        private const int FrontTileOrder = 5;
        private const int FrontLabelOrder = 6;
        private const int DragTileOrder = 20;
        private const int DragLabelOrder = 21;
        private const int LockScrimOrder = 10;
        private const int PadlockOrder = 11;
        private const int PadlockLabelOrder = 12;
        private const int StatusOrder = 30;
        private const int LabelFontSize = 64;
        private const float LabelHeightInTile = 0.6f;

        internal const string WonText = "Cleared!";
        internal const string StuckText = "No moves left";

        private static readonly Color32 BackgroundColor = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 ShelfColor = new Color32(0xA1, 0x88, 0x7F, 0xFF);
        private static readonly Color32 StatusColor = new Color32(0xFF, 0xF8, 0xE1, 0xFF);
        internal const float HiddenDim = 0.55f;

        /// <summary>Brass padlock, dark number: reads on the shelf brown and the dark background.</summary>
        private static readonly Color32 PadlockColor = new Color32(0xE0, 0xA5, 0x26, 0xFF);
        private static readonly Color32 PadlockTextColor = new Color32(0x3E, 0x27, 0x23, 0xFF);

        /// <summary>Dark scrim over a locked shelf: its items stay readable but look shut away.</summary>
        private static readonly Color32 LockScrimColor = new Color32(0x00, 0x00, 0x00, 0x55);

        /// <summary>Padlock width as a multiple of the tile side.</summary>
        internal const float PadlockWidthTiles = 0.55f;

        /// <summary>Number height as a fraction of the padlock body's height.</summary>
        private const float PadlockNumberHeight = 0.7f;

        // "0".."99" made once, so showing a count never allocates.
        private static readonly string[] CountTexts = BuildCountTexts(100);

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

        /// <summary>Soft shadow under a lifted, dragged or flying tile: width in tiles, and how dark.</summary>
        internal const float ShadowWidthTiles = 0.95f;
        private static readonly Color32 ShadowColor = new Color32(0x00, 0x00, 0x00, 0x50);

        /// <summary>Shadow centre below a resting tile's centre, in tiles (peeks out under the lifted tile).</summary>
        private const float ShadowDropTiles = 0.42f;

        /// <summary>Shadow centre below a dragged tile's centre, in tiles (it is "higher", so the shadow falls further).</summary>
        private const float DragShadowDropTiles = 0.6f;

        private Board _board;
        private Camera _camera;
        private BoardLayoutSettings _settings;
        private BoardLayout _layout;
        private ShelfVisual[] _shelves;
        private TextMesh _status;
        private Font _font;
        private SpriteRenderer _highlight;
        private SpriteRenderer _shadow;
        private IHaptics _haptics;

        // Pick-up squash of the selected or dragged tile (-1 when idle).
        private float _pickElapsed = -1f;

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
        private int _lockMotions;
        private BoardOutcome _pendingOutcome;
        private BoardOutcome _outcome;
        private bool _inputHeld;

        /// <summary>
        /// Raised once per board, after Won or Stuck, when every clear and reveal animation has finished: the moment to
        /// show the Win or Stuck panel.
        /// </summary>
        public event Action<BoardOutcome> OutcomeReady;

        /// <summary>
        /// Raised once per Core clear, at the top of its pop (or at once when the pop is skipped): the moment for the
        /// particle burst, the coin and the clear haptic. See <see cref="ClearBurst"/>.
        /// </summary>
        public event Action<ClearBurst> ClearShown;

        /// <summary>Haptics for wrong moves and clears; <see cref="GameServices.Haptics"/> unless set (tests).</summary>
        public IHaptics Haptics
        {
            get => _haptics ?? GameServices.Haptics;
            set => _haptics = value;
        }

        /// <summary>The board being drawn, or null when unbound.</summary>
        public Board Board => _board;

        /// <summary>The current layout, or null when unbound.</summary>
        public BoardLayout Layout => _layout;

        /// <summary>True while any shelf is playing a clear or reveal.</summary>
        public bool IsAnimating => _animatingShelves > 0;

        /// <summary>The outcome shown through <see cref="OutcomeReady"/>, or None.</summary>
        public BoardOutcome Outcome => _outcome;

        /// <summary>True once the board is won or stuck: board input stops (the result panel takes over).</summary>
        public bool InputLocked => _inputHeld || InputBlocked || _pendingOutcome != BoardOutcome.None || _outcome != BoardOutcome.None;

        /// <summary>
        /// Set by a screen that covers the board and owns the pointer while it is open (the dev level select). Unlike
        /// <see cref="HoldInput"/>, it survives level loads; the screen clears it when it closes.
        /// </summary>
        public bool InputBlocked { get; set; }

        /// <summary>
        /// Stops board input until the next <see cref="Bind"/>: the board stays on screen under a panel that owns the
        /// pointer ("More levels coming soon" when the game opens with every level won).
        /// </summary>
        public void HoldInput() => _inputHeld = true;

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
            _shadow = CreateShadow(transform, "Pick Shadow");
            _status = CreateLabel(transform, "Status", _font, StatusOrder);
            _status.text = string.Empty;
            _status.color = TextColor(StatusColor);

            _board.Moved += OnMoved;
            _board.Cleared += OnCleared;
            _board.LayerRevealed += OnLayerRevealed;
            _board.ShelfUnlocked += OnShelfUnlocked;
            _board.Won += OnWon;
            _board.Stuck += OnStuck;

            Relayout(safeArea);
            for (int i = 0; i < _shelves.Length; i++)
            {
                RefreshShelf(i);
                InitLock(i);
            }
        }

        /// <summary>Stops listening to the board and destroys the shelf objects.</summary>
        public void Unbind()
        {
            if (_board != null)
            {
                _board.Moved -= OnMoved;
                _board.Cleared -= OnCleared;
                _board.LayerRevealed -= OnLayerRevealed;
                _board.ShelfUnlocked -= OnShelfUnlocked;
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
            _shadow = null;
            _pickElapsed = -1f;
            _layout = null;
            _selectedShelf = _selectedSlot = -1;
            _dragShelf = _dragSlot = -1;
            _motionShelf = _motionSlot = -1;
            _animatingShelves = 0;
            _lockMotions = 0;
            _pendingOutcome = BoardOutcome.None;
            _outcome = BoardOutcome.None;
            _inputHeld = false;
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
            float padlockWidth = _layout.TileSize * PadlockWidthTiles;
            float padlockNumberScale = padlockWidth * PlaceholderSprites.PadlockBodyHeight * PadlockNumberHeight
                / PixelsPerUnit * 10f / LabelFontSize;

            for (int i = 0; i < _shelves.Length; i++)
            {
                var shelf = _shelves[i];
                var shelfRect = _layout.GetShelfRect(i);
                Place(shelf.Background, shelfRect);
                Place(shelf.Scrim, shelfRect);
                SizeShadow(shelf.FlightShadow);
                // Padlock on the shelf's top-right corner, straddling the top edge, clear of the front row.
                SetPixelPosition(shelf.Lock, new Vector2(shelfRect.xMax - 0.35f * padlockWidth, shelfRect.yMax - 0.1f * padlockWidth));
                float w = padlockWidth / PixelsPerUnit;
                shelf.LockSprite.transform.localScale = new Vector3(w, w, 1f);
                shelf.LockLabel.transform.localPosition = new Vector3(0f, -PlaceholderSprites.PadlockBodyCentreOffset * w, 0f);
                shelf.LockLabel.characterSize = padlockNumberScale;
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    Place(shelf.Front[slot], _layout.GetFrontSlotRect(i, slot), labelScale);
                    Place(shelf.Hidden[slot], _layout.GetHiddenSlotRect(i, slot), labelScale * _settings.HiddenStripHeight);
                }
            }

            // Every tile is back home now: drop any running motion, then re-apply the selection lift and the drag.
            _motionShelf = _motionSlot = -1;
            _highlight.size = Vector2.one * (_layout.TileSize * HighlightScale / PixelsPerUnit);
            SizeShadow(_shadow);
            _pickElapsed = -1f;
            ApplySelectionVisual();
            if (_dragShelf >= 0)
            {
                var dragged = _shelves[_dragShelf].Front[_dragSlot];
                SetDragOrder(dragged, true);
                PlaceDragShadow(dragged.Renderer.transform.position * PixelsPerUnit);
            }

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
            StartPickUp();
        }

        /// <summary>Removes the highlight and puts the selected tile back down.</summary>
        public void ClearSelection()
        {
            if (_board == null) return;
            if (_selectedShelf >= 0) ResetTile(_selectedShelf, _selectedSlot);
            _selectedShelf = _selectedSlot = -1;
            _highlight.gameObject.SetActive(false);
            if (_dragShelf < 0) HidePickUp();
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
            StartPickUp();
        }

        /// <summary>Moves the dragged tile's centre to <paramref name="screenPosition"/>.</summary>
        public void DragTo(Vector2 screenPosition)
        {
            if (_board == null || _dragShelf < 0) return;
            SetPixelPosition(_shelves[_dragShelf].Front[_dragSlot].Renderer.transform, screenPosition);
            PlaceDragShadow(screenPosition);
        }

        /// <summary>Ends the drag: reset at once when the move landed, otherwise glide home (and shake when refused).</summary>
        public void EndDrag(int shelf, int slot, DropFeedback feedback)
        {
            if (_board == null || shelf < 0) return;
            var tile = _shelves[shelf].Front[slot];
            Vector2 from = tile.Renderer.transform.position * PixelsPerUnit;
            _dragShelf = _dragSlot = -1;
            SetDragOrder(tile, false);
            HidePickUp();

            if (feedback == DropFeedback.Landed)
            {
                ResetTile(shelf, slot);
                return;
            }
            bool shake = feedback == DropFeedback.SnapBackWithShake;
            StartMotion(shelf, slot, from, true, shake);
            if (shake) Haptics.Play(HapticPulse.WrongMove);
        }

        /// <summary>Shakes a tile in place: the "not allowed" cue for a refused tap move.</summary>
        public void Shake(int shelf, int slot)
        {
            if (_board == null || shelf < 0) return;
            FinishMotion();
            CompleteShelf(shelf);
            StartMotion(shelf, slot, HomePixel(shelf, slot), false, true);
            Haptics.Play(HapticPulse.WrongMove);
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
            var home = HomePixel(_selectedShelf, _selectedSlot);
            var lifted = home + new Vector2(0f, SelectedLiftTiles * _layout.TileSize);
            SetPixelPosition(_shelves[_selectedShelf].Front[_selectedSlot].Renderer.transform, lifted);
            SetPixelPosition(_highlight.transform, lifted);
            _highlight.gameObject.SetActive(true);
            SetPixelPosition(_shadow.transform, home - new Vector2(0f, ShadowDropTiles * _layout.TileSize));
            _shadow.gameObject.SetActive(true);
        }

        // ---- Pick-up squash and shadow (issue #21) ----

        private void StartPickUp()
        {
            _pickElapsed = 0f;
            ApplyPickUp();
        }

        private void HidePickUp()
        {
            _pickElapsed = -1f;
            if (_shadow != null) _shadow.gameObject.SetActive(false);
        }

        private void PlaceDragShadow(Vector2 tilePixel)
        {
            SetPixelPosition(_shadow.transform, tilePixel - new Vector2(0f, DragShadowDropTiles * _layout.TileSize));
            _shadow.gameObject.SetActive(true);
        }

        private void AdvancePickUp(float seconds)
        {
            if (_pickElapsed < 0f) return;
            _pickElapsed += seconds;
            ApplyPickUp();
            if (_pickElapsed >= FeelMotion.PickUpSeconds) _pickElapsed = -1f;
        }

        /// <summary>Scales the selected or dragged tile by the pick-up squash on top of its resting or drag scale.</summary>
        private void ApplyPickUp()
        {
            int shelf = _dragShelf >= 0 ? _dragShelf : _selectedShelf;
            int slot = _dragShelf >= 0 ? _dragSlot : _selectedSlot;
            if (shelf < 0)
            {
                _pickElapsed = -1f;
                return;
            }
            var tile = _shelves[shelf].Front[slot];
            if (tile.Animating) return;
            float baseScale = _dragShelf >= 0 ? DragScale : 1f;
            var s = FeelMotion.PickUpScale(_pickElapsed);
            tile.Renderer.transform.localScale = new Vector3(baseScale * s.x, baseScale * s.y, 1f);
        }

        private void SizeShadow(SpriteRenderer shadow)
        {
            float w = _layout.TileSize * ShadowWidthTiles / PixelsPerUnit;
            shadow.transform.localScale = new Vector3(w, w, 1f);
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
            // Fly from wherever the player sees the item: under the finger, lifted, or at rest in its slot.
            Vector2 from = _shelves[info.FromShelf].Front[info.FromSlot].Renderer.transform.position * PixelsPerUnit;
            CompleteShelf(info.FromShelf);
            CompleteShelf(info.ToShelf);
            RefreshShelf(info.FromShelf);
            RefreshShelf(info.ToShelf);

            Vector2 to = HomePixel(info.ToShelf, info.ToSlot);
            var step = new AnimStep
            {
                Kind = StepKind.Fly,
                Slot = info.ToSlot,
                From = from,
                Height = FeelMotion.ArcHeight(Vector2.Distance(from, to), _layout.TileSize),
            };
            Enqueue(info.ToShelf, step);
        }

        private void OnCleared(ClearInfo info)
        {
            var step = new AnimStep { Kind = StepKind.Clear, Item = info.ItemId };
            Enqueue(info.ShelfIndex, step);
            RefreshLockCounts();
        }

        private void OnLayerRevealed(LayerRevealInfo info)
        {
            var shelf = _board.GetShelf(info.ShelfIndex);
            bool hasHidden = shelf.HiddenLayerCount > 0;
            var step = new AnimStep { Kind = StepKind.Reveal };
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
            AdvanceLocks(seconds);
            AdvancePickUp(seconds);

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
            for (int i = 0; i < _shelves.Length; i++)
            {
                CompleteShelf(i);
                FinishLockMotion(i);
            }
        }

        // ---- Padlocks (issue #19) ----

        /// <summary>Shows the padlock and scrim on a locked shelf with its count, or hides them on an open one.</summary>
        private void InitLock(int index)
        {
            var visual = _shelves[index];
            var shelf = _board.GetShelf(index);
            visual.TickElapsed = -1f;
            visual.PopElapsed = -1f;
            bool locked = shelf.IsLocked;
            visual.Lock.gameObject.SetActive(locked);
            visual.Scrim.gameObject.SetActive(locked);
            if (!locked) return;
            SetLockCount(visual, shelf.LockClearsLeft);
            SetLockLook(visual, 1f, 1f);
        }

        private void OnShelfUnlocked(int index)
        {
            var visual = _shelves[index];
            if (!visual.Lock.gameObject.activeSelf) return;
            FinishLockMotion(index);
            SetLockCount(visual, 0);
            visual.PopElapsed = 0f;
            _lockMotions++;
        }

        /// <summary>Re-reads every padlock's count after a clear and bumps the ones that went down.</summary>
        private void RefreshLockCounts()
        {
            for (int i = 0; i < _shelves.Length; i++)
            {
                var visual = _shelves[i];
                if (!visual.Lock.gameObject.activeSelf || visual.PopElapsed >= 0f) continue;
                int left = _board.GetShelf(i).LockClearsLeft;
                if (left == 0 || left == visual.LockShown) continue; // 0: ShelfUnlocked follows and pops it
                SetLockCount(visual, left);
                if (visual.TickElapsed < 0f) _lockMotions++;
                visual.TickElapsed = 0f;
            }
        }

        private void AdvanceLocks(float seconds)
        {
            if (_lockMotions == 0) return;
            for (int i = 0; i < _shelves.Length; i++)
            {
                var visual = _shelves[i];
                if (visual.PopElapsed >= 0f)
                {
                    visual.PopElapsed += seconds;
                    if (visual.PopElapsed >= LockMotion.PopSeconds) FinishLockMotion(i);
                    else SetLockLook(visual, LockMotion.PopScale(visual.PopElapsed), LockMotion.PopAlpha(visual.PopElapsed));
                }
                else if (visual.TickElapsed >= 0f)
                {
                    visual.TickElapsed += seconds;
                    if (visual.TickElapsed >= LockMotion.TickSeconds) FinishLockMotion(i);
                    else SetLockLook(visual, LockMotion.TickScale(visual.TickElapsed), 1f);
                }
            }
        }

        /// <summary>Ends a running tick (back to rest) or pop (padlock and scrim hidden) at once.</summary>
        private void FinishLockMotion(int index)
        {
            var visual = _shelves[index];
            if (visual.PopElapsed >= 0f)
            {
                visual.PopElapsed = -1f;
                _lockMotions--;
                visual.Lock.gameObject.SetActive(false);
                visual.Scrim.gameObject.SetActive(false);
                SetLockLook(visual, 1f, 1f);
            }
            if (visual.TickElapsed >= 0f)
            {
                visual.TickElapsed = -1f;
                _lockMotions--;
                SetLockLook(visual, 1f, 1f);
            }
        }

        private static void SetLockCount(ShelfVisual visual, int count)
        {
            visual.LockShown = count;
            visual.LockLabel.text = count >= 0 && count < CountTexts.Length ? CountTexts[count] : count.ToString();
        }

        private static void SetLockLook(ShelfVisual visual, float scale, float alpha)
        {
            visual.Lock.localScale = new Vector3(scale, scale, 1f);
            Color32 lockColor = PadlockColor;
            lockColor.a = (byte)(alpha * 255f + 0.5f);
            visual.LockSprite.color = lockColor;
            Color text = TextColor(PadlockTextColor);
            text.a = alpha;
            visual.LockLabel.color = text;
            Color32 scrim = LockScrimColor;
            scrim.a = (byte)(LockScrimColor.a * alpha + 0.5f);
            visual.Scrim.color = scrim;
        }

        private static string[] BuildCountTexts(int count)
        {
            var texts = new string[count];
            for (int i = 0; i < count; i++) texts[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return texts;
        }

        private static float StepDuration(in AnimStep step)
        {
            switch (step.Kind)
            {
                case StepKind.Reveal: return ClearRevealMotion.RevealSeconds;
                case StepKind.Fly: return FeelMotion.ArcSeconds;
                default: return ClearRevealMotion.ClearSeconds;
            }
        }

        private void Enqueue(int shelf, AnimStep step)
        {
            var visual = _shelves[shelf];
            if (visual.StepCount >= MaxQueuedSteps)
            {
                // Pathological chain: drop the animation and show the end state.
                CompleteShelf(shelf);
                if (step.Kind == StepKind.Clear) RaiseClearShown(shelf, step.Item, false);
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

            if (step.Kind == StepKind.Fly)
            {
                var tile = visual.Front[step.Slot];
                tile.Animating = tile.IsVisible;
                if (tile.Animating)
                {
                    tile.SetOrder(DragTileOrder, DragLabelOrder);
                    visual.FlightShadow.gameObject.SetActive(true);
                }
            }
            else if (step.IsReveal)
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

            if (step.Kind == StepKind.Fly)
            {
                var flyer = visual.Front[step.Slot];
                if (!flyer.Animating) return;
                var to = HomePixel(shelf, step.Slot);
                SetPixelPosition(flyer.Renderer.transform, FeelMotion.ArcPosition(step.From, to, step.Height, elapsed));
                float fs = FeelMotion.ArcScale(elapsed);
                flyer.Renderer.transform.localScale = new Vector3(fs, fs, 1f);
                SetPixelPosition(visual.FlightShadow.transform,
                    FeelMotion.ArcGround(step.From, to, elapsed) - new Vector2(0f, ShadowDropTiles * _layout.TileSize));
                return;
            }

            if (step.Kind == StepKind.Clear && !step.Burst && elapsed >= ClearRevealMotion.ClearPopSeconds)
            {
                step.Burst = true;
                RaiseClearShown(shelf, step.Item, true);
            }

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
                    float a = ClearRevealMotion.ClearSquashAspect(elapsed);
                    t.localScale = new Vector3(s * (1f + a), s * (1f - a), 1f);
                    tile.SetTint(1f, ClearRevealMotion.ClearAlpha(elapsed));
                }
            }
        }

        private void EndCurrentStep(int shelf)
        {
            var visual = _shelves[shelf];
            ref var ending = ref visual.Steps[visual.StepHead];
            var kind = ending.Kind;
            if (kind == StepKind.Clear && !ending.Burst)
            {
                // A long frame jumped past the top of the pop: still one burst per clear.
                ending.Burst = true;
                RaiseClearShown(shelf, ending.Item, true);
            }
            visual.FlightShadow.gameObject.SetActive(false);
            for (int slot = 0; slot < Shelf.SlotCount; slot++)
            {
                var tile = visual.Front[slot];
                if (!tile.Animating) continue;
                tile.Animating = false;
                if (kind == StepKind.Reveal)
                {
                    tile.SetTint(1f, 1f);
                }
                else if (kind == StepKind.Clear)
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

            // Every clear still owes its burst (counter, haptic): skipped ones are raised as not animated.
            for (int i = 0; i < visual.StepCount; i++)
            {
                ref var queued = ref visual.Steps[(visual.StepHead + i) % MaxQueuedSteps];
                if (queued.Kind == StepKind.Clear && !queued.Burst) RaiseClearShown(shelf, queued.Item, false);
            }
            visual.FlightShadow.gameObject.SetActive(false);
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

        private void RaiseClearShown(int shelf, string item, bool animated)
        {
            // The triple fills the front row, so its centre is the middle slot's centre.
            var centre = _layout.GetFrontSlotRect(shelf, Shelf.SlotCount / 2).center;
            Haptics.Play(HapticPulse.Light);
            ClearShown?.Invoke(new ClearBurst(shelf, item, centre, animated));
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

        private static SpriteRenderer CreateShadow(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderSprites.SoftShadow;
            renderer.color = ShadowColor;
            renderer.sortingOrder = ShadowOrder;
            go.SetActive(false);
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

        /// <summary>True when the step playing on <paramref name="shelf"/> is a move flight landing on it.</summary>
        internal bool IsShelfFlying(int shelf) =>
            _shelves[shelf].StepCount > 0 && _shelves[shelf].Steps[_shelves[shelf].StepHead].Kind == StepKind.Fly;

        /// <summary>Scale (x, y) of a front tile right now.</summary>
        internal Vector2 GetFrontTileScale2(int shelf, int slot) => _shelves[shelf].Front[slot].Renderer.transform.localScale;

        internal bool IsPickShadowVisible => _shadow != null && _shadow.gameObject.activeSelf;
        internal Vector2 PickShadowPixelPosition => _shadow.transform.position * PixelsPerUnit;
        internal bool IsFlightShadowVisible(int shelf) => _shelves[shelf].FlightShadow.gameObject.activeSelf;

        /// <summary>True while the selected or dragged tile is still squashing after pick-up.</summary>
        internal bool IsPickingUp => _pickElapsed >= 0f;

        /// <summary>True while the padlock (and scrim) on <paramref name="shelf"/> is drawn, popping included.</summary>
        internal bool IsPadlockVisible(int shelf) => _shelves[shelf].Lock.gameObject.activeSelf;

        /// <summary>The number on the padlock, or null when no padlock is drawn.</summary>
        internal string GetPadlockText(int shelf) => IsPadlockVisible(shelf) ? _shelves[shelf].LockLabel.text : null;

        internal bool IsScrimVisible(int shelf) => _shelves[shelf].Scrim.gameObject.activeSelf;

        /// <summary>True while the padlock is popping off.</summary>
        internal bool IsPadlockPopping(int shelf) => _shelves[shelf].PopElapsed >= 0f;

        /// <summary>True while the padlock bumps after its count went down.</summary>
        internal bool IsPadlockTicking(int shelf) => _shelves[shelf].TickElapsed >= 0f;

        internal float GetPadlockScale(int shelf) => _shelves[shelf].Lock.localScale.x;

        internal Vector2 GetPadlockPixelPosition(int shelf) => _shelves[shelf].Lock.position * PixelsPerUnit;

        /// <summary>Where a hidden-strip tile rests, in pixels.</summary>
        internal Vector2 GetHiddenTileHome(int shelf, int slot) => _layout.GetHiddenSlotRect(shelf, slot).center;

        private enum StepKind
        {
            Clear = 0,
            Reveal,
            Fly,
        }

        /// <summary>One queued clear, reveal or flight. Strings are references to the board's ids: copying allocates nothing.</summary>
        private struct AnimStep
        {
            public StepKind Kind;

            public bool IsReveal => Kind == StepKind.Reveal;

            /// <summary>Cleared object id (clear steps).</summary>
            public string Item;

            /// <summary>Clear steps: the burst has been raised.</summary>
            public bool Burst;

            // Fly steps: the landing slot, the start position in pixels and the arc height.
            public int Slot;
            public Vector2 From;
            public float Height;

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
            public readonly SpriteRenderer FlightShadow;
            public readonly Tile[] Front = new Tile[Shelf.SlotCount];
            public readonly Tile[] Hidden = new Tile[Shelf.SlotCount];
            public readonly AnimStep[] Steps = new AnimStep[MaxQueuedSteps];
            public int StepHead;
            public int StepCount;
            public float StepElapsed;

            // Padlock for a locked shelf: hidden on open shelves. Times are -1 when idle.
            public readonly SpriteRenderer Scrim;
            public readonly Transform Lock;
            public readonly SpriteRenderer LockSprite;
            public readonly TextMesh LockLabel;
            public int LockShown;
            public float TickElapsed = -1f;
            public float PopElapsed = -1f;

            public ShelfVisual(Transform parent, int index, Font font)
            {
                var root = new GameObject("Shelf " + index).transform;
                root.SetParent(parent, false);
                Background = CreateSprite(root, "Background", ShelfOrder);
                Background.color = ShelfColor;
                FlightShadow = CreateShadow(root, "Flight Shadow");
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    Front[slot] = new Tile(root, "Front " + slot, font, FrontTileOrder, FrontLabelOrder);
                    Hidden[slot] = new Tile(root, "Hidden " + slot, font, HiddenTileOrder, HiddenLabelOrder);
                }

                Scrim = CreateSprite(root, "Lock Scrim", LockScrimOrder);
                Lock = new GameObject("Padlock").transform;
                Lock.SetParent(root, false);
                var spriteObject = new GameObject("Padlock Sprite");
                spriteObject.transform.SetParent(Lock, false);
                LockSprite = spriteObject.AddComponent<SpriteRenderer>();
                LockSprite.sprite = PlaceholderSprites.Padlock;
                LockSprite.sortingOrder = PadlockOrder;
                LockLabel = CreateLabel(Lock, "Padlock Count", font, PadlockLabelOrder);
                Scrim.gameObject.SetActive(false);
                Lock.gameObject.SetActive(false);
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
