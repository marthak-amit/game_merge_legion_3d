using System.IO;
using MergeLegion.Data;
using MergeLegion.Levels;
using UnityEditor;
using UnityEngine;

namespace MergeLegion.Editor
{
    public static class CampaignTools
    {
        [MenuItem("Tools/Merge Legion/Regenerate Campaign Levels (overwrites JSON!)")]
        public static void Regenerate()
        {
            if (!EditorUtility.DisplayDialog("Regenerate campaign",
                    "This overwrites Assets/_Game/Resources/Levels/chapter_XX.json with the generated power curve. Hand edits are lost.", "Overwrite", "Cancel")) return;

            var files = CampaignGenerator.GenerateAll(GameDatabase.Instance, new CampaignGenParams());
            Directory.CreateDirectory("Assets/_Game/Resources/Levels");
            foreach (var f in files)
                File.WriteAllText($"Assets/_Game/Resources/Levels/chapter_{f.chapter:00}.json", JsonUtility.ToJson(f, true));
            AssetDatabase.Refresh();
            Debug.Log("[Campaign] Regenerated " + files.Length + " chapters.");
        }
    }
}
