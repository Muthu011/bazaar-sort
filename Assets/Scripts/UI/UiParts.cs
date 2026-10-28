using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Small uGUI building blocks shared by the HUD and panels built from code.</summary>
    internal static class UiParts
    {
        public static Image Image(Transform parent, string name, Color32 color, Sprite sprite, bool sliced = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sliced && sprite != null)
            {
                image.type = UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1.5f;
            }
            return image;
        }

        public static Text Text(Transform parent, string name, Font font, string text, int size, Color32 color, TextAnchor anchor)
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

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
