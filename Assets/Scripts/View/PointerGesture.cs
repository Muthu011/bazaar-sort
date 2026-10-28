using Game.Core;
using UnityEngine;

namespace Game.View
{
    /// <summary>How a dragged tile should go back when the drag ends.</summary>
    public enum DropFeedback
    {
        /// <summary>The move happened; the board redraw shows the item in its new slot. Reset the tile at once.</summary>
        Landed,
        /// <summary>Cancelled (own shelf, off the board, pointer lost): glide back, no shake.</summary>
        ReturnHome,
        /// <summary>The board refused the move: glide back, then a small shake.</summary>
        SnapBackWithShake,
    }

    /// <summary>
    /// What <see cref="PointerGesture"/> needs from the board view. <see cref="BoardView"/> implements it; Edit Mode
    /// tests use a fake.
    /// </summary>
    public interface IBoardInputView
    {
        /// <summary>Current layout (pixels, same space as pointer positions), or null when nothing is bound.</summary>
        BoardLayout Layout { get; }

        void ShowSelection(int shelf, int slot);
        void ClearSelection();
        void BeginDrag(int shelf, int slot, Vector2 screenPosition);
        void DragTo(Vector2 screenPosition);
        void EndDrag(int shelf, int slot, DropFeedback feedback);

        /// <summary>Shakes the tile in place (a refused tap move).</summary>
        void Shake(int shelf, int slot);
    }

    /// <summary>
    /// Turns raw pointer down / move / up into taps and drags, hit-tests them with <see cref="BoardLayout.HitTest"/>,
    /// feeds <see cref="MoveInput"/>, and tells the view what to show. No device access (see <see cref="BoardInput"/>)
    /// and no rules, so it is tested in Edit Mode.
    ///
    /// A press becomes a drag once the pointer travels more than <see cref="DragThreshold"/> from where it went down,
    /// and only if it went down on an item. Any other release is a tap at the press position.
    /// </summary>
    public sealed class PointerGesture
    {
        /// <summary>Drag starts after this many pixels, or <see cref="DragThresholdTiles"/> of a tile if larger.</summary>
        public const float MinDragThresholdPx = 10f;

        public const float DragThresholdTiles = 0.2f;

        private readonly MoveInput _input;
        private readonly IBoardInputView _view;
        private bool _pressed;
        private bool _dragging;
        private Vector2 _downPosition;
        private BoardTarget _downTarget;

        public PointerGesture(MoveInput input, IBoardInputView view)
        {
            _input = input ?? throw new System.ArgumentNullException(nameof(input));
            _view = view ?? throw new System.ArgumentNullException(nameof(view));
        }

        public MoveInput Input => _input;

        /// <summary>True between a pointer down and up.</summary>
        public bool IsPressed => _pressed;

        /// <summary>The outcome of the last tap or drop (for tests and later analytics).</summary>
        public InputOutcome LastOutcome { get; private set; } = InputOutcome.Ignored;

        /// <summary>Pixels the pointer must travel before a press on an item becomes a drag.</summary>
        public float DragThreshold
        {
            get
            {
                var layout = _view.Layout;
                return layout == null ? MinDragThresholdPx : Mathf.Max(MinDragThresholdPx, layout.TileSize * DragThresholdTiles);
            }
        }

        public void PointerDown(Vector2 position)
        {
            var layout = _view.Layout;
            if (layout == null) return;

            _pressed = true;
            _dragging = false;
            _downPosition = position;
            _downTarget = layout.HitTest(position);
        }

        public void PointerMove(Vector2 position)
        {
            if (!_pressed) return;

            if (_dragging)
            {
                _view.DragTo(position);
                return;
            }

            if ((position - _downPosition).sqrMagnitude <= DragThreshold * DragThreshold) return;

            bool hadSelection = _input.HasSelection;
            var outcome = _input.BeginDrag(_downTarget);
            if (outcome.Kind != InputOutcomeKind.DragStarted) return;

            if (hadSelection) _view.ClearSelection();
            _dragging = true;
            _view.BeginDrag(outcome.Shelf, outcome.Slot, position);
        }

        public void PointerUp(Vector2 position)
        {
            if (!_pressed) return;
            _pressed = false;

            var layout = _view.Layout;
            if (_dragging)
            {
                _dragging = false;
                _view.DragTo(position);
                var target = layout == null ? BoardTarget.None : layout.HitTest(position);
                var drop = _input.Drop(target);
                LastOutcome = drop;
                _view.EndDrag(drop.Shelf, drop.Slot, FeedbackFor(drop.Kind));
                return;
            }

            var tap = _input.Tap(_downTarget);
            LastOutcome = tap;
            switch (tap.Kind)
            {
                case InputOutcomeKind.Selected:
                    _view.ShowSelection(tap.Shelf, tap.Slot);
                    break;
                case InputOutcomeKind.Deselected:
                case InputOutcomeKind.Moved:
                    _view.ClearSelection();
                    break;
                case InputOutcomeKind.Rejected:
                    _view.ClearSelection();
                    _view.Shake(tap.Shelf, tap.Slot);
                    break;
            }
        }

        /// <summary>The pointer was lost (touch cancelled, focus lost). A drag goes home; a pending tap is dropped.</summary>
        public void PointerCancel()
        {
            if (!_pressed) return;
            _pressed = false;
            if (!_dragging) return;

            _dragging = false;
            var outcome = _input.CancelDrag();
            LastOutcome = outcome;
            _view.EndDrag(outcome.Shelf, outcome.Slot, DropFeedback.ReturnHome);
        }

        private static DropFeedback FeedbackFor(InputOutcomeKind kind)
        {
            switch (kind)
            {
                case InputOutcomeKind.Moved: return DropFeedback.Landed;
                case InputOutcomeKind.Rejected: return DropFeedback.SnapBackWithShake;
                default: return DropFeedback.ReturnHome;
            }
        }
    }
}
