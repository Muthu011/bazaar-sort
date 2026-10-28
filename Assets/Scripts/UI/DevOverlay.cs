using Game.View;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Development-build tools over the board: a row of level buttons (L1..Ln, one per level in the
    /// <see cref="LevelBootstrap"/> list) in the top HUD reserve, and an fps readout in the bottom reserve. No scene
    /// references it: <see cref="InstallForScenes"/> adds it at runtime only when
    /// <see cref="DevBuild.IsLevelSelectEnabled"/> is true for <c>Debug.isDebugBuild</c>, so a release player never
    /// creates it. Built from code with uGUI; sizes in dp from <see cref="DevLevelSelectLayout"/>.
    /// </summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        private const int CanvasSortingOrder = 110; // above the result panels, so you can jump levels from any panel
        private const int SlowFps = 55;

        private static readonly Color32 ButtonColor = new Color32(0x00, 0x83, 0x8F, 0xD0);  // peacock teal
        private static readonly Color32 CurrentColor = new Color32(0xFF, 0xB3, 0x00, 0xFF); // marigold
        private static readonly Color32 ButtonText = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color32 CurrentText = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 FpsGood = new Color32(0x9C, 0xFF, 0x9C, 0xFF);
        private static readonly Color32 FpsSlow = new Color32(0xFF, 0x6E, 0x6E, 0xFF);

        private LevelBootstrap _levels;
        private Camera _camera;
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _safeArea;
        private Button[] _buttons;
        private Text[] _buttonLabels;
        private Text _fps;
        private Text _worst;
        private FpsSampler _sampler = new FpsSampler(FpsSampler.DefaultWindowSeconds);
        private bool _built;
        private bool _trackScreen = true;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        // ---- Install ----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForScenes()
        {
            if (!DevBuild.IsLevelSelectEnabled(Debug.isDebugBuild)) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToScene();

        /// <summary>
        /// Adds the overlay, on its own object, for the scene's <see cref="LevelBootstrap"/> if there is one and no
        /// overlay yet. Its own object, because <see cref="BoardView"/> rebuilds the children of its object on Bind.
        /// </summary>
        private static void AttachToScene()
        {
            var levels = FindAnyObjectByType<LevelBootstrap>();
            if (levels == null || FindAnyObjectByType<DevOverlay>() != null) return;
            var viewCamera = Camera.main;
            if (viewCamera == null) return;
            new GameObject("Dev Overlay").AddComponent<DevOverlay>().Configure(levels, viewCamera);
        }

        /// <summary>Wires the level list and camera from code (installer, tests). Builds on Start.</summary>
        public void Configure(LevelBootstrap levels, Camera viewCamera)
        {
            if (_levels != null) _levels.LevelLoaded -= OnLevelLoaded;
            _levels = levels;
            _camera = viewCamera;
            if (_levels != null && isActiveAndEnabled) _levels.LevelLoaded += OnLevelLoaded;
        }

        private void OnEnable()
        {
            if (_levels != null)
            {
                _levels.LevelLoaded -= OnLevelLoaded;
                _levels.LevelLoaded += OnLevelLoaded;
            }
        }

        private void OnDisable()
        {
            if (_levels != null) _levels.LevelLoaded -= OnLevelLoaded;
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

        /// <summary>Builds the canvas, one button per level and the fps readout, once.</summary>
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

            _safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            _safeArea.SetParent(canvasObject.transform, false);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int count = _levels.LevelCount;
            _buttons = new Button[count];
            _buttonLabels = new Text[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var image = CreateImage(_safeArea, "Dev Level " + (i + 1), ButtonColor);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => _levels.LoadLevel(index));
                _buttons[i] = button;
                _buttonLabels[i] = CreateText(rect, "Label", font, "L" + (i + 1), DevLevelSelectLayout.LabelFontDp,
                    TextAnchor.MiddleCenter, ButtonText);
                Stretch(_buttonLabels[i].rectTransform);
            }

            _fps = CreateText(_safeArea, "Dev Fps", font, DevFpsLabels.Fps(0), DevLevelSelectLayout.FpsFontDp,
                TextAnchor.MiddleLeft, FpsGood);
            _worst = CreateText(_safeArea, "Dev Worst Frame", font, DevFpsLabels.WorstMs(0), DevLevelSelectLayout.FpsFontDp,
                TextAnchor.MiddleLeft, FpsGood);
            foreach (var label in new[] { _fps, _worst })
            {
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0.5f);
            }

            _built = true;
            LayoutForScreen();
            Highlight(_levels.CurrentIndex);
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
            var metrics = DevLevelSelectLayout.Compute(safeDp, settings.TopReserveFraction, settings.BottomReserveFraction,
                _buttons.Length);
            for (int i = 0; i < _buttons.Length; i++)
            {
                var rect = (RectTransform)_buttons[i].transform;
                rect.sizeDelta = metrics.Button;
                rect.anchoredPosition = metrics.ButtonCentre(i);
            }

            float y = metrics.FpsCentreFromBottom;
            float x = DevLevelSelectLayout.SideMarginDp;
            _fps.rectTransform.sizeDelta = new Vector2(72f, DevLevelSelectLayout.FpsHeightDp);
            _fps.rectTransform.anchoredPosition = new Vector2(x, y);
            _worst.rectTransform.sizeDelta = new Vector2(110f, DevLevelSelectLayout.FpsHeightDp);
            _worst.rectTransform.anchoredPosition = new Vector2(x + 76f, y);
        }

        private void OnLevelLoaded(int index) => Highlight(index);

        private void Highlight(int current)
        {
            if (!_built) return;
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool isCurrent = i == current;
                ((Image)_buttons[i].targetGraphic).color = isCurrent ? CurrentColor : ButtonColor;
                _buttonLabels[i].color = isCurrent ? CurrentText : ButtonText;
            }
        }

        // ---- Test hooks (Game.Tests.PlayMode) ----

        internal Button[] LevelButtons => _buttons;
        internal Canvas Canvas => _canvas;

        // ---- Building blocks ----

        private static Image CreateImage(Transform parent, string name, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = PlaceholderSprites.RoundedSquare;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.5f;
            return image;
        }

        private static Text CreateText(Transform parent, string name, Font font, string text, int size, TextAnchor anchor,
            Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = anchor;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
