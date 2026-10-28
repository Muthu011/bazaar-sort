using DG.Tweening;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Sets DOTween up once per play session (issue #21), before the first scene loads: recycled tweens, safe mode,
    /// errors-only logging, and pools sized for the busiest scene so DOTween never grows its lists mid-game (which
    /// would allocate). DOTween drives UI feel only (buttons, coin, counter, stars); board tiles keep their own
    /// allocation-free clock in <see cref="BoardView.AdvanceAnimations"/>. Never used in Game.Core or Game.Levels.
    /// </summary>
    public static class Tweens
    {
        /// <summary>
        /// Busiest scene, tweeners: 4 result-panel buttons + 1 HUD toggle (1 each), 6 coins (1 each), counter punch (1),
        /// star drop (3 stars x 3) = 21. Doubled for headroom.
        /// </summary>
        public const int TweenersCapacity = 48;

        /// <summary>The star-drop sequence, with headroom.</summary>
        public const int SequencesCapacity = 4;

        private static bool _ready;

        /// <summary>True once <see cref="Init"/> has run in this play session.</summary>
        public static bool IsReady => _ready && Application.isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession() => _ready = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Init()
        {
            if (_ready || !Application.isPlaying) return;
            DOTween.Init(recycleAllByDefault: true, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(TweenersCapacity, SequencesCapacity);
            _ready = true;
        }
    }
}
