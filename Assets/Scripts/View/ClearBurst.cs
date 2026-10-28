using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// A triple clear as the view shows it (<see cref="BoardView.ClearShown"/>). Raised exactly once per Core clear:
    /// at the top of the pop when it animates, or at once (<see cref="Animated"/> false) when the clear was skipped
    /// because the player touched the shelf, the screen re-laid out, or the queue overflowed. A struct of references
    /// and values: raising it allocates nothing.
    /// </summary>
    public readonly struct ClearBurst
    {
        public ClearBurst(int shelf, string itemId, Vector2 pixel, bool animated)
        {
            Shelf = shelf;
            ItemId = itemId;
            Pixel = pixel;
            Animated = animated;
        }

        public int Shelf { get; }

        /// <summary>Object id of the cleared triple.</summary>
        public string ItemId { get; }

        /// <summary>Centre of the cleared triple, in screen pixels.</summary>
        public Vector2 Pixel { get; }

        /// <summary>True when the pop was shown (play the burst and fly a coin); false when it was skipped.</summary>
        public bool Animated { get; }
    }
}
