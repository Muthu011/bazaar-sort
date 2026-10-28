using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Applies the project-wide player settings in code so they are reviewable.
    /// Run from the menu "Bazaar Sort/Apply Project Settings", via Unity MCP execute_menu_item,
    /// or headless with -executeMethod Game.EditorTools.ProjectSetup.Apply.
    /// </summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "Muthu";
        public const string ProductName = "Bazaar Sort";
        public const string PackageId = "com.muthu.bazaarsort";

        [MenuItem("Bazaar Sort/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, PackageId);

            // Portrait only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                return;
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogWarning("ProjectSetup: Android Build Support module is not installed; active build target left unchanged. Install it via Unity Hub, then run this again.");
                return;
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }
}
