using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Asks for 60 fps before the first scene loads. Android ignores <see cref="QualitySettings.vSyncCount"/> and paces
    /// to <see cref="Application.targetFrameRate"/> (with Optimized Frame Pacing on, frames line up with the display),
    /// so on the phone vSync is turned off and the target set to 60. In the editor only the target is set, so play
    /// mode never changes the project's quality settings.
    /// </summary>
    public static class FrameRate
    {
        public const int Target = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
#if !UNITY_EDITOR
            QualitySettings.vSyncCount = 0;
#endif
            Application.targetFrameRate = Target;
        }
    }
}
