using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Builds the placeholder particle systems from code with Unity's built-in ParticleSystem and a sprite made in code
    /// (<see cref="PlaceholderSprites.Dot"/>): the clear burst and the win confetti (issue #21). Each system is made
    /// once and reused; particles are emitted with <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/>
    /// (a struct, no allocation). Particles are capped (<see cref="BurstMaxParticles"/>,
    /// <see cref="ConfettiMaxParticles"/>) for low-end phones.
    /// </summary>
    public static class ParticleFx
    {
        public const int BurstMaxParticles = 96;
        public const int ConfettiMaxParticles = 140;

        /// <summary>Sparks per clear: the item's colour and a few marigold ones.</summary>
        public const int BurstItemSparks = 12;
        public const int BurstGoldSparks = 6;

        public const int ConfettiCount = 90;

        private static Material _material;

        /// <summary>
        /// The pipeline's default sprite material (the one every SpriteRenderer here uses, so it is always in the
        /// build) with the dot texture.
        /// </summary>
        public static Material Material
        {
            get
            {
                if (_material != null) return _material;
                var probe = new GameObject("Sprite Material Probe") { hideFlags = HideFlags.HideAndDontSave };
                var renderer = probe.AddComponent<SpriteRenderer>();
                _material = new Material(renderer.sharedMaterial)
                {
                    name = "PlaceholderParticles",
                    mainTexture = PlaceholderSprites.Dot.texture,
                    hideFlags = HideFlags.DontSave,
                };
                Destroy(probe);
                return _material;
            }
        }

        /// <summary>A world-space burst system, idle, emitting only through Emit. Sizes are set per burst.</summary>
        public static ParticleSystem CreateBurst(string name, int sortingOrder)
        {
            var ps = CreateSystem(name, sortingOrder, BurstMaxParticles);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.gravityModifier = 0.8f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.arc = 360f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeOut());

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            return ps;
        }

        /// <summary>A world-space confetti system: coloured flakes falling and spinning. Sizes are set by the caller.</summary>
        public static ParticleSystem CreateConfetti(string name, int sortingOrder)
        {
            var ps = CreateSystem(name, sortingOrder, ConfettiMaxParticles);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
            main.gravityModifier = 0.35f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(Rainbow()) { mode = ParticleSystemGradientMode.RandomColor };

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(90f, 0f, 0f); // emit downwards in the XY plane

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeOut(0.75f));
            return ps;
        }

        private static ParticleSystem CreateSystem(string name, int sortingOrder, int maxParticles)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = sortingOrder;
            return ps;
        }

        private static Gradient FadeOut(float fadeFrom = 0.5f)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeFrom), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        /// <summary>Festive placeholder colours: marigold, kumkum red, peacock teal, turmeric, leaf green, rose.</summary>
        private static Gradient Rainbow()
        {
            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color32(0xFF, 0xB3, 0x00, 0xFF), 0.16f),
                    new GradientColorKey(new Color32(0xD3, 0x2F, 0x2F, 0xFF), 0.33f),
                    new GradientColorKey(new Color32(0x00, 0x83, 0x8F, 0xFF), 0.5f),
                    new GradientColorKey(new Color32(0xFF, 0xEB, 0x3B, 0xFF), 0.66f),
                    new GradientColorKey(new Color32(0x43, 0xA0, 0x47, 0xFF), 0.83f),
                    new GradientColorKey(new Color32(0xEC, 0x40, 0x7A, 0xFF), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static void Destroy(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
