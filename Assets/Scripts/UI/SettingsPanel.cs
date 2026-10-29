using Game.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The small settings panel (issue #40), built from code by <see cref="GameHud"/> on its own canvas: a scrim over the
    /// whole screen (a tap on it closes the panel) and a card centred in the safe area with the title, a Haptics row
    /// (On / Off button, the setting lives in <c>ISettingsService</c>) and Close. Sizes from <see cref="SettingsPanelLayout"/>.
    /// Thin: it raises clicks; GameHud owns the setting and board input.
    /// </summary>
    internal sealed class SettingsPanel
    {
        internal const string TitleText = "Settings";
        internal const string HapticsRowText = "Haptics";
        internal const string CloseText = "Close";

        /// <summary>Above the HUD (50), below the result panels (100) and the dev overlay (110).</summary>
        private const int CanvasSortingOrder = 90;

        // World 1 palette (docs/style-guide.md).
        private static readonly Color32 ScrimColor = new Color32(0x2A, 0x3A, 0x44, 0x99);   // night slate, 60%
        private static readonly Color32 CardColor = new Color32(0xFF, 0xF4, 0xDC, 0xFF);    // card cream
        private static readonly Color32 TextColor = new Color32(0x3B, 0x24, 0x18, 0xFF);    // roasted brown
        private static readonly Color32 OnColor = new Color32(0x24, 0x73, 0x90, 0xFF);      // peacock blue, white text
        private static readonly Color32 OnText = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color32 OffColor = new Color32(0xE9, 0xDC, 0xC3, 0xFF);     // sandstone, brown text
        private static readonly Color32 CloseColor = new Color32(0xF2, 0xB6, 0x32, 0xFF);   // turmeric, brown text

        private readonly Camera _camera;
        private readonly Canvas _canvas;
        private readonly CanvasScaler _scaler;
        private readonly GameObject _root;
        private readonly RectTransform _card;
        private readonly RectTransform _title;
        private readonly RectTransform _rowLabel;
        private readonly Image _toggleImage;
        private readonly Text _toggleLabel;

        public SettingsPanel(Transform parent, Camera camera, Font font, UnityAction onToggle, UnityAction onClose)
        {
            _camera = camera;
            var canvasObject = new GameObject("Settings Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = camera;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = CanvasSortingOrder;
            _scaler = canvasObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            // The scrim covers the whole screen and swallows taps that miss the card; a tap on it closes the panel.
            var scrim = UiParts.Image(canvasObject.transform, "Settings Panel", ScrimColor, null);
            UiParts.Stretch(scrim.rectTransform);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            scrimButton.onClick.AddListener(onClose);
            _root = scrim.gameObject;

            var card = UiParts.Image(_root.transform, "Card", CardColor, PlaceholderSprites.RoundedSquare);
            _card = card.rectTransform;
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);

            var title = UiParts.Text(_card, "Title", font, TitleText, SettingsPanelLayout.TitleFontDp, TextColor, TextAnchor.MiddleCenter);
            _title = Centred(title.rectTransform);
            var row = UiParts.Text(_card, "Haptics Label", font, HapticsRowText, SettingsPanelLayout.RowFontDp, TextColor, TextAnchor.MiddleLeft);
            _rowLabel = Centred(row.rectTransform);

            _toggleImage = UiParts.Image(_card, "Haptics Toggle", OnColor, PlaceholderSprites.RoundedSquare);
            Centred(_toggleImage.rectTransform);
            HapticsToggle = _toggleImage.gameObject.AddComponent<Button>();
            HapticsToggle.targetGraphic = _toggleImage;
            HapticsToggle.onClick.AddListener(onToggle);
            _toggleImage.gameObject.AddComponent<ButtonPress>();
            _toggleLabel = UiParts.Text(_toggleImage.rectTransform, "Label", font, GameHud.HapticsOnLabel,
                SettingsPanelLayout.RowFontDp, OnText, TextAnchor.MiddleCenter);
            UiParts.Stretch(_toggleLabel.rectTransform);

            var close = UiParts.Image(_card, "Close", CloseColor, PlaceholderSprites.RoundedSquare);
            Centred(close.rectTransform);
            CloseButton = close.gameObject.AddComponent<Button>();
            CloseButton.targetGraphic = close;
            CloseButton.onClick.AddListener(onClose);
            close.gameObject.AddComponent<ButtonPress>();
            var closeLabel = UiParts.Text(close.rectTransform, "Label", font, CloseText, SettingsPanelLayout.ButtonFontDp,
                TextColor, TextAnchor.MiddleCenter);
            UiParts.Stretch(closeLabel.rectTransform);

            _root.SetActive(false);
        }

        public Button HapticsToggle { get; }
        public Button CloseButton { get; }
        public string HapticsLabel => _toggleLabel.text;
        public RectTransform Card => _card;
        public Canvas Canvas => _canvas;
        public bool IsOpen => _root.activeSelf;

        public void SetOpen(bool open) => _root.SetActive(open);

        public void ShowHaptics(bool on)
        {
            _toggleLabel.text = on ? GameHud.HapticsOnLabel : GameHud.HapticsOffLabel;
            _toggleLabel.color = on ? OnText : TextColor;
            _toggleImage.color = on ? OnColor : OffColor;
        }

        public void Layout(Rect safeAreaPx, float density)
        {
            _scaler.scaleFactor = density;
            ResultPanelLayout.SafeAreaAnchors(safeAreaPx, _camera.pixelWidth, _camera.pixelHeight, out var min, out var max);
            var m = SettingsPanelLayout.Compute(new Vector2(safeAreaPx.width / density, safeAreaPx.height / density));
            // The card is a child of the full-screen scrim, so anchor it at the safe area's centre.
            _card.anchorMin = _card.anchorMax = (min + max) * 0.5f;
            _card.sizeDelta = m.Card;
            _card.anchoredPosition = Vector2.zero;
            Place(_title, m.Title);
            Place(_rowLabel, m.HapticsLabel);
            Place(_toggleImage.rectTransform, m.HapticsToggle);
            Place((RectTransform)CloseButton.transform, m.Close);
        }

        private static RectTransform Centred(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Place(RectTransform rect, Rect r)
        {
            rect.sizeDelta = r.size;
            rect.anchoredPosition = r.center;
        }
    }
}
