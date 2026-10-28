namespace Game.Services
{
    /// <summary>
    /// Small persistent key-value store for settings. The game uses <see cref="PlayerPrefsStore"/>; tests use
    /// <see cref="FakeKeyValueStore"/> so they never touch the real PlayerPrefs.
    /// </summary>
    public interface IKeyValueStore
    {
        bool HasKey(string key);
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);

        /// <summary>Writes pending changes to disk.</summary>
        void Save();
    }
}
