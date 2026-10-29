using System;
using Game.Core;
using Game.Levels;
using Game.Services;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Scene entry point for the placeholder board: loads levels from an ordered list (the generated
    /// <see cref="LevelList"/> asset in the game scene, or an array wired from code) through <see cref="LevelLoader"/>
    /// and hands each board to a <see cref="BoardView"/>. <see cref="Retry"/> reloads the current level, <see cref="Next"/>
    /// loads the next one (<see cref="LevelSequence"/>; it never loops) and <see cref="ReplayFromFirst"/> loads the first one.
    /// Re-lays out the board when the screen size or safe area changes.
    ///
    /// Progress (issue #22), when <see cref="Progress"/> is set (the game scene uses <see cref="GameServices.Progress"/>):
    /// every win is saved the moment Core raises Won (<see cref="ProgressRules.RecordWin"/>), and on start the player
    /// continues at the first level never won (<see cref="ProgressRules.Continue"/>). With every level won, the last
    /// level is loaded with its input held and <see cref="AllLevelsCleared"/> is raised, so the result panels show
    /// "More levels coming soon" with Replay. Level ids are the level file names (w1_l001...). Without progress (tests
    /// that wire levels from code), it starts at the configured index and saves nothing.
    ///
    /// Stars and coins (issue #44): the bootstrap counts the moves of the level being played (every
    /// <see cref="Board.Moved"/>; a drop back on the same shelf is a cancel and never raises it) from each load, so a
    /// Retry starts again at 0. On Won the stars come from <see cref="WinRewards.StarsFor"/> against the level's
    /// <c>solver.minMoves</c> and are kept in <see cref="LastWinStars"/> for the Win card, with or without progress;
    /// with progress the win, its best stars and its coins are saved in one write.
    /// </summary>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelList _levelList;
        [SerializeField] private TextAsset[] _levels;
        [SerializeField] private int _startIndex;
        [SerializeField] private BoardView _view;
        [SerializeField] private Camera _camera;
        [SerializeField] private bool _useSavedProgress;

        private IProgressService _progress;
        private string[] _ids;
        private Board _board;
        private LevelData _level;
        private int _current = -1;
        private int _moves;
        private int _lastWinStars;
        private int _lastWinCoins;
        private bool _awaitingReplay;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        /// <summary>Raised after a level is loaded and bound to the view, with its index in the list.</summary>
        public event Action<int> LevelLoaded;

        /// <summary>
        /// Raised when the game opens with every level already won: the last level is shown, held, under
        /// "More levels coming soon". See <see cref="IsAwaitingReplay"/>.
        /// </summary>
        public event Action AllLevelsCleared;

        /// <summary>The view the levels are drawn in.</summary>
        public BoardView View => _view;

        /// <summary>Index of the level being played, or -1 before the first load.</summary>
        public int CurrentIndex => _current;

        public int LevelCount => Levels == null ? 0 : Levels.Length;

        /// <summary>True when a level follows the current one; false on the last level (or with no levels).</summary>
        public bool HasNext => LevelCount > 0 && LevelSequence.HasNext(_current, LevelCount);

        /// <summary>Parsed data of the level being played (tutorial key, limits, ...), or null before the first load.</summary>
        public LevelData CurrentLevel => _level;

        /// <summary>Moves made since the current level was loaded.</summary>
        public int MovesMade => _moves;

        /// <summary>Stars earned by the last win of the current level (1..3), or 0 when it hasn't been won since loading.</summary>
        public int LastWinStars => _lastWinStars;

        /// <summary>Coins added to the wallet by that win, or 0 (also 0 without saved progress).</summary>
        public int LastWinCoins => _lastWinCoins;

        /// <summary>Name of the level asset being played, or null.</summary>
        public string CurrentLevelName => _current < 0 ? null : Levels[_current].name;

        /// <summary>
        /// True from an all-cleared start until the next level load: "More levels coming soon" should be on screen.
        /// </summary>
        public bool IsAwaitingReplay => _awaitingReplay;

        /// <summary>
        /// Saved progress: set from code (tests), or <see cref="GameServices.Progress"/> when the scene turns saved
        /// progress on. Null means progress is neither read nor saved.
        /// </summary>
        public IProgressService Progress
        {
            get => _progress ?? (_useSavedProgress ? GameServices.Progress : null);
            set => _progress = value;
        }

        /// <summary>Level ids (file names) in play order; the keys of the progress save.</summary>
        public string[] LevelIds
        {
            get
            {
                var levels = Levels;
                if (_ids == null || levels == null || _ids.Length != levels.Length)
                {
                    _ids = new string[levels == null ? 0 : levels.Length];
                    for (int i = 0; i < _ids.Length; i++) _ids[i] = levels[i] == null ? string.Empty : levels[i].name;
                }
                return _ids;
            }
        }

        private TextAsset[] Levels => _levelList != null ? _levelList.Levels : _levels;

        /// <summary>Wires a single level from code (tests).</summary>
        public void Configure(TextAsset level, BoardView view, Camera viewCamera)
        {
            Configure(new[] { level }, view, viewCamera);
        }

        /// <summary>Wires the ordered level list from code (tests). Saved progress stays off unless <see cref="Progress"/> is set.</summary>
        public void Configure(TextAsset[] levels, BoardView view, Camera viewCamera, int startIndex = 0)
        {
            _levelList = null;
            _levels = levels;
            _ids = null;
            _view = view;
            _camera = viewCamera;
            _startIndex = startIndex;
        }

        /// <summary>Wires the generated level list and turns saved progress on (game scene builder).</summary>
        public void Configure(LevelList levels, BoardView view, Camera viewCamera)
        {
            _levelList = levels;
            _levels = null;
            _ids = null;
            _view = view;
            _camera = viewCamera;
            _startIndex = 0;
            _useSavedProgress = true;
        }

        private void Start()
        {
            if (LevelCount == 0 || _view == null || _camera == null)
            {
                Debug.LogError("LevelBootstrap: levels, view and camera must all be set.", this);
                return;
            }

            var progress = Progress;
            if (progress == null)
            {
                LoadLevel(Mathf.Clamp(_startIndex, 0, LevelCount - 1));
                return;
            }

            var point = ProgressRules.Continue(LevelIds, progress);
            if (!LoadLevel(point.Index) || !point.AllCleared) return;
            _view.HoldInput();
            _awaitingReplay = true;
            AllLevelsCleared?.Invoke();
        }

        private void OnDestroy() => Unwatch();

        /// <summary>Loads level <paramref name="index"/> of the list. Returns false (and logs) when it fails to load.</summary>
        public bool LoadLevel(int index)
        {
            var levels = Levels;
            if (levels == null || index < 0 || index >= levels.Length || levels[index] == null)
            {
                Debug.LogError($"LevelBootstrap: no level at index {index}.", this);
                return false;
            }

            var level = levels[index];
            var result = LevelLoader.Load(level.text, level.name);
            if (!result.Success)
            {
                foreach (var error in result.Errors) Debug.LogError(error.Message, this);
                return false;
            }

            Unwatch();
            _board = result.Board;
            _level = result.Level;
            _board.Won += OnWon;
            _board.Moved += OnMoved;
            _moves = 0;
            _lastWinStars = 0;
            _lastWinCoins = 0;
            _current = index;
            _awaitingReplay = false;
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

        private void OnMoved(MoveInfo move) => _moves++;

        private void OnWon()
        {
            if (_current < 0) return;
            _lastWinStars = WinRewards.StarsFor(_moves, _level?.MinMoves ?? 0);
            var progress = Progress;
            if (progress == null) return;
            _lastWinCoins = ProgressRules.RecordWin(LevelIds, progress, _current, _lastWinStars);
        }

        private void Unwatch()
        {
            if (_board != null)
            {
                _board.Won -= OnWon;
                _board.Moved -= OnMoved;
            }
            _board = null;
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
