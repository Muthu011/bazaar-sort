using Game.Core;
using Game.Levels;
using Game.Services;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Runs the first-play tutorials (issue #57) from the level JSON <c>tutorial</c> field. Thin: the steps are
    /// <see cref="TutorialGuide"/> (Core), the timing is <see cref="TutorialCueMotion"/>; this component only wires them
    /// to the level, the input and the save, and draws the cue.
    /// <list type="bullet">
    /// <item>match3 (L1): a hand pointer from the item to move to its target slot and a "Match 3!" pill above the
    /// target shelf. <see cref="BoardInput"/> is gated (<see cref="IMoveGate"/>) so only that move can be made until
    /// the first triple.</item>
    /// <item>layers (L2): an arrow bobbing over the hidden row of the first shelf that has one. No text.</item>
    /// <item>locked (L11): one short line on the locked shelf, "Make 1 match to open".</item>
    /// </list>
    /// When a guide completes, its key is saved in <see cref="LevelBootstrap.Progress"/>; from then on that tutorial is
    /// skipped, replays included. Without progress (tests wiring levels from code) the tutorial runs every time and
    /// nothing is saved. Placeholder sprites (<see cref="PlaceholderSprites.Hand"/>, <see cref="PlaceholderSprites.ArrowDown"/>)
    /// stand in until approved art exists. The cue objects are made once in <see cref="Awake"/>; Update only moves and
    /// tints them (no allocation).
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour, IMoveGate
    {
        internal const string Match3Text = "Match 3!";

        private const int PillOrder = 40;
        private const int TextOrder = 41;
        private const int HandOrder = 42;
        private const int FontSize = 64;

        /// <summary>Hand width in tiles.</summary>
        internal const float HandTiles = 0.75f;

        /// <summary>Arrow width in tiles.</summary>
        internal const float ArrowTiles = 0.45f;

        /// <summary>Text size as a fraction of the pill's height (TextMesh character size, as BoardView sizes its labels).</summary>
        private const float TextInPill = 0.8f;

        /// <summary>Pill height in tiles, at most.</summary>
        private const float PillHeightTiles = 0.5f;

        /// <summary>The "Match 3!" pill is at most this much of the top bar's height (BoardView.StatusMaxHeightInReserve).</summary>
        private const float PillMaxHeightInReserve = 0.4f;

        // docs/style-guide.md: Card cream pill, Roasted brown text; Turmeric (the World 1 accent) for the arrow.
        private static readonly Color32 CardCream = new Color32(0xFF, 0xF4, 0xDC, 0xFF);
        private static readonly Color32 RoastedBrown = new Color32(0x3B, 0x24, 0x18, 0xFF);
        private static readonly Color32 Turmeric = new Color32(0xF2, 0xB6, 0x32, 0xFF);

        [SerializeField] private LevelBootstrap _bootstrap;
        [SerializeField] private BoardInput _input;

        private TutorialGuide _guide;
        private string _key;
        private Board _gatedBoard;
        private float _elapsed;
        private int _shownLockCount = -1;

        private SpriteRenderer _hand;
        private SpriteRenderer _arrow;
        private SpriteRenderer _pill;
        private TextMesh _text;
        private MeshRenderer _textRenderer;

        /// <summary>The guide for the current level, or null (no tutorial, or skipped because it was done before).</summary>
        public TutorialGuide Guide => _guide;

        /// <summary>Wires the director from code (scene builder, tests).</summary>
        public void Configure(LevelBootstrap bootstrap, BoardInput input)
        {
            if (isActiveAndEnabled) Unwatch();
            _bootstrap = bootstrap;
            _input = input;
            if (isActiveAndEnabled) Watch();
        }

        /// <summary>The short line shown on a locked shelf with <paramref name="clearsLeft"/> clears to go.</summary>
        public static string LockedLine(int clearsLeft) =>
            clearsLeft == 1 ? "Make 1 match to open" : "Make " + clearsLeft + " matches to open";

        // ---- IMoveGate ----

        public bool AllowsPick(int shelf, int slot) => _guide == null || _guide.AllowsPick(shelf, slot);

        public bool AllowsMove(int fromShelf, int fromSlot, int toShelf) =>
            _guide == null || _guide.AllowsMove(fromShelf, fromSlot, toShelf);

        // ---- Lifetime ----

        private void Awake()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _pill = CreateSprite("Tutorial Pill", PlaceholderSprites.RoundedSquare, PillOrder);
            _pill.drawMode = SpriteDrawMode.Sliced;
            _pill.color = CardCream;
            _hand = CreateSprite("Tutorial Hand", PlaceholderSprites.Hand, HandOrder);
            _hand.color = CardCream;
            _arrow = CreateSprite("Tutorial Arrow", PlaceholderSprites.ArrowDown, HandOrder);
            _arrow.color = Turmeric;

            var textObject = new GameObject("Tutorial Text");
            textObject.transform.SetParent(transform, false);
            _text = textObject.AddComponent<TextMesh>();
            _text.font = font;
            _text.fontSize = FontSize;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.fontStyle = FontStyle.Bold;
            Color text = RoastedBrown;
            _text.color = QualitySettings.activeColorSpace == ColorSpace.Linear ? text.linear : text;
            _textRenderer = textObject.GetComponent<MeshRenderer>();
            _textRenderer.sharedMaterial = font.material;
            _textRenderer.sortingOrder = TextOrder;

            HideCue();
        }

        private void OnEnable() => Watch();

        private void OnDisable()
        {
            Unwatch();
            EndGuide();
        }

        private void Watch()
        {
            if (_bootstrap == null) return;
            _bootstrap.LevelLoaded += OnLevelLoaded;
            if (_bootstrap.CurrentIndex >= 0) OnLevelLoaded(_bootstrap.CurrentIndex);
        }

        private void Unwatch()
        {
            if (_bootstrap != null) _bootstrap.LevelLoaded -= OnLevelLoaded;
        }

        private void OnLevelLoaded(int index)
        {
            EndGuide();
            var board = _bootstrap.View == null ? null : _bootstrap.View.Board;
            var level = _bootstrap.CurrentLevel;
            if (board == null || level == null) return;

            var kind = TutorialKeys.Parse(level.Tutorial);
            if (kind == TutorialKind.None) return;

            var progress = _bootstrap.Progress;
            if (progress != null && progress.IsTutorialDone(level.Tutorial)) return;

            var guide = new TutorialGuide(board, kind);
            if (!guide.IsActive)
            {
                guide.Dispose();
                return;
            }

            _guide = guide;
            _key = level.Tutorial;
            _elapsed = 0f;
            _shownLockCount = -1;
            _guide.Completed += OnCompleted;
            if (_guide.Restricts && _input != null)
            {
                _gatedBoard = board;
                _input.SetGate(board, this);
            }
        }

        private void OnCompleted()
        {
            var progress = _bootstrap == null ? null : _bootstrap.Progress;
            string key = _key;
            EndGuide();
            if (progress != null && !string.IsNullOrEmpty(key)) progress.MarkTutorialDone(key);
        }

        private void EndGuide()
        {
            if (_guide != null)
            {
                _guide.Completed -= OnCompleted;
                _guide.Dispose();
                _guide = null;
            }
            _key = null;
            if (_gatedBoard != null && _input != null) _input.SetGate(_gatedBoard, null);
            _gatedBoard = null;
            HideCue();
        }

        private void OnDestroy() => EndGuide();

        // ---- Drawing ----

        private void Update() => Advance(Time.deltaTime);

        /// <summary>
        /// Moves the cue on by <paramref name="seconds"/> and redraws it. Called from Update; capture tools call it by
        /// hand with game time frozen so frames are exact.
        /// </summary>
        public void Advance(float seconds)
        {
            if (_guide == null || !_guide.IsActive)
            {
                HideCue();
                return;
            }

            var view = _bootstrap.View;
            var layout = view == null ? null : view.Layout;
            if (layout == null || view.InputLocked)
            {
                HideCue();
                return;
            }

            _elapsed += seconds;
            switch (_guide.Cue)
            {
                case TutorialCue.HandPointer:
                    DrawHand(layout);
                    break;
                case TutorialCue.LayerArrow:
                    DrawArrow(layout);
                    break;
                case TutorialCue.LockedLine:
                    DrawLockedLine(layout);
                    break;
                default:
                    HideCue();
                    break;
            }
        }

        private void DrawHand(BoardLayout layout)
        {
            var move = _guide.Move;
            var target = _guide.Board.GetShelf(move.ToShelf);
            int toSlot = Mathf.Max(0, target.FirstFreeFrontSlot);
            Vector2 from = layout.GetFrontSlotRect(move.FromShelf, move.FromSlot).center;
            Vector2 to = layout.GetFrontSlotRect(move.ToShelf, toSlot).center;

            var pose = TutorialCueMotion.Hand(_elapsed);
            float width = layout.TileSize * HandTiles * pose.Scale;
            Vector2 tip = Vector2.Lerp(from, to, pose.Travel);
            Vector2 centre = tip - PlaceholderSprites.HandTipOffset * width;
            PlaceSprite(_hand, centre, width);
            var color = (Color)CardCream;
            color.a = pose.Alpha;
            _hand.color = color;
            _hand.gameObject.SetActive(true);
            _arrow.gameObject.SetActive(false);

            // "Match 3!" sits where the board's status line goes: the lower part of the top bar, clear of the HUD's
            // level number (BoardView.StatusCentreInReserve). The status line is empty while the level is in play.
            var playfield = layout.Playfield;
            float reserve = Mathf.Max(0f, _bootstrap.View.SafeArea.yMax - playfield.yMax);
            float height = Mathf.Max(layout.TileSize * 0.3f,
                Mathf.Min(layout.TileSize * PillHeightTiles, reserve * PillMaxHeightInReserve));
            ShowPill(new Vector2(playfield.center.x, playfield.yMax + reserve * BoardView.StatusCentreInReserve),
                height * 4f, height);
            if (_text.text != Match3Text) _text.text = Match3Text;
        }

        private void DrawArrow(BoardLayout layout)
        {
            int shelf = _guide.FocusShelf;
            var hidden = layout.GetHiddenSlotRect(shelf, 1);
            float width = layout.TileSize * ArrowTiles;
            float bob = TutorialCueMotion.ArrowBob(_elapsed) * layout.TileSize;
            // Tip just above the hidden row, pointing down at it.
            var centre = new Vector2(hidden.center.x, hidden.yMax + width * 0.5f + bob);
            PlaceSprite(_arrow, centre, width);
            _arrow.gameObject.SetActive(true);
            _hand.gameObject.SetActive(false);
            _pill.gameObject.SetActive(false);
            _text.gameObject.SetActive(false);
        }

        private void DrawLockedLine(BoardLayout layout)
        {
            int shelf = _guide.FocusShelf;
            int left = _guide.Board.GetShelf(shelf).LockClearsLeft;
            if (left != _shownLockCount)
            {
                _shownLockCount = left;
                _text.text = LockedLine(left);
            }

            var rect = layout.GetShelfRect(shelf);
            _hand.gameObject.SetActive(false);
            _arrow.gameObject.SetActive(false);
            ShowPill(rect.center, rect.width * 0.92f, layout.TileSize * PillHeightTiles);
        }

        private void ShowPill(Vector2 centre, float width, float height)
        {
            _pill.transform.position = new Vector3(centre.x / BoardView.PixelsPerUnit, centre.y / BoardView.PixelsPerUnit, 0f);
            _pill.size = new Vector2(width, height) / BoardView.PixelsPerUnit;
            _pill.gameObject.SetActive(true);

            _text.transform.position = _pill.transform.position;
            _text.gameObject.SetActive(true);

            // Text fills TextInPill of the pill's height, shrunk if needed so the line stays inside the pill.
            float size = height * TextInPill / BoardView.PixelsPerUnit * 10f / FontSize;
            _text.characterSize = size;
            float textWidth = _textRenderer.bounds.size.x * BoardView.PixelsPerUnit;
            float room = width * 0.9f;
            if (textWidth > room && textWidth > 0f) _text.characterSize = size * room / textWidth;
        }

        private static void PlaceSprite(SpriteRenderer sprite, Vector2 centrePixels, float widthPixels)
        {
            sprite.transform.position = new Vector3(centrePixels.x / BoardView.PixelsPerUnit, centrePixels.y / BoardView.PixelsPerUnit, 0f);
            float w = widthPixels / BoardView.PixelsPerUnit;
            sprite.transform.localScale = new Vector3(w, w, 1f);
        }

        private void HideCue()
        {
            if (_hand != null) _hand.gameObject.SetActive(false);
            if (_arrow != null) _arrow.gameObject.SetActive(false);
            if (_pill != null) _pill.gameObject.SetActive(false);
            if (_text != null) _text.gameObject.SetActive(false);
        }

        private SpriteRenderer CreateSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        // ---- Test hooks (Game.Tests.PlayMode) ----

        internal bool IsHandVisible => _hand != null && _hand.gameObject.activeSelf;
        internal bool IsArrowVisible => _arrow != null && _arrow.gameObject.activeSelf;
        internal bool IsTextVisible => _text != null && _text.gameObject.activeSelf;
        internal string CueText => IsTextVisible ? _text.text : null;
        internal Vector2 ArrowPixelPosition => _arrow.transform.position * BoardView.PixelsPerUnit;
    }
}
