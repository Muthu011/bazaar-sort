using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.View
{
    /// <summary>
    /// Reads the current pointer (touch on the phone, mouse in the editor) through the Input System and forwards
    /// down / move / up to a <see cref="PointerGesture"/> for the board that <see cref="BoardView"/> is drawing.
    /// Thin by design: no hit testing, no rules. Rebinds itself when the view gets a new board.
    ///
    /// Positions are screen pixels; the view's camera fills the screen, so they match the layout's pixel space.
    /// Input stays live during clear and reveal animations (they are visual only); it stops once the board is won or
    /// stuck (<see cref="BoardView.InputLocked"/>).
    /// Update allocates nothing.
    /// </summary>
    public sealed class BoardInput : MonoBehaviour
    {
        [SerializeField] private BoardView _view;

        private Board _boundBoard;
        private Board _gateBoard;
        private IMoveGate _gate;
        private PointerGesture _gesture;
        private bool _tracking;

        /// <summary>Wires the view from code (scene builder, tests).</summary>
        public void Configure(BoardView view)
        {
            _view = view;
        }

        /// <summary>
        /// Limits input on <paramref name="board"/> to what <paramref name="gate"/> allows (a tutorial step, issue #57);
        /// null removes the limit. Applies now if that board is bound, or as soon as it is. Another board is never gated.
        /// </summary>
        public void SetGate(Board board, IMoveGate gate)
        {
            _gateBoard = gate == null ? null : board;
            _gate = gate;
            if (_gesture != null) _gesture.Input.Gate = GateFor(_boundBoard);
        }

        /// <summary>The gesture driver for the bound board, or null.</summary>
        internal PointerGesture Gesture => _gesture;

        private void Update()
        {
            if (_view == null) return;

            var board = _view.Board;
            if (board != _boundBoard) Rebind(board);
            if (_gesture == null) return;

            // Won or stuck: the result panel takes the pointer, so a tap on its buttons never reaches the board.
            if (_view.InputLocked)
            {
                Cancel();
                return;
            }

            var pointer = Pointer.current;
            if (pointer == null)
            {
                Cancel();
                return;
            }

            var press = pointer.press;
            bool pressed = press.isPressed;
            Vector2 position = pointer.position.ReadValue();

            // Track the press ourselves as well as the per-frame flags, so a press and release inside one frame
            // still counts as a tap, and a press that began before this component saw it still starts a gesture.
            if (!_tracking && (pressed || press.wasPressedThisFrame))
            {
                _tracking = true;
                _gesture.PointerDown(position);
            }

            if (!_tracking) return;

            if (pressed) _gesture.PointerMove(position);
            else
            {
                _tracking = false;
                _gesture.PointerUp(position);
            }
        }

        private void Rebind(Board board)
        {
            Cancel();
            _boundBoard = board;
            _gesture = board == null ? null : new PointerGesture(new MoveInput(board) { Gate = GateFor(board) }, _view);
        }

        private IMoveGate GateFor(Board board) => board != null && board == _gateBoard ? _gate : null;

        private void Cancel()
        {
            if (!_tracking) return;
            _tracking = false;
            _gesture?.PointerCancel();
        }

        private void OnDisable() => Cancel();

        private void OnApplicationPause(bool paused)
        {
            if (paused) Cancel();
        }
    }
}
