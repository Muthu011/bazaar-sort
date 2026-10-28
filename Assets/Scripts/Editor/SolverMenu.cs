using Game.Solver;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Editor menu for the level solver (issue #20). The work is in the Game.Solver assembly.</summary>
    public static class SolverMenu
    {
        [MenuItem("Bazaar Sort/Run Solver on All Levels")]
        public static void RunOnAllLevels()
        {
            var results = SolverRunner.RunOnAll(SolverRunner.LevelsRoot, true);
            AssetDatabase.Refresh();
            Debug.Log(SolverRunner.Summary(results));
        }
    }
}
