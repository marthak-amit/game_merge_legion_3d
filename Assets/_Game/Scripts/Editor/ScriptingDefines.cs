using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MergeLegion.Editor
{
    internal static class ScriptingDefines
    {
        public static void Set(string define, bool on)
        {
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                var list = new List<string>(defines);
                if (on && !list.Contains(define)) list.Add(define);
                if (!on) list.Remove(define);
                PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
            }
            Debug.Log("[MergeLegion] " + define + (on ? " enabled" : " disabled"));
        }

        public static bool Has(string define)
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out string[] defines);
            return new List<string>(defines).Contains(define);
        }
    }
}
