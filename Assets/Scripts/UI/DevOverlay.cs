using Game.Services;
using Game.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Development-build tools over the board: the level select and an fps readout in the bottom reserve. A "Levels"
    /// button in the top HUD reserve opens a card with one cell per level in the <see cref="LevelBootstrap"/> list
    /// (every level in <see cref="LevelList"/>, so it grows as level files are added), each showing its number, saved
    /// stars and state (<see cref="DevLevelCell"/>), plus Reset progress and Close. Tapping a cell loads that level,
    /// locked or not. While the card is open the board ignores the pointer (<see cref="BoardView.InputBlocked"/>).
    ///
    /// Release builds never get it: no scene references it, and <see cref="TryInstall"/> adds it at runtime only when
    /// <see cref="DevBuild.IsLevelSelectEnabled"/> is true for <c>Debug.isDebugBuild</c>. Should a release player ever
    /// find one in a scene, it destroys itself on Awake. Built from code with uGUI; sizes in dp from
    /// <see cref="DevLevelSelectLayout"/>.
    /// </summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        internal const string ToggleLabel = "Levels";
        internal const string Title = "Levels (dev build)";
        internal const string ResetLabel = "Reset progress";
        internal const string CloseLabel = "Close";

        private const int CanvasSortingOrder = 110; // above the result panels, so you can jump levels from any panel
        private const int SlowFps = 55;

        private static readonly Color32 ToggleColor = new Color32(0x00, 0x83, 0x8F, 0xD0);  // peacock teal
        private static readonly Color32 BackdropColor = new Color32(0x1B, 0x0F, 0x0C, 0xC8);
        private static readonly Color32 CardColor = new Color32(0xFF, 0xF8, 0xE1, 0xFF);
        private static readonly Color32 TitleColor = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 CurrentColor = new Color32(0xFF, 0xB3, 0x00, 0xFF);  // marigold
        private static readonly Color32 ClearedColor = new Color32(0x00, 0x83, 0x8F, 0xFF);  // peacock teal
        private static readonly Color32 OpenColor = new Color32(0x4D, 0xB6, 0xAC, 0xFF);     // light teal
        private static readonly Color32 LockedColor = new Color32(0xA1, 0x88, 0x7F, 0xFF);   // shelf brown-grey
        private static readonly Color32 DarkText = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 LightText = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color32 StarOn = new Color32(0xFF, 0xD5, 0x4F, 0xFF);
        private static readonly Color32 StarOnCurrent = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 ResetColor = new Color32(0xC6, 0x28, 0x28, 0xFF);    // kumkum red
        private static readonly Color32 FpsGood = new Color32(0x9C, 0xFF, 0x9C, 0xFF);
        private static readonly Color32 FpsSlow = new Color32(0xFF, 0x6E, 0x6E, 0xFF);

        private LevelBootstrap _levels;
        private Camera _camera;
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _safeArea;
        private Button _toggle;
        private GameObject _panel;
        private RectTransform _card;
        private RectTransform _title;
        private Button[] _cells;
        private Text[] _cellLabels;
        private Image[][] _cellStars;
        private DevLevelCellState[] _cellStates;
        private Button _reset;
        private Button _close;
        private Text _fps;
        private Text _worst;
        private FpsSampler _sampler = new FpsSampler(FpsSampler.DefaultWindowSeconds);
        private IProgressService _subscribedProgress;
        private bool _built;
        private bool _trackScreen = true;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        // ---- Install ----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForScenes() => TryInstall(Debug.isDebugBuild);

        /// <summary>
        /// The only way the overlay is made: when <see cref="DevBuild.IsLevelSelectEnabled"/> allows it for
        /// <paramref name="isDevBuild"/>, follows scene loads and adds the overlay to the current scene (returned, or null
        /// when the scene has no <see cref="LevelBootstrap"/>). In a release build it does nothing and returns null.
        /// </summary>
        internal static DevOverlay TryInstall(bool isDevBuild)
        {
            if (!DevBuild.IsLevelSelectEnabled(isDevBuild)) return null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            return AttachToScene();
        }

        internal static void Uninstall() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToScene();

        /// <summary>
        /// Adds the overlay, on its own object, for the scene's <see cref="LevelBootstrap"/> if there is one and no
        /// overlay yet. Its own object, because <see cref="BoardView"/> rebuilds the children of its object on Bind.
        /// </summary>
        private static DevOverlay AttachToScene()
        {
            var levels = FindAnyObjectByType<LevelBootstrap>();
            if (levels == null) return null;
            var existing = FindAnyObjectByType<DevOverlay>();
            if (existing != null) return existing;
            var viewCamera = Camera.main;
            if (viewCamera == null) return null;
            var overlay = new GameObject("Dev Overlay").AddComponent<DevOverlay>();
            overlay.Configure(levels, viewCamera);
            return overlay;
        }

        /// <summary>Wires the level list and camera from code (installer, tests). Builds on Start.</summary>
        public void Configure(LevelBootstrap levels, Camera viewCamera)
        {
            Unsubscribe();
            _levels = levels;
            _camera = viewCamera;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            if (!DevBuild.IsLevelSelectEnabled(Debug.isDebugBuild)) Destroy(gameObject);
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            SetBoardBlocked(false);
        }

        private void Start()
        {
            if (_levels == null || _camera == null) return;
            Build();
        }

        private void Update()
        {
            if (!_built) return;

            if (_sampler.AddFrame(Time.unscaledDeltaTime))
            {
                _fps.text = DevFpsLabels.Fps(_sampler.Fps);
                _worst.text = DevFpsLabels.WorstMs(_sampler.WorstFrameMs);
                _fps.color = _sampler.Fps < SlowFps ? FpsSlow : FpsGood;
            }

            if (!_trackScreen) return;
            var safeArea = Screen.safeArea;
            if (safeArea == _lastSafeArea && Screen.width == _lastWidth && Screen.height == _lastHeight) return;
            LayoutForScreen();
        }

        // ---- Build and layout ----

        /// <summary>Builds the canvas, the toggle, the (hidden) level card and the fps readout, once.</summary>
        public void Build()
        {
            if (_built || _levels == null || _camera == null) return;

            var canvasObject = new GameObject("Dev Overlay Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _camera;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = CanvasSortingOrder;
            _scaler = canvasObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // The backdrop covers the whole screen (not just the safe area) and swallows the taps that miss the card.
            var backdrop = UiParts.Image(canvasObject.transform, "Dev Level Select", BackdropColor, null);
            UiParts.Stretch(backdrop.rectTransform);
            _panel = backdrop.gameObject;

            _safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            _safeArea.SetParent(canvasObject.transform, false);
            _panel.transform.SetAsFirstSibling();

            _toggle = CreateButton(_safeArea, "Dev Levels Button", ToggleColor, font, ToggleLabel,
                DevLevelSelectLayout.ActionFontDp, LightText, Open);
            var toggleRect = (RectTransform)_toggle.transform;
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0f, 1f);

            // The card lives under the backdrop object so one SetActive shows or hides the whole panel, but is laid out
            // against the safe area: the backdrop is full screen, so the card is re-anchored in ApplyLayout.
            var cardImage = UiParts.Image(_panel.transform, "Card", CardColor, PlaceholderSprites.RoundedSquare);
            _card = cardImage.rectTransform;
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            var title = UiParts.Text(_card, "Title", font, Title, DevLevelSelectLayout.TitleFontDp, TitleColor, TextAnchor.MiddleCenter);
            _title = title.rectTransform;
            _title.anchorMin = _title.anchorMax = new Vector2(0.5f, 0.5f);

            int count = _levels.LevelCount;
            _cells = new Button[count];
            _cellLabels = new Text[count];
            _cellStars = new Image[count][];
            _cellStates = new DevLevelCellState[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var cell = CreateButton(_card, "Dev Level " + (i + 1), LockedColor, font, (i + 1).ToString(),
                    DevLevelSelectLayout.LabelFontDp, LightText, () => Choose(index));
                var cellRect = (RectTransform)cell.transform;
                cellRect.anchorMin = cellRect.anchorMax = new Vector2(0.5f, 0.5f);
                _cells[i] = cell;
                _cellLabels[i] = cell.GetComponentInChildren<Text>();
                var labelRect = _cellLabels[i].rectTransform;
                labelRect.offsetMin = new Vector2(0f, DevLevelSelectLayout.StarDp * 0.75f); // number sits above the stars

                var stars = new Image[ProgressRules.MaxStars];
                for (int s = 0; s < stars.Length; s++)
                {
                    var star = UiParts.Image(cellRect, "Star " + s, StarOn, PlaceholderSprites.Star, sliced: false);
                    star.raycastTarget = false;
                    var rect = star.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.sizeDelta = new Vector2(DevLevelSelectLayout.StarDp, DevLevelSelectLayout.StarDp);
                    rect.anchoredPosition = new Vector2((s - 1) * (DevLevelSelectLayout.StarDp + 1f), DevLevelSelectLayout.StarDp * 0.5f + 4f);
                    stars[s] = star;
                }
                _cellStars[i] = stars;
            }

            _reset = CreateButton(_card, "Dev Reset Progress", ResetColor, font, ResetLabel, DevLevelSelectLayout.ActionFontDp,
                LightText, ResetProgress);
            _close = CreateButton(_card, "Dev Close", ClearedColor, font, CloseLabel, DevLevelSelectLayout.ActionFontDp,
                LightText, Close);
            foreach (var button in new[] { _reset, _close })
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            }

            _fps = UiParts.Text(_safeArea, "Dev Fps", font, DevFpsLabels.Fps(0), DevLevelSelectLayout.FpsFontDp, FpsGood,
                TextAnchor.MiddleLeft);
            _worst = UiParts.Text(_safeArea, "Dev Worst Frame", font, DevFpsLabels.WorstMs(0), DevLevelSelectLayout.FpsFontDp,
                FpsGood, TextAnchor.MiddleLeft);
            foreach (var label in new[] { _fps, _worst })
            {
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0.5f);
            }

            _built = true;
            _panel.SetActive(false);
            LayoutForScreen();
            Refresh();
        }

        /// <summary>Lays out from the real screen (safe area, density) and keeps following screen changes.</summary>
        public void LayoutForScreen()
        {
            _trackScreen = true;
            _lastSafeArea = Screen.safeArea;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            ApplyLayout(_lastSafeArea, ScreenDensity.Current);
        }

        /// <summary>Lays out for a given safe area and density, and stops following the screen (screenshots, tests).</summary>
        public void Layout(Rect safeAreaPx, float density)
        {
            _trackScreen = false;
            ApplyLayout(safeAreaPx, density);
        }

        private void ApplyLayout(Rect safeAreaPx, float density)
        {
            if (!_built) return;
            density = Mathf.Max(0.5f, density);
            _scaler.scaleFactor = density;

            ResultPanelLayout.SafeAreaAnchors(safeAreaPx, _camera.pixelWidth, _camera.pixelHeight, out var min, out var max);
            _safeArea.anchorMin = min;
            _safeArea.anchorMax = max;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;

            var settings = BoardLayoutSettings.Default;
            var safeDp = new Vector2(safeAreaPx.width / density, safeAreaPx.height / density);
            var m = DevLevelSelectLayout.Compute(safeDp, settings.TopReserveFraction, settings.BottomReserveFraction,
                _cells.Length);

            var toggle = (RectTransform)_toggle.transform;
            toggle.sizeDelta = m.Toggle;
            toggle.anchoredPosition = m.ToggleCentre;

            // Centre of the safe area, as an anchor inside the full-screen backdrop.
            var centre = (min + max) * 0.5f;
            _card.anchorMin = _card.anchorMax = centre;
            _card.sizeDelta = m.Card;
            _card.anchoredPosition = Vector2.zero;
            _title.sizeDelta = new Vector2(m.Card.x - 2f * m.Padding, DevLevelSelectLayout.TitleHeightDp);
            _title.anchoredPosition = m.TitleCentre;
            for (int i = 0; i < _cells.Length; i++)
            {
                var rect = (RectTransform)_cells[i].transform;
                rect.sizeDelta = new Vector2(m.Cell, m.Cell);
                rect.anchoredPosition = m.CellCentre(i);
            }
            var reset = (RectTransform)_reset.transform;
            reset.sizeDelta = m.Action;
            reset.anchoredPosition = m.ActionCentre(0);
            var close = (RectTransform)_close.transform;
            close.sizeDelta = m.Action;
            close.anchoredPosition = m.ActionCentre(1);

            float y = m.FpsCentreFromBottom;
            float x = DevLevelSelectLayout.SideMarginDp;
            _fps.rectTransform.sizeDelta = new Vector2(72f, DevLevelSelectLayout.FpsHeightDp);
            _fps.rectTransform.anchoredPosition = new Vector2(x, y);
            _worst.rectTransform.sizeDelta = new Vector2(110f, DevLevelSelectLayout.FpsHeightDp);
            _worst.rectTransform.anchoredPosition = new Vector2(x + 76f, y);
        }

        // ---- Open, choose, reset ----

        public bool IsOpen => _built && _panel.activeSelf;

        /// <summary>Shows the level card with up-to-date progress and stops board input under it.</summary>
        public void Open()
        {
            if (!_built) return;
            Refresh();
            _panel.SetActive(true);
            SetBoardBlocked(true);
        }

        public void Close()
        {
            if (!_built) return;
            _panel.SetActive(false);
            SetBoardBlocked(false);
        }

        private void Choose(int index)
        {
            Close();
            _levels.LoadLevel(index);
        }

        private void ResetProgress()
        {
            var progress = _levels.Progress;
            if (progress != null) progress.Reset();
            Refresh();
        }

        private void SetBoardBlocked(bool blocked)
        {
            if (_levels != null && _levels.View != null) _levels.View.InputBlocked = blocked;
        }

        /// <summary>Recolours every cell from the save and the level on the board.</summary>
        private void Refresh()
        {
            if (!_built) return;
            var progress = _levels.Progress;
            var ids = _levels.LevelIds;
            for (int i = 0; i < _cells.Length; i++)
            {
                int stars = progress == null || i >= ids.Length ? 0 : progress.GetStars(ids[i]);
                bool unlocked = progress == null || ProgressRules.IsUnlocked(ids, progress, i);
                var state = DevLevelCell.StateFor(i == _levels.CurrentIndex, stars, unlocked);
                _cellStates[i] = state;

                Color32 color;
                switch (state)
                {
                    case DevLevelCellState.Current: color = CurrentColor; break;
                    case DevLevelCellState.Cleared: color = ClearedColor; break;
                    case DevLevelCellState.Open: color = OpenColor; break;
                    default: color = LockedColor; break;
                }
                ((Image)_cells[i].targetGraphic).color = color;
                _cellLabels[i].color = state == DevLevelCellState.Current ? DarkText : LightText;
                for (int s = 0; s < _cellStars[i].Length; s++)
                {
                    var star = _cellStars[i][s];
                    star.gameObject.SetActive(s < stars);
                    star.color = state == DevLevelCellState.Current ? StarOnCurrent : StarOn;
                }
            }
        }

        // ---- Events ----

        private void Subscribe()
        {
            Unsubscribe();
            if (_levels == null) return;
            _levels.LevelLoaded += OnLevelLoaded;
            _subscribedProgress = _levels.Progress;
            if (_subscribedProgress != null) _subscribedProgress.Changed += Refresh;
        }

        private void Unsubscribe()
        {
            if (_levels != null) _levels.LevelLoaded -= OnLevelLoaded;
            if (_subscribedProgress != null) _subscribedProgress.Changed -= Refresh;
            _subscribedProgress = null;
        }

        private void OnLevelLoaded(int index) => Refresh();

        // ---- Test hooks (Game.Tests.PlayMode) ----

        internal Button ToggleButton => _toggle;
        internal Button[] LevelButtons => _cells;
        internal Button ResetButton => _reset;
        internal Button CloseButton => _close;
        internal DevLevelCellState CellState(int index) => _cellStates[index];
        internal int VisibleStars(int index)
        {
            int n = 0;
            foreach (var star in _cellStars[index]) if (star.gameObject.activeSelf) n++;
            return n;
        }
        internal RectTransform Card => _card;
        internal Canvas Canvas => _canvas;

        // ---- Building blocks ----

        private static Button CreateButton(Transform parent, string name, Color32 color, Font font, string label,
            int fontSize, Color32 textColor, UnityEngine.Events.UnityAction onClick)
        {
            var image = UiParts.Image(parent, name, color, PlaceholderSprites.RoundedSquare);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var text = UiParts.Text(image.rectTransform, "Label", font, label, fontSize, textColor, TextAnchor.MiddleCenter);
            UiParts.Stretch(text.rectTransform);
            return button;
        }
    }
}
