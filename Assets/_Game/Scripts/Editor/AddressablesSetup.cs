using UnityEditor;

namespace MergeLegion.Editor
{
    /// <summary>
    /// Toggles the MERGELEGION_ADDRESSABLES scripting define. With it on, ArenaThemeBuilder loads an optional
    /// theme prefab from Addressables (key = ThemeData.addressKey) instead of the procedural props.
    /// Mark theme prefabs as Addressable with the addresses theme_grasslands, theme_desert, ...
    /// </summary>
    public static class AddressablesSetup
    {
        private const string Define = "MERGELEGION_ADDRESSABLES";

        [MenuItem("Tools/Merge Legion/Addressables Themes/Enable")]
        public static void Enable() => SetDefine(true);

        [MenuItem("Tools/Merge Legion/Addressables Themes/Disable")]
        public static void Disable() => SetDefine(false);

        private static void SetDefine(bool on) => ScriptingDefines.Set(Define, on);
    }
}
