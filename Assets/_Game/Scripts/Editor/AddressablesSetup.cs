using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

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

        private static void SetDefine(bool on)
        {
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                var list = new System.Collections.Generic.List<string>(defines);
                if (on && !list.Contains(Define)) list.Add(Define);
                if (!on) list.Remove(Define);
                PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
            }
            Debug.Log("[MergeLegion] " + Define + (on ? " enabled" : " disabled"));
        }
    }
}
