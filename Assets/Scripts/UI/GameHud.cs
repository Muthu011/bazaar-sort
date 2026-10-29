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
    /// and the settings button, which opens the <see cref="SettingsPanel"/> with the haptics on/off toggle.
    ///
    /// Coin pill (Muthu, PR #63): the text is always drawn at <see cref="HudLayout.CounterFontDp"/>, never shrunk; the
    /// pill widens leftwards to fit it (<see cref="HudLayout.CounterWidthFor"/>, 84 dp up to five digits).
    /// Win coins: the <see cref="ResultPanels"/> Win card calls <see cref="HoldWinCoins"/> just before a win's coins are
    /// saved, so the pill keeps the old balance (<see cref="CoinPayout"/>), then <see cref="FlyWinCoins"/> from its
    /// "+N" line: pooled coins (<see cref="UiFeel.CoinPool"/>, each one tween built once and restarted) fly over the
    /// card into the pill, which is lifted above the card's dim backdrop and counts up as each lands. Texts for every
    /// step are made when the coins are held, so the flight allocates nothing. Clears fly no coin and earn none;
    /// <see cref="ClearCounter"/> only tallies them, reset on every level load (which also shows any held coins at once).
    /// Thin: no rules; the setting lives in <see cref="ISettingsService"/>.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        internal const string HapticsOnLabel = "On";
        internal const string HapticsOffLabel = "Off";
        internal const string LevelPrefix = "Level ";

        /// <summary>Above the board and its particles, below the result panels (100) and the dev overlay (110).</summary>
        private const int CanvasSortingOrder = 50;

        /// <summary>The pill while Win coins fly into it: above the result card (100), under the coins.</summary>
        private const int LiftedPillSortingOrder = 102;

        /// <summary>Flying Win coins: over the result card and the lifted pill, under confetti (105) and the dev overlay (110).</summary>
        private const int CoinLayerSortingOrder = 103;

        private static readonly Color32 PillColor = new Color32(0x1B, 0x0F, 0x0C, 0xA0);
        private static readonly Color32 CounterText = new Color32(0xFF, 0xF8, 0xE1, 0xFF);
        private static readonly Color32 BoosterSlotColor = new Color32(0x1B, 0x0F, 0x0C, 0x50);
        private static readonly Color32 GearColor = new Color32(0xFF, 0xF4, 0xDC, 0xFF);  // card cream
        private static readonly Color32 LevelColor = new Color32(0xFF, 0xF8, 0xE1, 0xFF);

        [SerializeField] private BoardView _view;
        [SerializeField] private LevelBootstrap _levels;
        [SerializeField] private Camera _camera;

        private readonly ClearCounter _counter = new ClearCounter();
        private readonly CoinPayout _payout = new CoinPayout();
        private readonly string[] _payoutTexts = new string[UiFeel.CoinPool + 1];
        private readonly float[] _payoutWidths = new float[UiFeel.CoinPool + 1];
        private ISettingsService _settings;
        private ISettingsService _subscribedSettings;
        private BoardView _subscribedView;
        private LevelBootstrap _subscribedLevels;
        private IProgressService _subscribedProgress;
        private int _shownBalance = -1;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _safeArea;
        private RectTransform _pill;
        private Canvas _pillCanvas;
        private RectTransform _coinLayer;
        private Vector2 _pillRightBottom;
        private float _pillWidth = HudLayout.CounterMinWidthDp;
        private RectTransform _icon;
        private Text _count;
        private Text _level;
        private Image[] _boosters;
        private Button _settingsButton;
        private SettingsPanel _settingsPanel;
        private Coin[] _coins;
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

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _safeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            _safeArea.SetParent(canvasObject.transform, false);

            // Coin pill: a dark pill with the coin icon on the left and the wallet balance on the right. Its own canvas
            // lets it rise above the result card's backdrop while a win's coins fly in.
            var pillImage = UiParts.Image(_safeArea, "Coin Pill", PillColor, PlaceholderSprites.RoundedSquare);
            pillImage.raycastTarget = false;
            _pill = pillImage.rectTransform;
            Corner(_pill);
            _pillCanvas = _pill.gameObject.AddComponent<Canvas>();
            _pillCanvas.overrideSorting = true;
            _pillCanvas.sortingOrder = CanvasSortingOrder;
            var iconImage = UiParts.Image(_pill, "Coin Icon", Color.white, PlaceholderSprites.Coin, sliced: false);
            iconImage.raycastTarget = false;
            _icon = iconImage.rectTransform;
            _icon.anchorMin = _icon.anchorMax = new Vector2(0f, 0.5f);
            _icon.sizeDelta = new Vector2(HudLayout.CounterIconDp, HudLayout.CounterIconDp);
            _icon.anchoredPosition = new Vector2(HudLayout.CounterIconLeftDp + HudLayout.CounterIconDp * 0.5f, 0f);
            // Always the full font size: the pill grows to fit, the text never shrinks (PR #63).
            _count = UiParts.Text(_pill, "Count", font, CoinBalanceText.For(0), HudLayout.CounterFontDp, CounterText, TextAnchor.MiddleRight);
            _count.resizeTextForBestFit = false;
            var countRect = _count.rectTransform;
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.one;
            countRect.offsetMin = new Vector2(HudLayout.CounterTextLeftDp, 0f);
            countRect.offsetMax = new Vector2(-HudLayout.CounterTextRightDp, 0f);

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

            // Coin pool for a win's coins, on a layer over the result card.
            _coinLayer = new GameObject("Coin Layer", typeof(RectTransform)).GetComponent<RectTransform>();
            _coinLayer.SetParent(canvasObject.transform, false);
            UiParts.Stretch(_coinLayer);
            var layerCanvas = _coinLayer.gameObject.AddComponent<Canvas>();
            layerCanvas.overrideSorting = true;
            layerCanvas.sortingOrder = CoinLayerSortingOrder;
            _coins = new Coin[UiFeel.CoinPool];
            for (int i = 0; i < _coins.Length; i++) _coins[i] = new Coin(this, _coinLayer, i);

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
            _pillRightBottom = new Vector2(m.Counter.xMax, m.Counter.yMin);
            PlacePill();
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

        /// <summary>The pill at its current width, its right edge fixed beside the settings button.</summary>
        private void PlacePill()
        {
            _pill.sizeDelta = new Vector2(_pillWidth, HudLayout.CounterHeightDp);
            _pill.anchoredPosition = new Vector2(_pillRightBottom.x - _pillWidth, _pillRightBottom.y);
        }

        /// <summary>Shows <paramref name="balance"/> as <paramref name="text"/>, widening or narrowing the pill to fit it.</summary>
        private void ShowBalance(int balance, string text, float width)
        {
            _shownBalance = balance;
            _count.text = text;
            if (Mathf.Approximately(width, _pillWidth)) return;
            _pillWidth = width;
            PlacePill();
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

        private int Wallet => _subscribedProgress != null ? _subscribedProgress.Coins : 0;

        /// <summary>
        /// Shows the wallet balance, less any win coins still held for the flight; the text is only rebuilt when the
        /// number changes (progress events, never per frame).
        /// </summary>
        private void RefreshBalance()
        {
            if (!_built) return;
            int balance = _payout.Shown(Wallet);
            if (balance == _shownBalance) return;
            string text = CoinBalanceText.For(balance);
            ShowBalance(balance, text, HudLayout.CounterWidthFor(text));
        }

        // ---- Win coins (issue #44, PR #63) ----

        /// <summary>
        /// A win is about to add <paramref name="coins"/> to the wallet (<see cref="LevelBootstrap.WinRewarded"/>, before
        /// the save): the pill keeps showing the balance without them until <see cref="FlyWinCoins"/> lands them. The
        /// pill's text and width for every landing are made here, so the flight itself allocates nothing.
        /// </summary>
        internal void HoldWinCoins(int coins)
        {
            StopCoins();
            _payout.Hold(coins);
            int before = Wallet; // the save has not happened yet
            for (int i = 0; i <= _payout.Flown && i < _payoutTexts.Length; i++)
            {
                int shown = WinCoinsAdded(before, coins - _payout.HeldAfter(i));
                _payoutTexts[i] = CoinBalanceText.For(shown);
                _payoutWidths[i] = HudLayout.CounterWidthFor(_payoutTexts[i]);
            }
        }

        private static int WinCoinsAdded(int wallet, int added) => Game.Core.WinRewards.AddCoins(wallet, added < 0 ? 0 : added);

        /// <summary>
        /// Flies the held coins from <paramref name="fromScreen"/> (the Win card's "+N" coin, screen pixels) into the pill,
        /// which rises over the card's backdrop and counts up as each lands. With nothing held, or no tweens (edit mode),
        /// the full balance shows at once. Returns true when coins are flying.
        /// </summary>
        internal bool FlyWinCoins(Vector2 fromScreen)
        {
            if (!_built) return false;
            int flown = _payout.Launch();
            if (flown == 0 || !_coins[0].CanFly)
            {
                ReleaseWinCoins();
                return false;
            }
            SetPillLifted(true);
            Vector2 from = ScreenToCoinLayer(fromScreen);
            Vector2 to = ScreenToCoinLayer(IconScreenPoint());
            for (int i = 0; i < flown; i++) _coins[i].Launch(from, to, i * UiFeel.CoinStaggerSeconds);
            return true;
        }

        /// <summary>Drops any flying win coin, shows the whole wallet and puts the pill back under the backdrop.</summary>
        internal void ReleaseWinCoins()
        {
            StopCoins();
            _payout.Release();
            SetPillLifted(false);
            RefreshBalance();
        }

        private void SetPillLifted(bool lifted)
        {
            if (_pillCanvas != null) _pillCanvas.sortingOrder = lifted ? LiftedPillSortingOrder : CanvasSortingOrder;
        }

        private void StopCoins()
        {
            if (_coins == null) return;
            foreach (var coin in _coins) coin.Stop();
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

        /// <summary>
        /// Clear tally back to 0 (level load, Retry, Next, Replay); any win coins still held or flying show at once. The
        /// wallet is untouched.
        /// </summary>
        public void ResetCounter()
        {
            _counter.Reset();
            ReleaseWinCoins();
        }

        private void OnClearShown(ClearBurst clear) => _counter.AddClear();

        /// <summary>A win coin reached the pill: count up its share, from the texts made in <see cref="HoldWinCoins"/>.</summary>
        private void OnCoinLanded()
        {
            if (!_payout.Land()) return;
            int step = _payout.Landed;
            ShowBalance(_payout.Shown(Wallet), _payoutTexts[step], _payoutWidths[step]);
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

        private Vector2 IconScreenPoint() => RectTransformUtility.WorldToScreenPoint(_camera, _icon.position);

        private Vector2 ScreenToCoinLayer(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_coinLayer, screen, _camera, out var local);
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
        internal Text CountLabel => _count;
        internal Canvas Canvas => _canvas;

        /// <summary>The balance on the pill (the wallet less coins still on their way).</summary>
        internal int ShownBalance => _shownBalance;

        /// <summary>Win coins in the wallet that the pill doesn't show yet.</summary>
        internal int HeldCoins => _payout.Held;

        /// <summary>True while the pill draws above the result card (win coins flying in).</summary>
        internal bool IsPillLifted => _pillCanvas != null && _pillCanvas.sortingOrder == LiftedPillSortingOrder;

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

        /// <summary>Lands every flying coin at once, in launch order (tests, screenshots).</summary>
        internal void CompleteCoins()
        {
            if (_coins == null) return;
            foreach (var coin in _coins) coin.Complete();
        }

        private void OnDestroy()
        {
            if (_coins != null) foreach (var coin in _coins) coin.Kill();
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
                // The coin stays hidden through its stagger delay and shows on its first step (see Apply).
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

            /// <summary>False when tweens are not running (edit mode): coins can't fly and show at once instead.</summary>
            public bool CanFly => _tween != null;

            /// <summary>Starts a flight after <paramref name="delay"/> seconds.</summary>
            public void Launch(Vector2 from, Vector2 to, float delay)
            {
                if (_tween == null) return;
                _from = from;
                _to = to;
                Flying = true;
                Rect.gameObject.SetActive(false);
                Rect.localPosition = from;
                _tween.Restart(true, delay);
            }

            private void Apply(float p)
            {
                if (p > 0f && !Rect.gameObject.activeSelf) Rect.gameObject.SetActive(true);
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
