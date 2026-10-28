using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// Vibrates through Android's Vibrator and VibrationEffect.createOneShot via JNI (no plugin). minSdk is 26, so
    /// VibrationEffect always exists. The two effects are made once; a pulse is one JNI call. Any JNI failure turns
    /// the device off for the session (haptics are never worth a crash). Needs the VIBRATE permission, which the
    /// editor build hook <c>AndroidVibratePermission</c> adds to the manifest.
    /// </summary>
    public sealed class AndroidHapticDevice : IHapticDevice
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private object[] _lightArgs;
        private object[] _wrongArgs;
        private bool _ok;

        public AndroidHapticDevice()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                if (_vibrator == null || !_vibrator.Call<bool>("hasVibrator")) return;
                bool amplitude = _vibrator.Call<bool>("hasAmplitudeControl");
                using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    _lightArgs = new object[] { Create(effect, HapticPulse.Light, amplitude) };
                    _wrongArgs = new object[] { Create(effect, HapticPulse.WrongMove, amplitude) };
                }
                _ok = true;
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("Haptics unavailable: " + e.Message);
            }
        }

        private static AndroidJavaObject Create(AndroidJavaClass effect, HapticPulse pulse, bool amplitudeControl)
        {
            // VibrationEffect.DEFAULT_AMPLITUDE is -1.
            int amp = amplitudeControl ? HapticPulses.Amplitude(pulse) : -1;
            return effect.CallStatic<AndroidJavaObject>("createOneShot", HapticPulses.DurationMs(pulse), amp);
        }

        public void Play(HapticPulse pulse)
        {
            if (!_ok) return;
            try
            {
                _vibrator.Call("vibrate", pulse == HapticPulse.WrongMove ? _wrongArgs : _lightArgs);
            }
            catch (AndroidJavaException e)
            {
                _ok = false;
                Debug.LogWarning("Haptics stopped: " + e.Message);
            }
        }
#else
        public void Play(HapticPulse pulse) { }
#endif
    }
}
