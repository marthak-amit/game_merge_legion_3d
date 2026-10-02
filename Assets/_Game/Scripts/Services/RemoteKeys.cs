namespace MergeLegion.Services
{
    /// <summary>Remote Config keys. Defaults live in Resources/RemoteConfigDefaults.json, never in code.</summary>
    public static class RemoteKeys
    {
        public const string SaveAutosaveSeconds = "save_autosave_seconds";
        public const string BootTimeoutSeconds = "boot_timeout_seconds";
        /// <summary>JSON merged over game_config.json (partial overrides allowed).</summary>
        public const string ConfigOverride = "config_override";
    }
}
