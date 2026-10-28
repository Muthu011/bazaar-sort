using System;
using Game.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The placeholder Win panel (Next, Retry) and Stuck panel (Retry), built from code with uGUI on a screen-space
    /// camera canvas over the board. The canvas works in dp (constant pixel size, scale factor = screen density), so
    /// <see cref="ResultPanelLayout"/> sizes come out the same on every phone, and the cards sit inside the safe area.
    ///
    /// Thin: shows a panel when <see cref="BoardView.OutcomeReady"/> fires (after the clear animations), hides on
    /// <see cref="LevelBootstrap.LevelLoaded"/>, and turns button taps into <see cref="LevelBootstrap.Next"/> and
    /// <see cref="LevelBootstrap.Retry"/>. Text uses Unity's built-in legacy font, like the board labels.
    /// </summary>
    public sealed class ResultPanels : MonoBehaviour
    {
        internal const string WinTitle = "Cleared!";
        internal const string StuckTitle = "No moves left";
        internal const string NextLabel = "Next";
        internal const string RetryLabel = "Retry";

        private const int CanvasSortingOrder = 100;

        private static readonly Color32 BackdropColor = new Color32(0x1B, 0x0F, 0x0C, 0xB0);
        private static readonly Color32 CardColor = new Color32(0xFF, 0xF8, 0xE1, 0xFF);
        private static readonly Color32 TitleColor = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 PrimaryColor = new Color32(0xFF, 0xB3, 0x00, 0xFF);   // marigold
        private static readonly Color32 PrimaryText = new Color32(0x3E, 0x27, 0x23, 0xFF);
        private static readonly Color32 SecondaryColor = new Color32(0x00, 0x83, 0x8F, 0xFF); // peacock teal
        private static readonly Color32 SecondaryText = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        [SerializeField] private BoardView _view;
        [SerializeField] private LevelBootstrap _levels;
        [SerializeField] private Camera _camera;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _safeArea;
        private GameObject _backdrop;
        private Card _win;
        private Card _stuck;
        private bool _built;
        private BoardView _subscribedView;
        private LevelBootstrap _subscribedLevels;
        private bool _trackScreen = true;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        /// <summary>The Next button on the Win panel was tapped (after <see cref="LevelBootstrap.Next"/> ran, if wired).</summary>
        public event Action NextClicked;

        /// <summary>A Retry button was tapped (after <see cref="LevelBootstrap.Retry"/> ran, if wired).</summary>
        public event Action RetryClicked;

        /// <summary>Wires the view, level list and camera from code (scene builder, tests).</summary>
        public void Configure(BoardView view, LevelBootstrap levels, Camera viewCamera)
        {
            Unsubscribe();
            _view = view;
            _levels = levels;
            _camera = viewCamera;
            if (isActiveAndEnabled) Subscribe();
        }

        public bool IsWinVisible => _built && _win.Root.activeSelf;
        public bool IsStuckVisible => _built && _stuck.Root.activeSelf;

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            if (_camera == null)
            {
                Debug.LogError("ResultPanels: camera must be set.", this);
                return;
            }
            Build(_camera, true);
        }

        private void Update()
        {
            if (!_built || !_trackScreen) return;
            var safeArea = Screen.safeArea;
            if (safeArea == _lastSafeArea && Screen.width == _lastWidth && Screen.height == _lastHeight) return;
            LayoutForScreen();
        }

        /// <summary>
        /// Builds the canvas and both cards (hidden) once, drawn by <paramref name="viewCamera"/>. With
        /// <paramref name="ensureEventSystem"/>, also makes an EventSystem with the Input System UI module if the scene
        /// has none, so the buttons take taps.
        /// </summary>
        public void Build(Camera viewCamera, bool ensureEventSystem)
        {
            if (_built) return;
            _camera = viewCamera;

            var canvasObject = new GameObject("Result Panels Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = viewCamera;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = CanvasSortingOrder;
            _scaler = canvasObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _backdrop = CreateImage(canvasObject.transform, "Backdrop", BackdropColor, false).gameObject;
            Stretch((RectTransform)_backdrop.transform);

            _safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            _safeArea.SetParent(canvasObject.transform, false);

            _win = new Card(_safeArea, "Win Panel", WinTitle, font, 2);
            _win.SetButton(0, NextLabel, PrimaryColor, PrimaryText, OnNext);
            _win.SetButton(1, RetryLabel, SecondaryColor, SecondaryText, OnRetry);
            _stuck = new Card(_safeArea, "Stuck Panel", StuckTitle, font, 1);
            _stuck.SetButton(0, RetryLabel, PrimaryColor, PrimaryText, OnRetry);

            if (ensureEventSystem && FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(transform, false);
            }

            _built = true;
            Hide();
            LayoutForScreen();
        }

        /// <summary>Lays the panels out from the real screen: safe area and density. Keeps following screen changes.</summary>
        public void LayoutForScreen()
        {
            _trackScreen = true;
            _lastSafeArea = Screen.safeArea;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            ApplyLayout(_lastSafeArea, ScreenDensity.Current);
        }

        /// <summary>
        /// Lays the panels out for <paramref name="safeAreaPx"/> (pixels of the camera's target) at
        /// <paramref name="density"/> pixels per dp, and stops following the real screen (screenshots, tests).
        /// </summary>
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

            float width = _camera.pixelWidth;
            float height = _camera.pixelHeight;
            ResultPanelLayout.SafeAreaAnchors(safeAreaPx, width, height, out var min, out var max);
            _safeArea.anchorMin = min;
            _safeArea.anchorMax = max;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;

            var safeDp = new Vector2(safeAreaPx.width / density, safeAreaPx.height / density);
            _win.Apply(ResultPanelLayout.Compute(safeDp, 2));
            _stuck.Apply(ResultPanelLayout.Compute(safeDp, 1));
        }

        /// <summary>Shows the panel for <paramref name="outcome"/> (None hides both).</summary>
        public void Show(BoardOutcome outcome)
        {
            if (!_built) return;
            _win.Root.SetActive(outcome == BoardOutcome.Won);
            _stuck.Root.SetActive(outcome == BoardOutcome.Stuck);
            _backdrop.SetActive(outcome != BoardOutcome.None);
        }

        public void Hide() => Show(BoardOutcome.None);

        private void Subscribe()
        {
            Unsubscribe();
            _subscribedView = _view;
            _subscribedLevels = _levels;
            if (_subscribedView != null) _subscribedView.OutcomeReady += Show;
            if (_subscribedLevels != null) _subscribedLevels.LevelLoaded += OnLevelLoaded;
        }

        private void Unsubscribe()
        {
            if (_subscribedView != null) _subscribedView.OutcomeReady -= Show;
            if (_subscribedLevels != null) _subscribedLevels.LevelLoaded -= OnLevelLoaded;
            _subscribedView = null;
            _subscribedLevels = null;
        }

        private void OnLevelLoaded(int index) => Hide();

        private void OnNext()
        {
            Hide();
            if (_levels != null) _levels.Next();
            NextClicked?.Invoke();
        }

        private void OnRetry()
        {
            Hide();
            if (_levels != null) _levels.Retry();
            RetryClicked?.Invoke();
        }

        // ---- Test hooks (Game.Tests.PlayMode) ----

        internal Button WinNextButton => _win.Buttons[0];
        internal Button WinRetryButton => _win.Buttons[1];
        internal Button StuckRetryButton => _stuck.Buttons[0];
        internal RectTransform WinCard => _win.Rect;
        internal RectTransform StuckCard => _stuck.Rect;
        internal Canvas Canvas => _canvas;

        // ---- Building blocks ----

        private static Image CreateImage(Transform parent, string name, Color32 color, bool rounded)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = PlaceholderSprites.RoundedSquare;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1.5f;
            }
            return image;
        }

        private static Text CreateText(Transform parent, string name, Font font, string text, int size, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(label.rectTransform);
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>One card: rounded background, title, and stacked buttons.</summary>
        private sealed class Card
        {
            public readonly GameObject Root;
            public readonly RectTransform Rect;
            public readonly Button[] Buttons;
            private readonly RectTransform _title;
            private readonly Font _font;

            public Card(Transform parent, string name, string title, Font font, int buttonCount)
            {
                _font = font;
                var background = CreateImage(parent, name, CardColor, true);
                Root = background.gameObject;
                Rect = background.rectTransform;
                Rect.anchorMin = Rect.anchorMax = new Vector2(0.5f, 0.5f);
                Rect.pivot = new Vector2(0.5f, 0.5f);

                var titleText = CreateText(Rect, "Title", font, title, ResultPanelLayout.TitleFontDp, TitleColor);
                _title = titleText.rectTransform;
                _title.anchorMin = _title.anchorMax = new Vector2(0.5f, 0.5f);

                Buttons = new Button[buttonCount];
                for (int i = 0; i < buttonCount; i++)
                {
                    var image = CreateImage(Rect, "Button " + i, PrimaryColor, true);
                    var rect = image.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    var button = image.gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    Buttons[i] = button;
                }
            }

            public void SetButton(int index, string label, Color32 color, Color32 textColor, UnityEngine.Events.UnityAction onClick)
            {
                var button = Buttons[index];
                button.name = label + " Button";
                ((Image)button.targetGraphic).color = color;
                CreateText(button.transform, "Label", _font, label, ResultPanelLayout.ButtonFontDp, textColor);
                button.onClick.AddListener(onClick);
            }

            public void Apply(ResultPanelMetrics metrics)
            {
                Rect.sizeDelta = metrics.Card;
                Rect.anchoredPosition = Vector2.zero;
                _title.sizeDelta = new Vector2(metrics.Card.x - 2f * metrics.Padding, metrics.TitleHeight);
                _title.anchoredPosition = metrics.TitleCentre;
                for (int i = 0; i < Buttons.Length; i++)
                {
                    var rect = (RectTransform)Buttons[i].transform;
                    rect.sizeDelta = metrics.Button;
                    rect.anchoredPosition = metrics.ButtonCentre(i);
                }
            }
        }
    }
}
