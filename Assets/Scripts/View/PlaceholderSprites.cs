using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// A white rounded-square sprite generated in code for placeholder tiles and shelves, so the placeholder board
    /// needs no art asset. Built once and shared; tint it with <see cref="SpriteRenderer.color"/> and draw it
    /// <see cref="SpriteDrawMode.Sliced"/> so the corners stay round at any size.
    /// </summary>
    public static class PlaceholderSprites
    {
        private const int TextureSize = 64;
        private const int CornerRadius = 16;

        private static Sprite _roundedSquare;

        /// <summary>One world unit across at its natural size; 9-slice borders are the corner radius.</summary>
        public static Sprite RoundedSquare
        {
            get
            {
                if (_roundedSquare == null) _roundedSquare = CreateRoundedSquare();
                return _roundedSquare;
            }
        }

        private static Sprite CreateRoundedSquare()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "PlaceholderRoundedSquare",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[TextureSize * TextureSize];
            float r = CornerRadius;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    // Distance from the pixel centre to the nearest point of the inner (unrounded) square.
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dx = Mathf.Max(Mathf.Max(r - px, px - (TextureSize - r)), 0f);
                    float dy = Mathf.Max(Mathf.Max(r - py, py - (TextureSize - r)), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r - distance + 0.5f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                TextureSize, 0, SpriteMeshType.FullRect, new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius));
            sprite.name = "PlaceholderRoundedSquare";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
