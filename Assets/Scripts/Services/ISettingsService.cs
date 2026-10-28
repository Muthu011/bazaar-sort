using System;

namespace Game.Services
{
    /// <summary>Player settings that persist between sessions. Only haptics for now.</summary>
    public interface ISettingsService
    {
        /// <summary>Light vibration on wrong moves and clears. On by default.</summary>
        bool HapticsEnabled { get; set; }

        /// <summary>Raised after any setting changes value.</summary>
        event Action Changed;
    }
}
