using UnityEngine;

namespace Game.View
{
    /// <summary>One frame of the L1 hand pointer: how far along the path, how visible, how pressed.</summary>
    public readonly struct HandPose
    {
        public HandPose(float travel, float alpha, float scale)
        {
            Travel = travel;
            Alpha = alpha;
            Scale = scale;
        }

        /// <summary>0 at the item to move, 1 at the target slot.</summary>
        public float Travel { get; }

        public float Alpha { get; }

        /// <summary>1 at rest, <see cref="TutorialCueMotion.PressScale"/> while "pressing" the item.</summary>
        public float Scale { get; }
    }

    /// <summary>
    /// Timing of the tutorial cues (issue #57), pure so it is Edit Mode tested. The hand loops every
    /// <see cref="HandPeriod"/> seconds: fade in on the item, press, glide to the target slot, let go, fade out, pause.
    /// The L2 arrow bobs up and down. Times in seconds; the functions allocate nothing.
    /// </summary>
    public static class TutorialCueMotion
    {
        public const float HandPeriod = 2f;
        public const float PressScale = 0.85f;

        /// <summary>Arrow bob: one cycle per this many seconds.</summary>
        public const float ArrowBobPeriod = 1f;

        /// <summary>Arrow bob height, in tiles, either way from rest.</summary>
        public const float ArrowBobTiles = 0.08f;

        private const float FadeInEnd = 0.25f;
        private const float PressEnd = 0.45f;
        private const float GlideEnd = 1.25f;
        private const float ReleaseEnd = 1.45f;
        private const float FadeOutEnd = 1.8f;

        /// <summary>The hand at <paramref name="time"/> seconds since the cue appeared (loops).</summary>
        public static HandPose Hand(float time)
        {
            float t = Mathf.Repeat(Mathf.Max(0f, time), HandPeriod);
            if (t < FadeInEnd) return new HandPose(0f, t / FadeInEnd, 1f);
            if (t < PressEnd) return new HandPose(0f, 1f, Mathf.Lerp(1f, PressScale, (t - FadeInEnd) / (PressEnd - FadeInEnd)));
            if (t < GlideEnd) return new HandPose(Mathf.SmoothStep(0f, 1f, (t - PressEnd) / (GlideEnd - PressEnd)), 1f, PressScale);
            if (t < ReleaseEnd) return new HandPose(1f, 1f, Mathf.Lerp(PressScale, 1f, (t - GlideEnd) / (ReleaseEnd - GlideEnd)));
            if (t < FadeOutEnd) return new HandPose(1f, 1f - (t - ReleaseEnd) / (FadeOutEnd - ReleaseEnd), 1f);
            return new HandPose(1f, 0f, 1f);
        }

        /// <summary>Arrow offset above its rest point, in tiles, at <paramref name="time"/> seconds.</summary>
        public static float ArrowBob(float time)
        {
            return Mathf.Sin(Mathf.Max(0f, time) * (2f * Mathf.PI / ArrowBobPeriod)) * ArrowBobTiles;
        }
    }
}
