using System.Collections.Generic;
using UnityEngine;

namespace MergeLegion.Data
{
    /// <summary>
    /// Root of all static game data. Prefers the baked asset (Resources/GameDatabase, created by
    /// Tools > Merge Legion > Import Balance); falls back to building from the CSVs so the game runs without editor steps.
    /// </summary>
    public sealed class GameDatabase : ScriptableObject
    {
        public List<UnitLineData> lines = new List<UnitLineData>();
        public List<CommanderData> commanders = new List<CommanderData>();

        private static GameDatabase _instance;

        public static GameDatabase Instance
        {
            get
            {
                if (_instance == null) _instance = Load();
                return _instance;
            }
        }

        public static void SetInstance(GameDatabase db) => _instance = db;

        public UnitLineData GetLine(UnitLineId id)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].id == id) return lines[i];
            return null;
        }

        public CommanderData GetCommander(string id)
        {
            for (int i = 0; i < commanders.Count; i++)
                if (commanders[i].id == id) return commanders[i];
            return null;
        }

        private static GameDatabase Load()
        {
            var baked = Resources.Load<GameDatabase>("GameDatabase");
            if (baked != null && baked.lines.Count > 0)
            {
                if (baked.commanders.Count == 0) baked.commanders = BuildFromCsv().commanders;
                return baked;
            }
            return BuildFromCsv();
        }

        public static GameDatabase BuildFromCsv()
        {
            var db = CreateInstance<GameDatabase>();
            var linesCsv = Resources.Load<TextAsset>("Balance/lines");
            var unitsCsv = Resources.Load<TextAsset>("Balance/units");
            db.lines = BalanceBuilder.BuildLines(linesCsv != null ? linesCsv.text : "", unitsCsv != null ? unitsCsv.text : "");
            var cmdCsv = Resources.Load<TextAsset>("Balance/commanders");
            db.commanders = BalanceBuilder.BuildCommanders(cmdCsv != null ? cmdCsv.text : "");
            return db;
        }
    }
}
