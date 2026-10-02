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
        public void DataDrivenKeys_FromConfigJson_Exist()
        {
            Loc.Reset();
            Loc.Load("en");
            var keys = new List<string>();
            var meta = Meta.MetaConfig.FromJson(Resources.Load<TextAsset>("Config/meta_config").text);
            foreach (var c in meta.chests) keys.Add(c.nameKey);
            foreach (var m in meta.missions.daily) keys.Add(m.titleKey);
            foreach (var m in meta.missions.weekly) keys.Add(m.titleKey);
            foreach (var a in meta.missions.achievements) keys.Add(a.titleKey);
            foreach (var s in meta.spin.segments) keys.Add(s.labelKey);
            var shop = Monetization.MonetizationConfig.FromJson(Resources.Load<TextAsset>("Config/monetization_config").text);
            foreach (var p in shop.products) { keys.Add(p.titleKey); keys.Add(p.descKey); }
            foreach (var o in shop.offers) keys.Add(o.titleKey);
            keys.Add(shop.weekendEvent.nameKey);
            keys.Add(shop.weekendEvent.tokenKey);
            foreach (var t in Data.ThemeLibrary.All()) keys.Add(t.nameKey);
            var db = Data.GameDatabase.BuildFromCsv();
            foreach (var c in db.commanders) { keys.Add(c.nameKey); keys.Add(c.passiveKey); keys.Add(c.skillKey); }

            var missing = new List<string>();
            foreach (var k in keys) if (Loc.Get(k) == "#" + k) missing.Add(k);
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
