using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// Every level in the game, in play order: <c>Assets/Levels/LevelList.asset</c>. Generated, never edited by hand:
    /// the editor's <c>LevelListSync</c> rebuilds it from the level files under <c>Assets/Levels</c> (play order from
    /// <c>Game.Levels.LevelOrder</c>) whenever one is added, changed or removed, so a new level file is in the game,
    /// the level select and the progress save as soon as Unity imports it.
    /// </summary>
    [CreateAssetMenu(menuName = "Bazaar Sort/Level List", fileName = "LevelList")]
    public sealed class LevelList : ScriptableObject
    {
        [SerializeField] private TextAsset[] _levels = new TextAsset[0];

        /// <summary>The level files in play order. Do not modify the array.</summary>
        public TextAsset[] Levels => _levels;

        /// <summary>Replaces the list (editor sync and tests only).</summary>
        public void Assign(TextAsset[] levels)
        {
            _levels = levels ?? new TextAsset[0];
        }
    }
}
