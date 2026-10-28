using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// The particle burst at a triple clear (issue #21): listens to <see cref="BoardView.ClearShown"/> and emits sparks
    /// in the item's colour plus a few marigold ones from one reused <see cref="ParticleSystem"/>
    /// (<see cref="ParticleFx.CreateBurst"/>). Thin: no rules, no allocation per burst. Lives on its own object (not a
    /// child of the board view, which rebuilds its children on every level).
    /// </summary>
    public sealed class BoardEffects : MonoBehaviour
    {
        /// <summary>Above the tiles and the dragged tile, below the HUD and result panels.</summary>
        public const int SortingOrder = 25;

        private static readonly Color32 Gold = new Color32(0xFF, 0xB3, 0x00, 0xFF);

        [SerializeField] private BoardView _view;

        private BoardView _subscribed;
        private ParticleSystem _burst;
        private int _bursts;

        /// <summary>Wires the view from code (scene builder, tests).</summary>
        public void Configure(BoardView view)
        {
            Unsubscribe();
            _view = view;
            if (isActiveAndEnabled) Subscribe();
        }

        /// <summary>Bursts played since this component started (tests).</summary>
        internal int BurstCount => _bursts;

        internal ParticleSystem Burst => _burst;

        private void OnEnable() => Subscribe();

        private void Start()
        {
            // Made up front so the first clear does not pay for it.
            if (_burst == null) _burst = ParticleFx.CreateBurst("Clear Burst", SortingOrder);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            if (_burst != null) Destroy(_burst.gameObject);
        }

        private void Subscribe()
        {
            Unsubscribe();
            _subscribed = _view;
            if (_subscribed != null) _subscribed.ClearShown += OnClearShown;
        }

        private void Unsubscribe()
        {
            if (_subscribed != null) _subscribed.ClearShown -= OnClearShown;
            _subscribed = null;
        }

        private void OnClearShown(ClearBurst clear)
        {
            if (!clear.Animated || _view.Layout == null) return;
            if (_burst == null) _burst = ParticleFx.CreateBurst("Clear Burst", SortingOrder);

            // Sizes follow the tile, so the burst looks the same on every screen. World units are pixels / 100.
            float tile = _view.Layout.TileSize / BoardView.PixelsPerUnit;
            var main = _burst.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f * tile, 0.22f * tile);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * tile, 3.2f * tile);
            var shape = _burst.shape;
            shape.radius = 0.35f * tile;

            var emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(clear.Pixel.x / BoardView.PixelsPerUnit, clear.Pixel.y / BoardView.PixelsPerUnit, 0f),
                applyShapeToPosition = true,
                startColor = PlaceholderPalette.Get(clear.ItemId).Color,
            };
            _burst.Emit(emit, ParticleFx.BurstItemSparks);
            emit.startColor = Gold;
            _burst.Emit(emit, ParticleFx.BurstGoldSparks);
            _bursts++;
        }
    }
}
