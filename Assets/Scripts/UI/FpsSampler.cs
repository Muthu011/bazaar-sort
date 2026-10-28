using System;

namespace Game.UI
{
    /// <summary>
    /// Averages frame rate over a fixed window (frames / seconds) and keeps the slowest frame in that window. A struct
    /// with no allocations, so the dev fps readout can call <see cref="AddFrame"/> every Update. Edit Mode tested.
    /// </summary>
    public struct FpsSampler
    {
        /// <summary>Window length used by the dev readout, in seconds.</summary>
        public const float DefaultWindowSeconds = 0.5f;

        private readonly float _window;
        private float _elapsed;
        private float _worst;
        private int _frames;

        public FpsSampler(float windowSeconds)
        {
            if (windowSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            _window = windowSeconds;
            _elapsed = 0f;
            _worst = 0f;
            _frames = 0;
            Fps = 0;
            WorstFrameMs = 0;
        }

        /// <summary>Average frames per second over the last full window, rounded. 0 before the first window ends.</summary>
        public int Fps { get; private set; }

        /// <summary>Longest frame in the last full window, in whole milliseconds (rounded up).</summary>
        public int WorstFrameMs { get; private set; }

        /// <summary>
        /// Adds one frame of <paramref name="deltaSeconds"/> (unscaled). Returns true when this frame completed a
        /// window, so <see cref="Fps"/> and <see cref="WorstFrameMs"/> just changed.
        /// </summary>
        public bool AddFrame(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return false;
            _elapsed += deltaSeconds;
            _frames++;
            if (deltaSeconds > _worst) _worst = deltaSeconds;
            if (_elapsed < _window) return false;

            Fps = (int)Math.Round(_frames / _elapsed);
            WorstFrameMs = (int)Math.Ceiling(_worst * 1000f - 0.001f);
            _elapsed = 0f;
            _frames = 0;
            _worst = 0f;
            return true;
        }
    }
}
