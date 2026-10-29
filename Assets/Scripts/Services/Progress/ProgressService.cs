using System;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// <see cref="IProgressService"/> over an <see cref="IProgressStore"/>: read once, on first use; written on every
    /// win. An unreadable save, or a save that fails to write, never stops the game: play goes on with what is in memory
    /// and one warning is logged per service (so per app launch), however often it happens.
    /// </summary>
    public sealed class ProgressService : IProgressService
    {
        private readonly IProgressStore _store;
        private readonly Action<string> _warn;
        private ProgressData _data;
        private ProgressLoadStatus _loadStatus;
        private bool _warned;

        /// <param name="warn">Where the single warning goes; <c>Debug.LogWarning</c> when null.</param>
        public ProgressService(IProgressStore store, Action<string> warn = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _warn = warn ?? Debug.LogWarning;
        }

        public event Action Changed;

        /// <summary>How the save was found on first use (tests, dev tools).</summary>
        public ProgressLoadStatus LoadStatus
        {
            get
            {
                EnsureLoaded();
                return _loadStatus;
            }
        }

        public string HighestUnlocked
        {
            get
            {
                EnsureLoaded();
                return _data.highestUnlocked;
            }
        }

        public int GetStars(string levelId)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(levelId)) return 0;
            foreach (var entry in _data.levels)
            {
                if (string.Equals(entry.id, levelId, StringComparison.Ordinal)) return entry.stars;
            }
            return 0;
        }

        public void RecordWin(string levelId, int stars, string highestUnlocked)
        {
            if (string.IsNullOrEmpty(levelId)) throw new ArgumentException("A level id is required.", nameof(levelId));
            EnsureLoaded();
            stars = Mathf.Clamp(stars, 1, ProgressRules.MaxStars);

            LevelStars existing = null;
            foreach (var entry in _data.levels)
            {
                if (string.Equals(entry.id, levelId, StringComparison.Ordinal)) existing = entry;
            }
            if (existing == null) _data.levels.Add(new LevelStars { id = levelId, stars = stars });
            else existing.stars = Math.Max(existing.stars, stars);

            if (!string.IsNullOrEmpty(highestUnlocked)) _data.highestUnlocked = highestUnlocked;
            Save();
            Changed?.Invoke();
        }

        public bool IsTutorialDone(string tutorialKey)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(tutorialKey)) return false;
            foreach (var key in _data.tutorialsDone)
            {
                if (string.Equals(key, tutorialKey, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public void MarkTutorialDone(string tutorialKey)
        {
            if (string.IsNullOrEmpty(tutorialKey)) throw new ArgumentException("A tutorial key is required.", nameof(tutorialKey));
            if (IsTutorialDone(tutorialKey)) return;
            _data.tutorialsDone.Add(tutorialKey);
            Save();
            Changed?.Invoke();
        }

        public void Reset()
        {
            EnsureLoaded();
            _data = new ProgressData();
            Save();
            Changed?.Invoke();
        }

        private void EnsureLoaded()
        {
            if (_data != null) return;
            _loadStatus = _store.Load(out var data, out string problem);
            _data = data ?? new ProgressData();
            if (_loadStatus == ProgressLoadStatus.Unreadable) WarnOnce("Saved progress could not be read (" + problem + "); starting fresh.");
        }

        private void Save()
        {
            if (!_store.Save(_data, out string problem)) WarnOnce("Progress was not saved: " + problem);
        }

        private void WarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            _warn(message);
        }
    }
}
