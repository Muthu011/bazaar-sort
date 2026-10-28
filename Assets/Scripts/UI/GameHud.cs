using DG.Tweening;
using Game.Services;
using Game.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The in-game HUD (issue #21), built from code with uGUI on its own screen-space camera canvas: the clear counter
    /// (coin icon and a number, display only, see <see cref="ClearCounter"/>) and the haptics on/off toggle, right-aligned
    /// in the bottom reserve (<see cref="HudLayout"/>). On every <see cref="BoardView.ClearShown"/> that animated, a coin
    /// flies from the triple to the counter (DOTween, pooled: <see cref="UiFeel.CoinPool"/> coins, each with one tween
    /// built once and restarted) and the number ticks up as it lands. Resets on every level load.
    /// Thin: no rules; the setting lives in <see cref="ISettingsService"/>.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        internal const string HapticsOnLabel = "Haptics\nOn";
        internal const string HapticsOffLabel = "Haptics\nOff";

        /// <summary>Above the board and its particles, below the result panels (100) and the dev overlay (110).</summary>
        private const int CanvasSortingOrder = 50;

        private static readonly Color32 PillColor = new Color32(0x1B, 0x0F, 0x0C, 0xA0);
        private static readonly Color32 CounterText = new Color32(0xFF, 0xF8, 0xE1, 0xFF);
        private static readonly Color32 ToggleOn = new Color32(0x00, 0x83, 0x8F, 0xFF);   // peacock teal
        private static readonly Color32 ToggleOff = new Color32(0x6D, 0x5D, 0x58, 0xFF);  // muted brown-grey
        private static readonly Color32 ToggleText = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        [SerializeField] private BoardView _view;
        [SerializeField] private LevelBootstrap _levels;
        [SerializeField] private Camera _camera;

        private readonly ClearCounter _counter = new ClearCounter();
        private ISettingsService _settings;
        private ISettingsService _subscribedSettings;
        private BoardView _subscribedView;
        private LevelBootstrap _subscribedLevels;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private CanvasScaler _scaler;
        private RectTransform _safeArea;
        private RectTransform _pill;
        private RectTransform _icon;
        private Text _count;
        private Button _toggle;
        private Text _toggleLabel;
        private Coin[] _coins;
        private Tween _punch;
        private bool _built;
        private bool _trackScreen = true;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        /// <summary>Wires the view, level list and camera from code (scene builder, tests).</summary>
        public void Configure(BoardView view, LevelBootstrap levels, Camera viewCamera)
        {
            Unsubscribe();
            _view = view;
            _levels = levels;
            _camera = viewCamera;
            if (isActiveAndEnabled) Subscribe();
        }

        /// <summary>The settings service the toggle changes; <see cref="GameServices.Settings"/> unless set (tests).</summary>
        public ISettingsService Settings
        {
            get => _settings ?? GameServices.Settings;
            set
            {
                _settings = value;
                if (isActiveAndEnabled) Subscribe();
                RefreshToggle();
            }
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            if (_camera == null)
            {
                Debug.LogError("GameHud: camera must be set.", this);
                return;
            }
            Build(_camera);
        }

        private void Update()
        {
            if (!_built || !_trackScreen) return;
            var safeArea = Screen.safeArea;
            if (safeArea == _lastSafeArea && Screen.width == _lastWidth && Screen.height == _lastHeight) return;
            LayoutForScreen();
        }

        /// <summary>Builds the canvas, counter, toggle and coin pool once, drawn by <paramref name="viewCamera"/>.</summary>
        public void Build(Camera viewCamera)
        {
            if (_built) return;
            _camera = viewCamera;
            Tweens.Init();

            var canvasObject = new GameObject("HUD Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = viewCamera;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = CanvasSortingOrder;
            _scaler = canvasObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObject.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)canvasObject.transform;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            _safeArea.SetParent(canvasObject.transform, false);

            // Clear counter: a dark pill with the coin icon on the left and the number on the right.
            var pillImage = UiParts.Image(_safeArea, "Clear Counter", PillColor, PlaceholderSprites.RoundedSquare);
            pillImage.raycastTarget = false;
            _pill = pillImage.rectTransform;
            _pill.anchorMin = _pill.anchorMax = new Vector2(1f, 0f);
            var iconImage = UiParts.Image(_pill, "Coin Icon", Color.white, PlaceholderSprites.Coin, sliced: false);
            iconImage.raycastTarget = false;
            _icon = iconImage.rectTransform;
            _icon.anchorMin = _icon.anchorMax = new Vector2(0f, 0.5f);
            _icon.sizeDelta = new Vector2(28f, 28f);
            _icon.anchoredPosition = new Vector2(8f + 14f, 0f);
            _count = UiParts.Text(_pill, "Count", font, _counter.Text, HudLayout.CounterFontDp, CounterText, TextAnchor.MiddleRight);
            var countRect = _count.rectTransform;
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(40f, 0f);
            countRect.offsetMax = new Vector2(-12f, 0f);

            // Haptics toggle.
            var toggleImage = UiParts.Image(_safeArea, "Haptics Toggle", ToggleOn, PlaceholderSprites.RoundedSquare);
            var toggleRect = toggleImage.rectTransform;
            toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 0f);
            _toggle = toggleImage.gameObject.AddComponent<Button>();
            _toggle.targetGraphic = toggleImage;
            _toggle.onClick.AddListener(ToggleHaptics);
            toggleImage.gameObject.AddComponent<ButtonPress>();
            _toggleLabel = UiParts.Text(toggleRect, "Label", font, HapticsOnLabel, HudLayout.ToggleFontDp, ToggleText, TextAnchor.MiddleCenter);
            UiParts.Stretch(_toggleLabel.rectTransform);

            // Coin pool, drawn over the HUD row.
            _coins = new Coin[UiFeel.CoinPool];
            for (int i = 0; i < _coins.Length; i++) _coins[i] = new Coin(this, _canvasRect, i);

            if (Tweens.IsReady)
            {
                _punch = _pill.DOPunchScale(new Vector3(UiFeel.CounterPunch, UiFeel.CounterPunch, 0f), UiFeel.CounterPunchSeconds, 6, 0.6f)
                    .SetAutoKill(false).SetRecyclable(false).SetLink(gameObject).Pause();
            }

            _built = true;
            RefreshToggle();
            LayoutForScreen();
        }

        /// <summary>Lays the HUD out from the real screen (safe area and density) and keeps following screen changes.</summary>
        public void LayoutForScreen()
        {
            _trackScreen = true;
            _lastSafeArea = Screen.safeArea;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            ApplyLayout(_lastSafeArea, ScreenDensity.Current);
        }

        /// <summary>Lays out for a given safe area and density and stops following the screen (screenshots, tests).</summary>
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

            var safeDp = new Vector2(safeAreaPx.width / density, safeAreaPx.height / density);
            var metrics = HudLayout.Compute(safeDp, BoardLayoutSettings.Default.BottomReserveFraction);
            var toggle = (RectTransform)_toggle.transform;
            toggle.sizeDelta = metrics.Toggle;
            toggle.anchoredPosition = metrics.ToggleCentre;
            _pill.sizeDelta = metrics.Counter;
            _pill.anchoredPosition = metrics.CounterCentre;
        }

        // ---- Events ----

        private void Subscribe()
        {
            Unsubscribe();
            _subscribedView = _view;
            _subscribedLevels = _levels;
            _subscribedSettings = Settings;
            if (_subscribedView != null) _subscribedView.ClearShown += OnClearShown;
            if (_subscribedLevels != null) _subscribedLevels.LevelLoaded += OnLevelLoaded;
            _subscribedSettings.Changed += RefreshToggle;
        }

        private void Unsubscribe()
        {
            if (_subscribedView != null) _subscribedView.ClearShown -= OnClearShown;
            if (_subscribedLevels != null) _subscribedLevels.LevelLoaded -= OnLevelLoaded;
            if (_subscribedSettings != null) _subscribedSettings.Changed -= RefreshToggle;
            _subscribedView = null;
            _subscribedLevels = null;
            _subscribedSettings = null;
        }

        private void OnLevelLoaded(int index) => ResetCounter();

        /// <summary>Back to 0 and every flying coin dropped (level load, Retry, Next, Replay).</summary>
        public void ResetCounter()
        {
            _counter.Reset();
            if (_built)
            {
                foreach (var coin in _coins) coin.Stop();
                if (_punch != null) _punch.Rewind();
                _count.text = _counter.Text;
            }
        }

        private void OnClearShown(ClearBurst clear)
        {
            var coin = clear.Animated && _built ? FreeCoin() : null;
            if (coin == null || !coin.Launch(ScreenToCanvas(clear.Pixel), ScreenToCanvas(IconScreenPoint())))
            {
                _counter.AddClear(false);
                if (_built) _count.text = _counter.Text;
                return;
            }
            _counter.AddClear(true);
        }

        private void OnCoinLanded()
        {
            if (!_counter.CoinLanded()) return;
            _count.text = _counter.Text;
            if (_punch != null) _punch.Restart();
        }

        private void ToggleHaptics()
        {
            var settings = Settings;
            settings.HapticsEnabled = !settings.HapticsEnabled;
            RefreshToggle();
        }

        private void RefreshToggle()
        {
            if (!_built) return;
            bool on = Settings.HapticsEnabled;
            _toggleLabel.text = on ? HapticsOnLabel : HapticsOffLabel;
            ((Image)_toggle.targetGraphic).color = on ? ToggleOn : ToggleOff;
        }

        private Coin FreeCoin()
        {
            for (int i = 0; i < _coins.Length; i++)
            {
                if (!_coins[i].Flying) return _coins[i];
            }
            return null;
        }

        private Vector2 IconScreenPoint() => RectTransformUtility.WorldToScreenPoint(_camera, _icon.position);

        private Vector2 ScreenToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, _camera, out var local);
            return local;
        }

        // ---- Test hooks (Game.Tests.PlayMode) ----

        internal ClearCounter Counter => _counter;
        internal string CountText => _count == null ? null : _count.text;
        internal Button HapticsToggle => _toggle;
        internal string HapticsLabel => _toggleLabel == null ? null : _toggleLabel.text;
        internal RectTransform CounterRect => _pill;
        internal Canvas Canvas => _canvas;

        internal int CoinsInFlight
        {
            get
            {
                int n = 0;
                if (_coins == null) return 0;
                foreach (var coin in _coins) if (coin.Flying) n++;
                return n;
            }
        }

        /// <summary>Position of the first flying coin in canvas units, or null when none flies.</summary>
        internal Vector2? FirstCoinPosition
        {
            get
            {
                if (_coins == null) return null;
                foreach (var coin in _coins) if (coin.Flying) return coin.Rect.localPosition;
                return null;
            }
        }

        /// <summary>Lands every flying coin at once (tests, screenshots).</summary>
        internal void CompleteCoins()
        {
            if (_coins == null) return;
            foreach (var coin in _coins) coin.Complete();
        }

        private void OnDestroy()
        {
            if (_coins != null) foreach (var coin in _coins) coin.Kill();
            if (_punch != null && _punch.IsActive()) _punch.Kill();
        }

        /// <summary>One pooled coin: an Image and a float tween 0..1 whose setter moves it along the curve.</summary>
        private sealed class Coin
        {
            public readonly RectTransform Rect;
            private readonly GameHud _owner;
            private readonly Tween _tween;
            private Vector2 _from;
            private Vector2 _to;

            public bool Flying { get; private set; }

            public Coin(GameHud owner, RectTransform parent, int index)
            {
                _owner = owner;
                var image = UiParts.Image(parent, "Coin " + index, Color.white, PlaceholderSprites.Coin, sliced: false);
                image.raycastTarget = false;
                Rect = image.rectTransform;
                Rect.sizeDelta = new Vector2(UiFeel.CoinDp, UiFeel.CoinDp);
                Rect.gameObject.SetActive(false);
                if (!Tweens.IsReady) return;
                // Delegates made once per coin: flying a coin allocates nothing.
                _tween = DOTween.To(Zero, Apply, 1f, UiFeel.CoinSeconds)
                    .SetEase(Ease.InOutQuad)
                    .SetAutoKill(false)
                    .SetRecyclable(false)
                    .SetLink(owner.gameObject)
                    .OnComplete(Landed)
                    .Pause();
            }

            private static float Zero() => 0f;

            /// <summary>Starts a flight. False when tweens are not running (edit mode): the clear counts without a coin.</summary>
            public bool Launch(Vector2 from, Vector2 to)
            {
                if (_tween == null) return false;
                _from = from;
                _to = to;
                Flying = true;
                Rect.gameObject.SetActive(true);
                Apply(0f);
                _tween.Restart();
                return true;
            }

            private void Apply(float p)
            {
                Rect.localPosition = UiFeel.CoinPosition(_from, _to, p);
                float s = UiFeel.CoinScale(p);
                Rect.localScale = new Vector3(s, s, 1f);
            }

            private void Landed()
            {
                Flying = false;
                Rect.gameObject.SetActive(false);
                _owner.OnCoinLanded();
            }

            public void Complete()
            {
                if (Flying && _tween != null) _tween.Complete(true);
            }

            public void Stop()
            {
                if (_tween != null) _tween.Pause();
                Flying = false;
                Rect.gameObject.SetActive(false);
            }

            public void Kill()
            {
                if (_tween != null && _tween.IsActive()) _tween.Kill();
            }
        }
    }
}
