using System;
using DG.Tweening;
using Game.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The placeholder Win panel (Next, Retry), Fail panel ("Out of space!", Free Extra Slot, Retry) and More-levels-soon panel (Replay), built from code with uGUI on a screen-space
    /// camera canvas over the board. The canvas works in dp (constant pixel size, scale factor = screen density), so
    /// <see cref="ResultPanelLayout"/> sizes come out the same on every phone, and the cards sit inside the safe area.
    ///
    /// Thin: shows a panel when <see cref="BoardView.OutcomeReady"/> fires (after the clear animations; which one is
    /// <see cref="ResultPanelChoice"/>: winning the last level shows More-levels-soon instead of Win), hides on
    /// <see cref="LevelBootstrap.LevelLoaded"/>, shows More-levels-soon when the game opens with every level already won
    /// (<see cref="LevelBootstrap.AllLevelsCleared"/>, issue #22), and turns button taps into <see cref="LevelBootstrap.Next"/>,
    /// <see cref="LevelBootstrap.Retry"/> and <see cref="LevelBootstrap.ReplayFromFirst"/>. Text uses Unity's built-in legacy font, like the board labels.
    ///
    /// Feel (issue #21): every button presses down (<see cref="ButtonPress"/>). The Win card has a row of
    /// <see cref="UiFeel.WinStars"/> star slots that drop in one at a time with a scale punch: the stars the win earned
    /// (<see cref="LevelBootstrap.LastWinStars"/>, issue #44; all three when no level list is wired) in marigold, the
    /// rest as dim empty stars,
    /// then confetti falls (<see cref="ParticleFx.CreateConfetti"/>); More-levels-soon, also a win, gets the confetti.
    /// The empty-star colour <c>#D7CCC8</c> is a placeholder until the Slice 4 art pass.
    ///
    /// Coins (issue #44, Muthu on PR #63): under the stars a "+N" line with a coin (<see cref="WinCoinsText"/>) shows the
    /// coins this win earned (<see cref="LevelBootstrap.LastWinCoins"/>); a replay that earned 0 has no line and the card
    /// is as tall as before. With the HUD wired (<see cref="Configure"/>), the card holds those coins back from the HUD
    /// pill the moment they are saved (<see cref="LevelBootstrap.WinRewarded"/>), and when the stars have landed they fly
    /// from the line into the pill, which counts up as they land (<see cref="GameHud.FlyWinCoins"/>). Any other panel,
    /// or hiding the card, shows the full balance at once.
    /// The star drop is one DOTween sequence built once and restarted on every Win; outside play mode (screenshots)
    /// the stars just show at rest.
    ///
    /// Fail (issue #41): shown on <see cref="BoardOutcome.Failed"/>. Retry reloads the level and nothing else happens:
    /// no interstitial and no penalty after a fail (docs/game-design.md). The card also holds a rewarded-ad Continue
    /// ("Free Extra Slot" with a video mark, Muthu on PR #51): the main, taller button above Retry, which then turns
    /// into the smaller secondary button. Until Slice 6 Continue is hidden and wired to nothing; Retry alone is the
    /// main button and the card is sized for it. <see cref="ShowContinueForPreview"/> shows it for screenshots and tests.
    /// button for Slice 6; it is hidden and wired to nothing, and the card is sized for Retry alone until it shows.
    /// </summary>
    public sealed class ResultPanels : MonoBehaviour
    {
        internal const string WinTitle = "Cleared!";
        internal const string FailTitle = "Out of space!";
        // Final wording of the rewarded button is checked in Slice 6, when the ad is wired (docs/game-design.md, Fail).
        internal const string ContinueLabel = "Free Extra Slot";
        internal const string NextLabel = "Next";
        internal const string RetryLabel = "Retry";
        internal const string MoreLevelsTitle = "More levels coming soon";
        internal const string ReplayLabel = "Replay";

        /// <summary>The More-levels-soon title as drawn: two lines, so it fits the 320 dp card at the title size.</summary>
        private const string MoreLevelsTitleLines = "More levels\ncoming soon";
        private const int MoreLevelsTitleLineCount = 2;

        private const int CanvasSortingOrder = 100;

        /// <summary>Fail card buttons, top to bottom: the rewarded Continue (main, hidden until Slice 6), then Retry.</summary>
        private const int FailContinueIndex = 0;
        private const int FailRetryIndex = 1;

        /// <summary>The video mark beside the Continue label, in dp: a rounded rect with a play triangle inside.</summary>
        internal const float VideoIconWidthDp = 28f;
        internal const float VideoIconHeightDp = 20f;
        internal const float VideoIconGapDp = 10f;
        private const float VideoPlayDp = 12f;

        /// <summary>Confetti draws over the result card (100), under the dev overlay (110).</summary>
        private const int ConfettiSortingOrder = 105;

        private static readonly Color32 StarColor = new Color32(0xFF, 0xB3, 0x00, 0xFF); // marigold
        // PLACEHOLDER until the Slice 4 art pass (docs/game-design.md, Stars and coins): pale clay for an unearned star.
        private static readonly Color32 StarEmptyColor = new Color32(0xD7, 0xCC, 0xC8, 0xFF);

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
        [SerializeField] private GameHud _hud;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _safeArea;
        private GameObject _backdrop;
        private Card _win;
        private Card _fail;
        private RectTransform _videoIcon;
        private RectTransform _continueLabel;
        private Text _continueText;
        private Rect _layoutSafeAreaPx;
        private float _layoutDensity = 1f;
        private Card _moreLevels;
        private RectTransform _coinLine;
        private Text _coinLineText;
        private RectTransform _coinLineIcon;
        private Vector2 _layoutSafeDp;
        private TweenCallback _starsLanded;
        private bool _built;
        private Sequence _stars;
        private ParticleSystem _confetti;
        private int _confettiBursts;
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

        /// <summary>Replay on More-levels-soon was tapped (after <see cref="LevelBootstrap.ReplayFromFirst"/> ran, if wired).</summary>
        public event Action ReplayClicked;

        /// <summary>
        /// Wires the view, level list and camera from code (scene builder, tests), and the HUD whose coin pill the Win
        /// card's coins fly into (null: no flight, the pill shows a win's coins at once).
        /// </summary>
        public void Configure(BoardView view, LevelBootstrap levels, Camera viewCamera, GameHud hud = null)
        {
            Unsubscribe();
            _view = view;
            _levels = levels;
            _camera = viewCamera;
            _hud = hud;
            if (isActiveAndEnabled) Subscribe();
        }

        public bool IsWinVisible => _built && _win.Root.activeSelf;
        public bool IsFailVisible => _built && _fail.Root.activeSelf;

        /// <summary>True when the Fail card's rewarded-ad Continue button is on screen. Always false until Slice 6.</summary>
        public bool IsContinueVisible => IsFailVisible && _fail.Buttons[FailContinueIndex].gameObject.activeSelf;
        public bool IsMoreLevelsVisible => _built && _moreLevels.Root.activeSelf;

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

            _win = new Card(_safeArea, "Win Panel", WinTitle, font, 2, UiFeel.WinStars);
            _win.SetButton(0, NextLabel, PrimaryColor, PrimaryText, OnNext);
            _win.SetButton(1, RetryLabel, SecondaryColor, SecondaryText, OnRetry);
            BuildCoinLine(font);
            _fail = new Card(_safeArea, "Fail Panel", FailTitle, font, 2);
            // Rewarded-ad continue arrives in Slice 6: built now so the card has its place, hidden and wired to nothing.
            _fail.SetButton(FailContinueIndex, ContinueLabel, PrimaryColor, PrimaryText, null);
            _fail.SetButton(FailRetryIndex, RetryLabel, PrimaryColor, PrimaryText, OnRetry);
            BuildVideoIcon(_fail.Buttons[FailContinueIndex]);
            _fail.Buttons[FailContinueIndex].gameObject.SetActive(false);
            _moreLevels = new Card(_safeArea, "More Levels Panel", MoreLevelsTitleLines, font, 1);
            _moreLevels.SetButton(0, ReplayLabel, PrimaryColor, PrimaryText, OnReplay);

            BuildFeel();

            if (ensureEventSystem && FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(transform, false);
            }

            _built = true;
            Hide();
            LayoutForScreen();
            // The bootstrap may have started first: it opened on an all-cleared save before this card existed.
            if (_levels != null && _levels.IsAwaitingReplay) Show(ResultPanel.MoreLevelsSoon);
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
            _layoutSafeDp = safeDp;
            ApplyWinLayout();
            _layoutSafeAreaPx = safeAreaPx;
            _layoutDensity = density;
            ApplyFailLayout(safeDp);
            _moreLevels.Apply(ResultPanelLayout.Compute(safeDp, 1, MoreLevelsTitleLineCount));
        }

        /// <summary>
        /// Shows the panel for <paramref name="outcome"/> (None hides all). A win on the last level of the wired list
        /// shows More-levels-soon; with no level list wired, a win shows Win.
        /// </summary>
        public void Show(BoardOutcome outcome)
        {
            Show(ResultPanelChoice.For(outcome, _levels == null || _levels.HasNext));
        }

        /// <summary>Shows exactly <paramref name="panel"/> (None hides all).</summary>
        public void Show(ResultPanel panel)
        {
            if (!_built) return;
            _win.Root.SetActive(panel == ResultPanel.Win);
            _fail.Root.SetActive(panel == ResultPanel.Fail);
            _moreLevels.Root.SetActive(panel == ResultPanel.MoreLevelsSoon);
            _backdrop.SetActive(panel != ResultPanel.None);

            StopConfetti();
            if (panel == ResultPanel.Win)
            {
                _win.SetEarnedStars(EarnedStars, StarColor, StarEmptyColor);
                ShowCoinLine(EarnedCoins);
                PlayStars();
            }
            else
            {
                if (_stars != null) _stars.Pause();
                // No coin line on this panel: whatever the pill held back shows at once.
                if (_hud != null) _hud.ReleaseWinCoins();
            }
            if (panel == ResultPanel.MoreLevelsSoon) PlayConfetti();
        }

        // ---- Fail card (issue #41) ----

        private bool ContinueShown => _fail.Buttons[FailContinueIndex].gameObject.activeSelf;

        /// <summary>
        /// Sizes the Fail card for the buttons on show. Continue hidden: Retry alone, as the main button. Continue shown:
        /// Continue on top as the taller main button, Retry under it in the smaller secondary style.
        /// </summary>
        private void ApplyFailLayout(Vector2 safeDp)
        {
            bool withContinue = ContinueShown;
            var retry = _fail.Buttons[FailRetryIndex];
            ((Image)retry.targetGraphic).color = withContinue ? SecondaryColor : PrimaryColor;
            retry.GetComponentInChildren<Text>(true).color = withContinue ? SecondaryText : PrimaryText;
            _fail.Apply(withContinue
                ? ResultPanelLayout.Compute(safeDp, 2, leadButton: true)
                : ResultPanelLayout.Compute(safeDp, 1));
            if (withContinue) LayoutVideoIcon();
        }

        /// <summary>
        /// The video mark: a rounded rect in the label colour with a marigold play triangle, beside the label. Drawn
        /// from code (<see cref="PlaceholderSprites"/>), no icon asset.
        /// </summary>
        private void BuildVideoIcon(Button button)
        {
            _continueText = button.GetComponentInChildren<Text>(true);
            _continueLabel = _continueText.rectTransform;
            var frame = UiParts.Image(button.transform, "Video Icon", PrimaryText, PlaceholderSprites.RoundedSquare);
            frame.raycastTarget = false;
            frame.pixelsPerUnitMultiplier = 4f;
            _videoIcon = frame.rectTransform;
            _videoIcon.anchorMin = _videoIcon.anchorMax = new Vector2(0.5f, 0.5f);
            _videoIcon.sizeDelta = new Vector2(VideoIconWidthDp, VideoIconHeightDp);
            var play = UiParts.Image(_videoIcon, "Play", PrimaryColor, PlaceholderSprites.PlayTriangle, sliced: false);
            play.raycastTarget = false;
            var playRect = play.rectTransform;
            playRect.anchorMin = playRect.anchorMax = new Vector2(0.5f, 0.5f);
            playRect.sizeDelta = new Vector2(VideoPlayDp, VideoPlayDp);
            playRect.anchoredPosition = new Vector2(1f, 0f);
        }

        /// <summary>Centres icon + gap + label as one group on the button.</summary>
        private void LayoutVideoIcon()
        {
            float text = _continueText.preferredWidth;
            float group = VideoIconWidthDp + VideoIconGapDp + text;
            float shift = (VideoIconWidthDp + VideoIconGapDp) * 0.5f;
            _continueLabel.offsetMin = new Vector2(shift, 0f);
            _continueLabel.offsetMax = new Vector2(shift, 0f);
            _videoIcon.anchoredPosition = new Vector2(-group * 0.5f + VideoIconWidthDp * 0.5f, 0f);
        }

        /// <summary>
        /// Shows or hides the rewarded Continue and lays the card out again. QA and test hook only: the game keeps it
        /// hidden until Slice 6.
        /// </summary>
        internal void ShowContinueForPreview(bool shown)
        {
            if (!_built) return;
            _fail.Buttons[FailContinueIndex].gameObject.SetActive(shown);
            float density = Mathf.Max(0.5f, _layoutDensity);
            ApplyFailLayout(new Vector2(_layoutSafeAreaPx.width / density, _layoutSafeAreaPx.height / density));
        }

        // ---- Coin line (issue #44, PR #63) ----

        private void BuildCoinLine(Font font)
        {
            _coinLine = new GameObject("Coin Line", typeof(RectTransform)).GetComponent<RectTransform>();
            _coinLine.SetParent(_win.Rect, false);
            _coinLine.anchorMin = _coinLine.anchorMax = new Vector2(0.5f, 0.5f);
            _coinLineText = CreateText(_coinLine, "Coins Earned", font, string.Empty, ResultPanelLayout.CoinLineFontDp, TitleColor);
            var textRect = _coinLineText.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            var icon = UiParts.Image(_coinLine, "Coin", Color.white, PlaceholderSprites.Coin, sliced: false);
            icon.raycastTarget = false;
            _coinLineIcon = icon.rectTransform;
            _coinLineIcon.anchorMin = _coinLineIcon.anchorMax = new Vector2(0.5f, 0.5f);
            _coinLineIcon.sizeDelta = new Vector2(ResultPanelLayout.CoinLineIconDp, ResultPanelLayout.CoinLineIconDp);
            _coinLine.gameObject.SetActive(false);
        }

        /// <summary>"+N" and a coin, centred as one group, for <paramref name="coins"/>; hidden for 0. Once per Win.</summary>
        private void ShowCoinLine(int coins)
        {
            bool shown = WinCoinsText.IsShown(coins);
            _coinLine.gameObject.SetActive(shown);
            if (shown)
            {
                _coinLineText.text = WinCoinsText.For(coins);
                float text = _coinLineText.preferredWidth;
                float group = text + ResultPanelLayout.CoinLineGapDp + ResultPanelLayout.CoinLineIconDp;
                var textRect = _coinLineText.rectTransform;
                textRect.sizeDelta = new Vector2(text, ResultPanelLayout.CoinLineDp);
                textRect.anchoredPosition = new Vector2(-group * 0.5f + text * 0.5f, 0f);
                _coinLineIcon.anchoredPosition = new Vector2(group * 0.5f - ResultPanelLayout.CoinLineIconDp * 0.5f, 0f);
            }
            ApplyWinLayout();
        }

        /// <summary>The Win card for the last layout, with the coin line's band only while the line is shown.</summary>
        private void ApplyWinLayout()
        {
            if (_win == null) return;
            bool coinLine = _coinLine != null && _coinLine.gameObject.activeSelf;
            var metrics = ResultPanelLayout.Compute(_layoutSafeDp, 2, 1, ResultPanelLayout.WinRowDp(coinLine));
            _win.Apply(metrics);
            _win.PlaceStarRow(ResultPanelLayout.WinStarsCentre(metrics));
            if (_coinLine != null)
            {
                _coinLine.sizeDelta = new Vector2(metrics.Card.x - 2f * metrics.Padding, ResultPanelLayout.CoinLineDp);
                _coinLine.anchoredPosition = ResultPanelLayout.WinCoinLineCentre(metrics);
            }
        }

        /// <summary>The stars have landed: the line's coins fly into the HUD pill (or show there at once).</summary>
        private void PayOutCoins()
        {
            if (_hud == null) return;
            if (!_coinLine.gameObject.activeInHierarchy)
            {
                _hud.ReleaseWinCoins();
                return;
            }
            _hud.FlyWinCoins(RectTransformUtility.WorldToScreenPoint(_camera, _coinLineIcon.position));
        }

        private void OnStarsLanded()
        {
            PlayConfetti();
            PayOutCoins();
        }

        private void OnWinRewarded(int stars, int coins)
        {
            if (_hud != null) _hud.HoldWinCoins(coins);
        }

        // ---- Stars and confetti (issue #21) ----

        private void BuildFeel()
        {
            _win.ShowStarsAtRest();
            if (!Tweens.IsReady) return;

            _starsLanded = OnStarsLanded;
            _confetti = ParticleFx.CreateConfetti("Win Confetti", ConfettiSortingOrder);
            _confetti.transform.SetParent(transform, false);

            _stars = DOTween.Sequence().SetAutoKill(false).SetRecyclable(false).SetLink(gameObject).Pause();
            for (int i = 0; i < _win.Stars.Length; i++)
            {
                var star = _win.Stars[i];
                var rect = star.rectTransform;
                float start = UiFeel.StarStart(i);
                _stars.Insert(start, star.DOFade(1f, 0.06f));
                _stars.Insert(start, rect.DOAnchorPos(_win.StarRest(i), UiFeel.StarDropSeconds).SetEase(Ease.InQuad));
                _stars.Insert(start + UiFeel.StarDropSeconds,
                    rect.DOPunchScale(new Vector3(UiFeel.StarPunch, UiFeel.StarPunch, 0f), UiFeel.StarPunchSeconds, 6, 0.5f));
            }
            _stars.InsertCallback(UiFeel.StarsSeconds, _starsLanded);
        }

        private void PlayStars()
        {
            if (_stars == null)
            {
                _win.ShowStarsAtRest();
                PayOutCoins();
                return;
            }
            _win.PrepareStarDrop();
            _stars.Restart();
        }

        private void PlayConfetti()
        {
            if (_confetti == null) return;
            // World units are pixels / 100 (BoardView's camera); rain from just above the top edge, across the width.
            float width = _camera.pixelWidth / BoardView.PixelsPerUnit;
            float height = _camera.pixelHeight / BoardView.PixelsPerUnit;
            float k = width / 10.8f; // sizes tuned on a 1080 px wide screen
            _confetti.transform.position = new Vector3(width * 0.5f, height + 0.3f * k, 0f);
            var shape = _confetti.shape;
            shape.scale = new Vector3(width, 0.1f, 0.1f);
            var main = _confetti.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f * k, 0.3f * k);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * k, 4f * k);
            _confetti.Play();
            _confetti.Emit(ParticleFx.ConfettiCount);
            _confettiBursts++;
        }

        private void StopConfetti()
        {
            if (_confetti != null) _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDestroy()
        {
            if (_stars != null && _stars.IsActive()) _stars.Kill();
        }

        public void Hide() => Show(ResultPanel.None);

        private void Subscribe()
        {
            Unsubscribe();
            _subscribedView = _view;
            _subscribedLevels = _levels;
            if (_subscribedView != null) _subscribedView.OutcomeReady += Show;
            if (_subscribedLevels != null)
            {
                _subscribedLevels.LevelLoaded += OnLevelLoaded;
                _subscribedLevels.AllLevelsCleared += OnAllLevelsCleared;
                _subscribedLevels.WinRewarded += OnWinRewarded;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedView != null) _subscribedView.OutcomeReady -= Show;
            if (_subscribedLevels != null)
            {
                _subscribedLevels.LevelLoaded -= OnLevelLoaded;
                _subscribedLevels.AllLevelsCleared -= OnAllLevelsCleared;
                _subscribedLevels.WinRewarded -= OnWinRewarded;
            }
            _subscribedView = null;
            _subscribedLevels = null;
        }

        private void OnLevelLoaded(int index) => Hide();

        /// <summary>The game opened with every level won (issue #22): More-levels-soon over the last level.</summary>
        private void OnAllLevelsCleared() => Show(ResultPanel.MoreLevelsSoon);

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

        private void OnReplay()
        {
            Hide();
            if (_levels != null) _levels.ReplayFromFirst();
            ReplayClicked?.Invoke();
        }

        // ---- Test hooks (Game.Tests.PlayMode) ----

        internal Button WinNextButton => _win.Buttons[0];
        internal Button WinRetryButton => _win.Buttons[1];
        internal Button FailRetryButton => _fail.Buttons[FailRetryIndex];
        internal Button FailContinueButton => _fail.Buttons[FailContinueIndex];
        internal Button MoreLevelsReplayButton => _moreLevels.Buttons[0];
        internal Button[] MoreLevelsButtons => _moreLevels.Buttons;
        internal Text MoreLevelsTitleLabel => _moreLevels.Title;
        internal RectTransform MoreLevelsCard => _moreLevels.Rect;
        internal RectTransform WinCard => _win.Rect;
        internal RectTransform FailCard => _fail.Rect;
        internal Text FailTitleLabel => _fail.Title;
        internal RectTransform FailVideoIcon => _videoIcon;
        internal Canvas Canvas => _canvas;

        /// <summary>Stars to light on the Win card: the bootstrap's last win, or all of them without one.</summary>
        private int EarnedStars => _levels != null && _levels.LastWinStars > 0 ? _levels.LastWinStars : UiFeel.WinStars;

        /// <summary>Coins the Win card shows: the bootstrap's last win, or 0.</summary>
        private int EarnedCoins => _levels != null ? _levels.LastWinCoins : 0;

        /// <summary>The "+N" line, and whether it is on the card.</summary>
        internal Text WinCoinsLabel => _coinLineText;
        internal bool IsWinCoinsLineVisible => IsWinVisible && _coinLine.gameObject.activeSelf;
        internal RectTransform WinCoinsIcon => _coinLineIcon;

        /// <summary>Shows the coin line for <paramref name="coins"/> without a win (screenshots).</summary>
        internal void ShowCoinsForPreview(int coins) => ShowCoinLine(coins);

        /// <summary>Win card star slots (always <see cref="UiFeel.WinStars"/>).</summary>
        internal Image[] WinStars => _win.Stars;

        /// <summary>How many Win card stars are lit (earned colour).</summary>
        internal int WinStarsLit
        {
            get
            {
                int n = 0;
                foreach (var star in _win.Stars)
                {
                    var c = (Color32)star.color;
                    if (c.r == StarColor.r && c.g == StarColor.g && c.b == StarColor.b) n++;
                }
                return n;
            }
        }

        /// <summary>True while the stars are still dropping.</summary>
        internal bool AreStarsPlaying => _stars != null && _stars.IsPlaying();

        /// <summary>True when every Win star is shown, opaque, at full size and in its place.</summary>
        internal bool AreStarsAtRest => _win.StarsAtRest();

        internal int ConfettiBursts => _confettiBursts;
        internal ParticleSystem Confetti => _confetti;

        /// <summary>Finishes the star drop at once, confetti included (tests, screenshots).</summary>
        internal void CompleteStars()
        {
            if (_stars != null && _stars.IsPlaying()) _stars.Complete(true);
        }

        /// <summary>Jumps the star drop to <paramref name="seconds"/> and holds it there (screenshots).</summary>
        internal void GotoStars(float seconds)
        {
            if (_stars != null) _stars.Goto(seconds, false);
        }

        internal Button[] AllButtons
        {
            get
            {
                var all = new Button[_win.Buttons.Length + _fail.Buttons.Length + _moreLevels.Buttons.Length];
                _win.Buttons.CopyTo(all, 0);
                _fail.Buttons.CopyTo(all, _win.Buttons.Length);
                _moreLevels.Buttons.CopyTo(all, _win.Buttons.Length + _fail.Buttons.Length);
                return all;
            }
        }

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
            public readonly Text Title;
            public readonly Image[] Stars;
            private readonly RectTransform _title;
            private readonly RectTransform _starRow;
            private readonly Font _font;

            public Card(Transform parent, string name, string title, Font font, int buttonCount, int starCount = 0)
            {
                _font = font;
                var background = CreateImage(parent, name, CardColor, true);
                Root = background.gameObject;
                Rect = background.rectTransform;
                Rect.anchorMin = Rect.anchorMax = new Vector2(0.5f, 0.5f);
                Rect.pivot = new Vector2(0.5f, 0.5f);

                var titleText = CreateText(Rect, "Title", font, title, ResultPanelLayout.TitleFontDp, TitleColor);
                Title = titleText;
                _title = titleText.rectTransform;
                _title.anchorMin = _title.anchorMax = new Vector2(0.5f, 0.5f);

                Stars = new Image[starCount];
                if (starCount > 0)
                {
                    _starRow = new GameObject("Stars", typeof(RectTransform)).GetComponent<RectTransform>();
                    _starRow.SetParent(Rect, false);
                    _starRow.anchorMin = _starRow.anchorMax = new Vector2(0.5f, 0.5f);
                    for (int i = 0; i < starCount; i++)
                    {
                        var star = UiParts.Image(_starRow, "Star " + i, StarColor, PlaceholderSprites.Star, sliced: false);
                        star.raycastTarget = false;
                        star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        star.rectTransform.sizeDelta = new Vector2(ResultPanelLayout.StarDp, ResultPanelLayout.StarDp);
                        Stars[i] = star;
                    }
                }

                Buttons = new Button[buttonCount];
                for (int i = 0; i < buttonCount; i++)
                {
                    var image = CreateImage(Rect, "Button " + i, PrimaryColor, true);
                    var rect = image.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    var button = image.gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    image.gameObject.AddComponent<ButtonPress>();
                    Buttons[i] = button;
                }
            }

            /// <summary>Where star <paramref name="index"/> rests inside the star row (fixed, so tweens can keep their end values).</summary>
            public Vector2 StarRest(int index)
            {
                float step = ResultPanelLayout.StarDp + ResultPanelLayout.StarGapDp;
                return new Vector2((index - (Stars.Length - 1) * 0.5f) * step, 0f);
            }

            /// <summary>Moves the star row to <paramref name="centre"/> (the Win card puts it at the top of its band).</summary>
            public void PlaceStarRow(Vector2 centre)
            {
                if (_starRow != null) _starRow.anchoredPosition = centre;
            }

            public void ShowStarsAtRest()
            {
                for (int i = 0; i < Stars.Length; i++) SetStar(i, StarRest(i), 1f);
            }

            /// <summary>Every star hidden above its place: the state the drop sequence starts from.</summary>
            public void PrepareStarDrop()
            {
                for (int i = 0; i < Stars.Length; i++) SetStar(i, StarRest(i) + new Vector2(0f, UiFeel.StarDropDp), 0f);
            }

            public bool StarsAtRest()
            {
                if (Stars.Length == 0) return false;
                for (int i = 0; i < Stars.Length; i++)
                {
                    var star = Stars[i];
                    if (!star.gameObject.activeInHierarchy || star.color.a < 0.999f) return false;
                    if (Vector2.Distance(star.rectTransform.anchoredPosition, StarRest(i)) > 0.01f) return false;
                    if (Mathf.Abs(star.rectTransform.localScale.x - 1f) > 0.001f) return false;
                }
                return true;
            }

            /// <summary>The first <paramref name="earned"/> stars in <paramref name="on"/>, the rest in <paramref name="off"/>; alpha is kept for the drop.</summary>
            public void SetEarnedStars(int earned, Color32 on, Color32 off)
            {
                for (int i = 0; i < Stars.Length; i++)
                {
                    Color c = i < earned ? (Color)on : (Color)off;
                    c.a = Stars[i].color.a;
                    Stars[i].color = c;
                }
            }

            private void SetStar(int index, Vector2 position, float alpha)
            {
                var star = Stars[index];
                star.rectTransform.anchoredPosition = position;
                star.rectTransform.localScale = Vector3.one;
                var c = star.color;
                c.a = alpha;
                star.color = c;
            }

            public void SetButton(int index, string label, Color32 color, Color32 textColor, UnityEngine.Events.UnityAction onClick)
            {
                var button = Buttons[index];
                button.name = label + " Button";
                ((Image)button.targetGraphic).color = color;
                CreateText(button.transform, "Label", _font, label, ResultPanelLayout.ButtonFontDp, textColor);
                if (onClick != null) button.onClick.AddListener(onClick);
            }

            public void Apply(ResultPanelMetrics metrics)
            {
                Rect.sizeDelta = metrics.Card;
                Rect.anchoredPosition = Vector2.zero;
                _title.sizeDelta = new Vector2(metrics.Card.x - 2f * metrics.Padding, metrics.TitleHeight);
                _title.anchoredPosition = metrics.TitleCentre;
                if (_starRow != null) _starRow.anchoredPosition = metrics.StarRowCentre;
                // Hidden buttons take no place: the shown ones stack from the top (Fail card while Continue is hidden).
                int slot = 0;
                for (int i = 0; i < Buttons.Length; i++)
                {
                    if (!Buttons[i].gameObject.activeSelf) continue;
                    var rect = (RectTransform)Buttons[i].transform;
                    rect.sizeDelta = metrics.ButtonSize(slot);
                    rect.anchoredPosition = metrics.ButtonCentre(slot);
                    slot++;
                }
            }
        }
    }
}
