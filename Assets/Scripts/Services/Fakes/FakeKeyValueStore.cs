using System.Collections.Generic;

namespace Game.Services
{
    /// <summary>In-memory <see cref="IKeyValueStore"/> for tests. Counts saves.</summary>
    public sealed class FakeKeyValueStore : IKeyValueStore
    {
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();

        public int SaveCount { get; private set; }

        public bool HasKey(string key) => _ints.ContainsKey(key);
        public int GetInt(string key, int defaultValue) => _ints.TryGetValue(key, out int v) ? v : defaultValue;
        public void SetInt(string key, int value) => _ints[key] = value;
        public void Save() => SaveCount++;
    }
}
