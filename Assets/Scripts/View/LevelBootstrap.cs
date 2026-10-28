using Game.Levels;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Scene entry point for the placeholder board: loads one level file through <see cref="LevelLoader"/> and hands
    /// the board to a <see cref="BoardView"/>. Re-lays out the board when the screen size or safe area changes.
    /// </summary>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset _level;
        [SerializeField] private BoardView _view;
        [SerializeField] private Camera _camera;

        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        /// <summary>Wires the references from code (scene builder, tests).</summary>
        public void Configure(TextAsset level, BoardView view, Camera viewCamera)
        {
            _level = level;
            _view = view;
            _camera = viewCamera;
        }

        private void Start()
        {
            if (_level == null || _view == null || _camera == null)
            {
                Debug.LogError("LevelBootstrap: level, view and camera must all be set.", this);
                return;
            }

            var result = LevelLoader.Load(_level.text, _level.name);
            if (!result.Success)
            {
                foreach (var error in result.Errors) Debug.LogError(error.Message, this);
                return;
            }

            _lastSafeArea = Screen.safeArea;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _view.Bind(result.Board, _camera, _lastSafeArea, BoardLayoutSettings.Default);
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
