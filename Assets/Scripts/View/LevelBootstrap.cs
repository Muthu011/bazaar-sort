using System;
using Game.Levels;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Scene entry point for the placeholder board: loads levels from an ordered list (World 1: w1_l001 to w1_l005)
    /// through <see cref="LevelLoader"/> and hands each board to a <see cref="BoardView"/>. <see cref="Retry"/> reloads
    /// the current level, <see cref="Next"/> loads the next one (<see cref="LevelSequence"/>; it never loops) and
    /// <see cref="ReplayFromFirst"/> loads the first one. Re-lays out the board when the screen size or safe area changes.
    /// </summary>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset[] _levels;
        [SerializeField] private int _startIndex;
        [SerializeField] private BoardView _view;
        [SerializeField] private Camera _camera;

        private int _current = -1;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        /// <summary>Raised after a level is loaded and bound to the view, with its index in the list.</summary>
        public event Action<int> LevelLoaded;

        /// <summary>Index of the level being played, or -1 before the first load.</summary>
        public int CurrentIndex => _current;

        public int LevelCount => _levels == null ? 0 : _levels.Length;

        /// <summary>True when a level follows the current one; false on the last level (or with no levels).</summary>
        public bool HasNext => LevelCount > 0 && LevelSequence.HasNext(_current, LevelCount);

        /// <summary>Name of the level asset being played, or null.</summary>
        public string CurrentLevelName => _current < 0 ? null : _levels[_current].name;

        /// <summary>Wires a single level from code (tests).</summary>
        public void Configure(TextAsset level, BoardView view, Camera viewCamera)
        {
            Configure(new[] { level }, view, viewCamera);
        }

        /// <summary>Wires the ordered level list from code (scene builder, tests).</summary>
        public void Configure(TextAsset[] levels, BoardView view, Camera viewCamera, int startIndex = 0)
        {
            _levels = levels;
            _view = view;
            _camera = viewCamera;
            _startIndex = startIndex;
        }

        private void Start()
        {
            if (_levels == null || _levels.Length == 0 || _view == null || _camera == null)
            {
                Debug.LogError("LevelBootstrap: levels, view and camera must all be set.", this);
                return;
            }
            LoadLevel(Mathf.Clamp(_startIndex, 0, _levels.Length - 1));
        }

        /// <summary>Loads level <paramref name="index"/> of the list. Returns false (and logs) when it fails to load.</summary>
        public bool LoadLevel(int index)
        {
            if (_levels == null || index < 0 || index >= _levels.Length || _levels[index] == null)
            {
                Debug.LogError($"LevelBootstrap: no level at index {index}.", this);
                return false;
            }

            var level = _levels[index];
            var result = LevelLoader.Load(level.text, level.name);
            if (!result.Success)
            {
                foreach (var error in result.Errors) Debug.LogError(error.Message, this);
                return false;
            }

            _current = index;
            _lastSafeArea = Screen.safeArea;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _view.Bind(result.Board, _camera, _lastSafeArea, BoardLayoutSettings.Default);
            LevelLoaded?.Invoke(index);
            return true;
        }

        /// <summary>Reloads the current level from its file: the start state again.</summary>
        public void Retry()
        {
            if (_current >= 0) LoadLevel(_current);
        }

        /// <summary>
        /// Loads the next level in the list. After the last level there is none: returns false and loads nothing
        /// (Next never loops; the player gets "More levels coming soon" and <see cref="ReplayFromFirst"/>).
        /// </summary>
        public bool Next()
        {
            if (LevelCount == 0 || !LevelSequence.TryGetNext(_current, LevelCount, out int next)) return false;
            return LoadLevel(next);
        }

        /// <summary>Loads the first level of the list in its start state (Replay on "More levels coming soon").</summary>
        public bool ReplayFromFirst()
        {
            if (LevelCount == 0) return false;
            return LoadLevel(LevelSequence.First);
        }

        private void Update()
        {
            if (_view == null || _view.Board == null) return;
            var safeArea = Screen.safeArea;
            if (safeArea == _lastSafeArea && Screen.width == _lastWidth && Screen.height == _lastHeight) return;

            _lastSafeArea = safeArea;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _view.Relayout(safeArea);
        }
    }
}
