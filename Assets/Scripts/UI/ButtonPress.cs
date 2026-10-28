using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Presses a button down to <see cref="UiFeel.PressedScale"/> while the pointer holds it and springs it back on
    /// release (issue #21). One DOTween tweener per button, built once and never killed: press plays it forwards,
    /// release plays it backwards, so a tap allocates nothing. Visual only: the Button's onClick is untouched. Runs on
    /// unscaled time so it works whatever the game's time scale.
    /// </summary>
    public sealed class ButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Tween _press;
        private bool _down;

        /// <summary>True while the pointer holds the button.</summary>
        public bool IsPressed => _down;

        public void OnPointerDown(PointerEventData eventData)
        {
            _down = true;
            var tween = Ensure();
            if (tween != null) tween.PlayForward();
            else transform.localScale = new Vector3(UiFeel.PressedScale, UiFeel.PressedScale, 1f);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_down) Release();
        }

        private void Release()
        {
            _down = false;
            var tween = Ensure();
            if (tween != null) tween.PlayBackwards();
            else transform.localScale = Vector3.one;
        }

        /// <summary>Snaps back to rest at once (panel hidden).</summary>
        public void ResetPress()
        {
            _down = false;
            if (_press != null) _press.Rewind();
            transform.localScale = Vector3.one;
        }

        private void OnDisable() => ResetPress();

        private Tween Ensure()
        {
            if (_press != null || !Game.View.Tweens.IsReady) return _press;
            _press = transform.DOScale(new Vector3(UiFeel.PressedScale, UiFeel.PressedScale, 1f), UiFeel.PressSeconds)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetAutoKill(false)
                .SetRecyclable(false)
                .SetLink(gameObject)
                .Pause();
            return _press;
        }

        private void OnDestroy()
        {
            if (_press != null && _press.IsActive()) _press.Kill();
            _press = null;
        }
    }
}
