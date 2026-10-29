using DG.Tweening;
using Game.Services;
using Game.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The in-game HUD (issues #21, #40), built from code with uGUI on its own screen-space camera canvas, laid out by
    /// <see cref="HudLayout"/> inside the safe area. Top bar: the level number. Bottom bar: placeholder booster slots
    /// (boosters arrive in #42), the coin pill (coin icon and the wallet balance from the bootstrap's
    /// <see cref="LevelBootstrap.Progress"/>, issue #44, refreshed whenever progress changes: a win's coins, a booster
    /// spend; 0 without progress; text from <see cref="CoinBalanceText"/>)
    /// and the settings button, which opens the <see cref="SettingsPanel"/> with the haptics on/off toggle. On every <see cref="BoardView.ClearShown"/> that animated, a coin
    /// flies from the triple to the pill (DOTween, pooled: <see cref="UiFeel.CoinPool"/> coins, each with one tween
    /// built once and restarted) and the pill punches as it lands. That flight is feel only: clears earn no coins, so
    /// the number stays the wallet; <see cref="ClearCounter"/> tracks the clears and the coins in flight. The clear
    /// tally resets on every level load.
    /// Thin: no rules; the setting lives in <see cref="ISettingsService"/>.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        internal const string HapticsOnLabel = "On";
        internal const string HapticsOffLabel = "Off";
        internal const string LevelPrefix = "Level ";

        /// <summary>Above the board and its particles, below the result panels (100) and the dev overlay (110).</summary>
        private const int CanvasSortingOrder = 50;

        private static readonly Color32 PillColor = new Color32(0x1B, 0x0F, 0x0C, 0xA0);
        private static readonly Color32 CounterText = new Color32(0xFF, 0xF8, 0xE1, 0xFF);
        private static readonly Color32 BoosterSlotColor = new Color32(0x1B, 0x0F, 0x0C, 0x50);
        private static readonly Color32 GearColor = new Color32(0xFF, 0xF4, 0xDC, 0xFF);  // card cream
        private static readonly Color32 LevelColor = new Color32(0xFF, 0xF8, 0xE1, 0xFF);

        [SerializeField] private BoardView _view;
        [SerializeField] private LevelBootstrap _levels;
        [SerializeField] private Camera _camera;

        private readonly ClearCounter _counter = new ClearCounter();
        private ISettingsService _settings;
        private ISettingsService _subscribedSettings;
        private BoardView _subscribedView;
        private LevelBootstrap _subscribedLevels;
        private IProgressService _subscribedProgress;
        private int _shownBalance = -1;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private CanvasScaler _scaler;
        private RectTransform _safeArea;
        private RectTransform _pill;
        private RectTransform _icon;
        private Text _count;
        private Text _level;
        private Image[] _boosters;
        private Button _settingsButton;
        private SettingsPanel _settingsPanel;
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
            Corner(_pill);
            var iconImage = UiParts.Image(_pill, "Coin Icon", Color.white, PlaceholderSprites.Coin, sliced: false);
            iconImage.raycastTarget = false;
            _icon = iconImage.rectTransform;
            _icon.anchorMin = _icon.anchorMax = new Vector2(0f, 0.5f);
            _icon.sizeDelta = new Vector2(28f, 28f);
            _icon.anchoredPosition = new Vector2(8f + 14f, 0f);
            _count = UiParts.Text(_pill, "Count", font, CoinBalanceText.For(0), HudLayout.CounterFontDp, CounterText, TextAnchor.MiddleRight);
            // Four-digit balances shrink to fit the pill instead of spilling out of it.
            _count.resizeTextForBestFit = true;
            _count.resizeTextMinSize = HudLayout.CounterFontDp / 2;
            _count.resizeTextMaxSize = HudLayout.CounterFontDp;
            var countRect = _count.rectTransform;
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(40f, 0f);
            countRect.offsetMax = new Vector2(-12f, 0f);

            // Top bar: the level number.
            _level = UiParts.Text(_safeArea, "Level Number", font, string.Empty, HudLayout.LevelFontDp, LevelColor, TextAnchor.MiddleCenter);
            Corner(_level.rectTransform);

            // Bottom bar: placeholder booster slots (no boosters yet: they take no taps).
            _boosters = new Image[HudLayout.BoosterSlots];
            for (int i = 0; i < _boosters.Length; i++)
            {
                _boosters[i] = UiParts.Image(_safeArea, "Booster Slot " + (i + 1), BoosterSlotColor, PlaceholderSprites.RoundedSquare);
                _boosters[i].raycastTarget = false;
                Corner(_boosters[i].rectTransform);
            }

            // Settings button: a gear on a dark pill.
            var settingsImage = UiParts.Image(_safeArea, "Settings Button", PillColor, PlaceholderSprites.RoundedSquare);
            Corner(settingsImage.rectTransform);
            _settingsButton = settingsImage.gameObject.AddComponent<Button>();
            _settingsButton.targetGraphic = settingsImage;
            _settingsButton.onClick.AddListener(OpenSettings);
            settingsImage.gameObject.AddComponent<ButtonPress>();
            var gear = UiParts.Image(settingsImage.rectTransform, "Gear", GearColor, PlaceholderSprites.Gear, sliced: false);
            gear.raycastTarget = false;
            gear.rectTransform.anchorMin = gear.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            gear.rectTransform.sizeDelta = new Vector2(28f, 28f);

            _settingsPanel = new SettingsPanel(transform, viewCamera, font, ToggleHaptics, CloseSettings);

            // Coin pool, drawn over the HUD row.
            _coins = new Coin[UiFeel.CoinPool];
            for (int i = 0; i < _coins.Length; i++) _coins[i] = new Coin(this, _canvasRect, i);

            if (Tweens.IsReady)
            {
                _punch = _pill.DOPunchScale(new Vector3(UiFeel.CounterPunch, UiFeel.CounterPunch, 0f), UiFeel.CounterPunchSeconds, 6, 0.6f)
                    .SetAutoKill(false).SetRecyclable(false).SetLink(gameObject).Pause();
            }

            _built = true;
            _shownBalance = -1;
            WatchProgress();
            RefreshToggle();
            // The bootstrap may have loaded its first level before this HUD was built.
            if (_levels != null && _levels.CurrentIndex >= 0) ShowLevel(_levels.CurrentIndex + 1);
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
            var settings = BoardLayoutSettings.Default;
            var m = HudLayout.Compute(safeDp, settings.TopReserveFraction, settings.BottomReserveFraction);
            Place(_level.rectTransform, m.LevelLabel);
            Place(_pill, m.Counter);
            Place((RectTransform)_settingsButton.transform, m.Settings);
            for (int i = 0; i < _boosters.Length; i++) Place(_boosters[i].rectTransform, m.Boosters[i]);
            _settingsPanel.Layout(safeAreaPx, density);
        }

        /// <summary>Anchors and pivots a HUD piece at the safe area's bottom-left, so HudLayout rectangles map 1:1.</summary>
        private static void Corner(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
        }

        private static void Place(RectTransform rect, Rect r)
        {
            rect.sizeDelta = r.size;
            rect.anchoredPosition = r.position;
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
            WatchProgress();
        }

        /// <summary>
        /// Follows the bootstrap's progress (it can be set after the HUD is wired, so this runs again on every level
        /// load) and shows its balance.
        /// </summary>
        private void WatchProgress()
        {
            var progress = _subscribedLevels != null ? _subscribedLevels.Progress : null;
            if (!ReferenceEquals(progress, _subscribedProgress))
            {
                if (_subscribedProgress != null) _subscribedProgress.Changed -= RefreshBalance;
                _subscribedProgress = progress;
                if (_subscribedProgress != null) _subscribedProgress.Changed += RefreshBalance;
            }
            RefreshBalance();
        }

        /// <summary>Shows the wallet balance; the text is only rebuilt when the number changes.</summary>
        private void RefreshBalance()
        {
            if (!_built) return;
            int balance = _subscribedProgress != null ? _subscribedProgress.Coins : 0;
            if (balance == _shownBalance) return;
            _shownBalance = balance;
            _count.text = CoinBalanceText.For(balance);
        }

        private void Unsubscribe()
        {
            if (_subscribedView != null) _subscribedView.ClearShown -= OnClearShown;
            if (_subscribedLevels != null) _subscribedLevels.LevelLoaded -= OnLevelLoaded;
            if (_subscribedSettings != null) _subscribedSettings.Changed -= RefreshToggle;
            if (_subscribedProgress != null) _subscribedProgress.Changed -= RefreshBalance;
            _subscribedProgress = null;
            _subscribedView = null;
            _subscribedLevels = null;
            _subscribedSettings = null;
        }

        private void OnLevelLoaded(int index)
        {
            ResetCounter();
            WatchProgress();
            ShowLevel(index + 1);
            CloseSettings();
        }

        /// <summary>Shows "Level <paramref name="number"/>" in the top bar (1-based; level loads call it).</summary>
        public void ShowLevel(int number)
        {
            if (_built) _level.text = number > 0 ? LevelPrefix + number : string.Empty;
        }

        // ---- Settings panel ----

        public bool IsSettingsOpen => _built && _settingsPanel.IsOpen;

        /// <summary>Opens the settings panel; the board ignores the pointer while it is open.</summary>
        public void OpenSettings()
        {
            if (!_built) return;
            RefreshToggle();
            _settingsPanel.SetOpen(true);
            if (_view != null) _view.InputBlocked = true;
        }

        public void CloseSettings()
        {
            if (!_built || !_settingsPanel.IsOpen) return;
            _settingsPanel.SetOpen(false);
            if (_view != null) _view.InputBlocked = false;
        }

        /// <summary>Clear tally back to 0 and every flying coin dropped (level load, Retry, Next, Replay). The wallet is untouched.</summary>
        public void ResetCounter()
        {
            _counter.Reset();
            if (_built)
            {
                foreach (var coin in _coins) coin.Stop();
                if (_punch != null) _punch.Rewind();
            }
        }

        private void OnClearShown(ClearBurst clear)
        {
            var coin = clear.Animated && _built ? FreeCoin() : null;
            if (coin == null || !coin.Launch(ScreenToCanvas(clear.Pixel), ScreenToCanvas(IconScreenPoint())))
            {
                _counter.AddClear(false);
                return;
            }
            _counter.AddClear(true);
        }

        private void OnCoinLanded()
        {
            if (!_counter.CoinLanded()) return;
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
            _settingsPanel.ShowHaptics(Settings.HapticsEnabled);
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
        internal Button HapticsToggle => _settingsPanel?.HapticsToggle;
        internal string HapticsLabel => _settingsPanel?.HapticsLabel;
        internal Button SettingsButton => _settingsButton;
        internal Button SettingsCloseButton => _settingsPanel?.CloseButton;
        internal RectTransform SettingsCard => _settingsPanel?.Card;
        internal string LevelText => _level == null ? null : _level.text;
        internal RectTransform LevelRect => _level == null ? null : _level.rectTransform;
        internal Image[] BoosterSlots => _boosters;
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
