namespace Game.UI
{
    /// <summary>
    /// The guard for development-only tools (level select, fps readout), fed <c>Debug.isDebugBuild</c>: true in the
    /// editor and in development players, false in release players. Unity 6.6 deprecates the DEVELOPMENT_BUILD
    /// compile symbol (warning UAC0009) and points to this runtime check instead, so <c>DevOverlay</c> is compiled
    /// everywhere but a release player never creates it (it is only ever made by <c>DevOverlay.TryInstall</c>, which
    /// asks this guard, and it removes itself if a release player ever finds it in a scene). Pure, Edit Mode tested.
    /// </summary>
    public static class DevBuild
    {
        /// <summary>The dev level select is shown only in a development build (or the editor).</summary>
        public static bool IsLevelSelectEnabled(bool isDevBuild) => isDevBuild;

        /// <summary>The dev fps readout follows the same rule as the level select.</summary>
        public static bool IsFpsReadoutEnabled(bool isDevBuild) => isDevBuild;
    }
}
