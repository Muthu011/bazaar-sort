using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// A white rounded-square sprite generated in code for placeholder tiles and shelves, so the placeholder board
    /// needs no art asset. Built once and shared; tint it with <see cref="SpriteRenderer.color"/> and draw it
    /// <see cref="SpriteDrawMode.Sliced"/> so the corners stay round at any size. Also a white padlock silhouette for
    /// locked shelves (<see cref="Padlock"/>).
    /// </summary>
    public static class PlaceholderSprites
    {
        private const int TextureSize = 64;
        private const int CornerRadius = 16;

        private const int PadlockWidth = 64;
        private const int PadlockHeight = 70;
        private const int PadlockBodyTop = 46;
        private const float PadlockBodyRadius = 10f;
        private const float ShackleOuter = 21f;
        private const float ShackleInner = 12f;

        /// <summary>Padlock height / width.</summary>
        public const float PadlockAspect = (float)PadlockHeight / PadlockWidth;

        /// <summary>Centre of the padlock body below the sprite's pivot, as a fraction of the sprite's width.</summary>
        public const float PadlockBodyCentreOffset = (PadlockHeight * 0.5f - PadlockBodyTop * 0.5f) / PadlockWidth;

        /// <summary>Height of the padlock body as a fraction of the sprite's width.</summary>
        public const float PadlockBodyHeight = (float)PadlockBodyTop / PadlockWidth;

        private static Sprite _roundedSquare;
        private static Sprite _padlock;

        /// <summary>One world unit across at its natural size; 9-slice borders are the corner radius.</summary>
        public static Sprite RoundedSquare
        {
            get
            {
                if (_roundedSquare == null) _roundedSquare = CreateRoundedSquare();
                return _roundedSquare;
            }
        }

        /// <summary>
        /// A white padlock (shackle on a rounded body), one world unit wide and <see cref="PadlockAspect"/> tall, pivot
        /// in the centre. Draw it in simple mode and size it with the transform scale.
        /// </summary>
        public static Sprite Padlock
        {
            get
            {
                if (_padlock == null) _padlock = CreatePadlock();
                return _padlock;
            }
        }

        private static Sprite CreatePadlock()
        {
            var texture = new Texture2D(PadlockWidth, PadlockHeight, TextureFormat.RGBA32, false)
            {
                name = "PlaceholderPadlock",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[PadlockWidth * PadlockHeight];
            float cx = PadlockWidth * 0.5f;
            float r = PadlockBodyRadius;
            for (int y = 0; y < PadlockHeight; y++)
            {
                for (int x = 0; x < PadlockWidth; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    // Body: rounded rectangle over the bottom rows.
                    float dx = Mathf.Max(Mathf.Max(r - px, px - (PadlockWidth - r)), 0f);
                    float dy = Mathf.Max(Mathf.Max(r - py, py - (PadlockBodyTop - r)), 0f);
                    float body = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);

                    // Shackle: the top half of a ring sitting on the body, with straight legs down into it.
                    float shackle = 0f;
                    float ringY = PadlockBodyTop;
                    float ox = px - cx;
                    float oy = Mathf.Max(py - ringY, 0f);
                    float d = Mathf.Sqrt(ox * ox + oy * oy);
                    if (py >= ringY - 4f)
                    {
                        shackle = Mathf.Clamp01(Mathf.Min(ShackleOuter - d, d - ShackleInner) + 0.5f);
                    }

                    float alpha = Mathf.Max(body, shackle);
                    pixels[y * PadlockWidth + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, PadlockWidth, PadlockHeight), new Vector2(0.5f, 0.5f),
                PadlockWidth, 0, SpriteMeshType.FullRect);
            sprite.name = "PlaceholderPadlock";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
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
