using UnityEngine;

namespace Game.Services
{
    /// <summary><see cref="IKeyValueStore"/> over Unity's PlayerPrefs (SharedPreferences on Android).</summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
