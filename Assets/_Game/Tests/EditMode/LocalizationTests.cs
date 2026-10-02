using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MergeLegion.UI;
using NUnit.Framework;
using UnityEngine;

namespace MergeLegion.Tests
{
    public class LocalizationTests
    {
        [Test]
        public void EveryLiteralLocKeyInCode_ExistsInEnglishTable()
        {
            Loc.Reset();
            Loc.Load("en");
            var pattern = new Regex("Loc\\.(?:Get|Format)\\(\\s*\"([^\"]+)\"");
            var missing = new List<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "_Game", "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in pattern.Matches(File.ReadAllText(file)))
                {
                    string key = m.Groups[1].Value;
                    if (Loc.Get(key) == "#" + key) missing.Add(key + " (" + Path.GetFileName(file) + ")");
                }
            }
            Loc.Reset();
            Assert.AreEqual(0, missing.Count, "Missing keys: " + string.Join(", ", missing));
        }

        [Test]
        public void DataDrivenKeys_FromBalanceCsv_Exist()
        {
            Loc.Reset();
            Loc.Load("en");
            var db = Data.GameDatabase.BuildFromCsv();
            foreach (var line in db.lines)
            {
                Assert.AreNotEqual("#" + line.nameKey, Loc.Get(line.nameKey), line.nameKey);
                Assert.AreNotEqual("#" + line.roleKey, Loc.Get(line.roleKey), line.roleKey);
            }
            Loc.Reset();
        }
    }
}
