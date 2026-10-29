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
        private static Sprite _shadow;
        private static Sprite _dot;
        private static Sprite _star;
        private static Sprite _coin;
        private static Sprite _gear;

        /// <summary>
        /// A soft white ellipse, twice as wide as tall, one world unit wide: the pick-up and flight shadow (tint it dark
        /// and see-through).
        /// </summary>
        public static Sprite SoftShadow => _shadow != null ? _shadow : (_shadow = CreateSoftShadow());

        /// <summary>A soft-edged white disc, one world unit across: particle sparks and confetti.</summary>
        public static Sprite Dot => _dot != null ? _dot : (_dot = CreateDot());

        /// <summary>A white five-point star, one world unit across: the Win panel stars.</summary>
        public static Sprite Star => _star != null ? _star : (_star = CreateStar());

        /// <summary>A gold coin (disc with a darker rim and a lighter face), one world unit across: the clear coin.</summary>
        public static Sprite Coin => _coin != null ? _coin : (_coin = CreateCoin());

        /// <summary>A white eight-tooth gear with a hole, one world unit across: the settings button icon (issue #40).</summary>
        public static Sprite Gear => _gear != null ? _gear : (_gear = CreateGear());

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

        private const int EffectSize = 64;

        private static Sprite CreateSoftShadow()
        {
            const int w = 64, h = 32;
            return Build("PlaceholderSoftShadow", w, h, (x, y) =>
            {
                float dx = (x - w * 0.5f) / (w * 0.5f);
                float dy = (y - h * 0.5f) / (h * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 1.6f;
            });
        }

        private static Sprite CreateDot()
        {
            const float r = EffectSize * 0.5f;
            return Build("PlaceholderDot", EffectSize, EffectSize, (x, y) =>
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
                return Mathf.Clamp01((r - d) / 6f);
            });
        }

        private static Sprite CreateStar()
        {
            const float c = EffectSize * 0.5f;
            const float outer = EffectSize * 0.48f;
            const float inner = outer * 0.42f;
            const float sector = Mathf.PI / 5f; // 36 degrees: from a point to the next notch
            return Build("PlaceholderStar", EffectSize, EffectSize, (x, y) =>
            {
                // The star is star-shaped around its centre, so a pixel is inside when it lies on the centre's side
                // of the straight edge (point to notch) of its angular sector. Signed distance gives a soft edge.
                float dx = x - c;
                float dy = y - c;
                float angle = Mathf.Repeat(Mathf.Atan2(dx, dy), 2f * Mathf.PI); // 0 = straight up, clockwise
                int k = Mathf.FloorToInt(angle / sector);
                float a0 = k * sector;
                float a1 = a0 + sector;
                float r0 = k % 2 == 0 ? outer : inner;
                float r1 = k % 2 == 0 ? inner : outer;
                var p0 = new Vector2(Mathf.Sin(a0) * r0, Mathf.Cos(a0) * r0);
                var p1 = new Vector2(Mathf.Sin(a1) * r1, Mathf.Cos(a1) * r1);
                var edge = p1 - p0;
                var normal = new Vector2(edge.y, -edge.x).normalized; // points away from the centre
                if (Vector2.Dot(normal, -p0) > 0f) normal = -normal;
                float distance = Vector2.Dot(new Vector2(dx, dy) - p0, normal); // > 0 outside
                return Mathf.Clamp01(0.5f - distance);
            });
        }

        private static Sprite CreateCoin()
        {
            const float r = EffectSize * 0.5f - 1f;
            var rim = new Color32(0xC7, 0x8A, 0x00, 0xFF);
            var face = new Color32(0xFF, 0xC8, 0x2E, 0xFF);
            var shine = new Color32(0xFF, 0xE6, 0x8A, 0xFF);
            var texture = NewTexture("PlaceholderCoin", EffectSize, EffectSize);
            var pixels = new Color32[EffectSize * EffectSize];
            for (int y = 0; y < EffectSize; y++)
            {
                for (int x = 0; x < EffectSize; x++)
                {
                    float px = x + 0.5f - EffectSize * 0.5f;
                    float py = y + 0.5f - EffectSize * 0.5f;
                    float d = Mathf.Sqrt(px * px + py * py);
                    Color32 c = d < r * 0.72f ? face : rim;
                    if (d < r * 0.72f && d > r * 0.45f && px < 0f && py > 0f) c = shine;
                    c.a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                    pixels[y * EffectSize + x] = c;
                }
            }
            return Finish(texture, pixels, EffectSize, EffectSize);
        }

        private static Sprite CreateGear()
        {
            const float c = EffectSize * 0.5f;
            const float outer = EffectSize * 0.47f;
            const float body = EffectSize * 0.35f;
            const float hole = EffectSize * 0.14f;
            const int teeth = 8;
            return Build("PlaceholderGear", EffectSize, EffectSize, (x, y) =>
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // Each tooth is half of its angular period; soften its sides by their distance in pixels at this radius.
                float t = Mathf.Repeat(Mathf.Atan2(dy, dx) * teeth / (2f * Mathf.PI), 1f);
                float side = (0.25f - Mathf.Abs(t - 0.5f)) * (2f * Mathf.PI / teeth) * d; // > 0 inside the tooth
                float tooth = Mathf.Min(Mathf.Clamp01(side + 0.5f), Mathf.Clamp01(outer - d + 0.5f));
                float ring = Mathf.Max(Mathf.Clamp01(body - d + 0.5f), tooth);
                return Mathf.Min(ring, Mathf.Clamp01(d - hole + 0.5f));
            });
        }

        private static Sprite Build(string name, int width, int height, System.Func<float, float, float> alpha)
        {
            var texture = NewTexture(name, width, height);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            return Finish(texture, pixels, width, height);
        }

        private static Texture2D NewTexture(string name, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        /// <summary>Uploads and makes a sprite one world unit wide, pivot centred.</summary>
        private static Sprite Finish(Texture2D texture, Color32[] pixels, int width, int height)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width, 0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
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
