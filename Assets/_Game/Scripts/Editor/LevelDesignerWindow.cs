using System.IO;
using System.Linq;
using MergeLegion.Core;
using MergeLegion.Data;
using MergeLegion.Levels;
using UnityEditor;
using UnityEngine;

namespace MergeLegion.Editor
{
    /// <summary>
    /// Visual enemy formation editor (section 1.4). Pick a chapter file, pick a level, paint units onto the enemy grid,
    /// tune boss/multipliers, save back to JSON. The row nearest the player is drawn at the bottom.
    /// </summary>
    public sealed class LevelDesignerWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/_Game/Resources/Levels";
        private const int GridRows = 8;

        private string[] _files = new string[0];
        private int _fileIndex;
        private ChapterFile _chapter;
        private int _levelIndex;
        private int _brushLine;
        private int _brushLevel = 1;
        private Vector2 _scroll;
        private bool _bossFoldout = true;
        private bool _dirty;
        private GameDatabase _db;

        private static readonly Color[] LineColors =
        {
            new Color(0.85f, 0.3f, 0.25f), new Color(0.3f, 0.75f, 0.35f), new Color(0.3f, 0.5f, 0.95f), new Color(0.75f, 0.4f, 0.9f)
        };
        private static readonly string[] LineNames = { "Melee", "Ranged", "Tank", "Flying" };

        [MenuItem("Tools/Merge Legion/Level Designer")]
        public static void Open() => GetWindow<LevelDesignerWindow>("Level Designer").Show();

        private void OnEnable()
        {
            _db = GameDatabase.Instance;
            RefreshFiles();
            if (_files.Length > 0) Load(0);
        }

        private void RefreshFiles()
        {
            _files = Directory.Exists(LevelsFolder)
                ? Directory.GetFiles(LevelsFolder, "chapter_*.json").OrderBy(f => f).ToArray()
                : new string[0];
        }

        private void Load(int index)
        {
            _fileIndex = Mathf.Clamp(index, 0, _files.Length - 1);
            _chapter = JsonUtility.FromJson<ChapterFile>(File.ReadAllText(_files[_fileIndex]));
            _levelIndex = Mathf.Clamp(_levelIndex, 0, Mathf.Max(0, _chapter.levels.Count - 1));
            _dirty = false;
        }

        private void Save()
        {
            File.WriteAllText(_files[_fileIndex], JsonUtility.ToJson(_chapter, true));
            AssetDatabase.Refresh();
            _dirty = false;
        }

        private LevelDefinition Current => _chapter != null && _chapter.levels.Count > 0 ? _chapter.levels[_levelIndex] : null;

        private void OnGUI()
        {
            if (_files.Length == 0)
            {
                EditorGUILayout.HelpBox("No chapter files in " + LevelsFolder + ". Use Tools > Merge Legion > Generate Campaign.", MessageType.Info);
                if (GUILayout.Button("Refresh")) { RefreshFiles(); if (_files.Length > 0) Load(0); }
                return;
            }

            DrawToolbar();
            var level = Current;
            if (level == null) return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawLevelHeader(level);
            EditorGUILayout.Space();
            DrawPalette();
            DrawGrid(level);
            EditorGUILayout.Space();
            DrawStats(level);
            DrawBoss(level);
            DrawActions(level);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int sel = EditorGUILayout.Popup(_fileIndex, _files.Select(Path.GetFileNameWithoutExtension).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(150));
            if (sel != _fileIndex) Load(sel);
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60))) Save();
            if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(60))) Load(_fileIndex);
            GUILayout.FlexibleSpace();
            GUILayout.Label(_dirty ? "unsaved changes" : "saved", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLevelHeader(LevelDefinition level)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30))) _levelIndex = Mathf.Max(0, _levelIndex - 1);
            _levelIndex = EditorGUILayout.IntSlider("Level", _levelIndex + 1, 1, _chapter.levels.Count) - 1;
            if (GUILayout.Button(">", GUILayout.Width(30))) _levelIndex = Mathf.Min(_chapter.levels.Count - 1, _levelIndex + 1);
            EditorGUILayout.EndHorizontal();
            level = Current;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Campaign level #" + level.id, EditorStyles.boldLabel);
            var themeIds = ThemeLibrary.All().Select(t => t.id).ToArray();
            int ti = Mathf.Max(0, System.Array.IndexOf(themeIds, level.theme));
            level.theme = themeIds[EditorGUILayout.Popup("Theme", ti, themeIds)];
            level.isBoss = EditorGUILayout.Toggle("Boss level", level.isBoss);
            level.hasBoss = level.isBoss && EditorGUILayout.Toggle("Has boss unit", level.hasBoss);
            level.hpMult = EditorGUILayout.FloatField("Enemy HP x", level.hpMult);
            level.dmgMult = EditorGUILayout.FloatField("Enemy damage x", level.dmgMult);
            if (EditorGUI.EndChangeCheck()) _dirty = true;
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("Brush (left click = place, right click = clear)", EditorStyles.boldLabel);
            _brushLine = GUILayout.Toolbar(_brushLine, LineNames);
            _brushLevel = EditorGUILayout.IntSlider("Unit level", _brushLevel, 1, 8);
        }

        private void DrawGrid(LevelDefinition level)
        {
            int cols = Mathf.Max(1, level.enemyCols);
            GUILayout.Label("Enemy side (top = far back, bottom = nearest to your army)", EditorStyles.miniLabel);
            for (int row = GridRows - 1; row >= 0; row--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("r" + row, GUILayout.Width(24));
                for (int col = 0; col < cols; col++)
                {
                    var unit = level.enemies.FirstOrDefault(e => e.col == col && e.row == row);
                    var rect = GUILayoutUtility.GetRect(60, 40, GUILayout.Width(60));
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = unit != null ? LineColors[Mathf.Clamp(unit.line, 0, 3)] : new Color(0.35f, 0.35f, 0.35f);
                    string label = unit != null ? LineNames[Mathf.Clamp(unit.line, 0, 3)][0] + unit.level.ToString() : "";
                    GUI.Box(rect, label, EditorStyles.helpBox);
                    GUI.backgroundColor = old;
                    HandleCellClick(level, rect, col, row, unit);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void HandleCellClick(LevelDefinition level, Rect rect, int col, int row, EnemySpawnDef existing)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || !rect.Contains(e.mousePosition)) return;
            if (existing != null) level.enemies.Remove(existing);
            if (e.button == 0) level.enemies.Add(new EnemySpawnDef { line = _brushLine, level = _brushLevel, col = col, row = row });
            _dirty = true;
            e.Use();
            Repaint();
        }

        private void DrawStats(LevelDefinition level)
        {
            float power = LevelPower.ArmyPower(_db, level.enemies, level.hpMult, level.dmgMult);
            float curve = new CampaignGenParams().startPower * Mathf.Pow(new CampaignGenParams().growth, level.id - 1);
            EditorGUILayout.LabelField("Units", level.enemies.Count + (level.enemies.Count > ProceduralLevelGenerator.MaxEnemyUnits ? "  (too many!)" : ""));
            EditorGUILayout.LabelField("Formation power", power.ToString("F0") + "   (curve target ~" + curve.ToString("F0") + ", " + (power / Mathf.Max(1f, curve) * 100f).ToString("F0") + "%)");
        }

        private void DrawBoss(LevelDefinition level)
        {
            if (!level.hasBoss) return;
            _bossFoldout = EditorGUILayout.Foldout(_bossFoldout, "Boss", true);
            if (!_bossFoldout) return;
            EditorGUI.BeginChangeCheck();
            var b = level.boss;
            b.hp = EditorGUILayout.FloatField("HP", b.hp);
            b.damage = EditorGUILayout.FloatField("Damage", b.damage);
            b.attackSpeed = EditorGUILayout.FloatField("Attack speed", b.attackSpeed);
            b.range = EditorGUILayout.FloatField("Range", b.range);
            b.moveSpeed = EditorGUILayout.FloatField("Move speed", b.moveSpeed);
            b.scale = EditorGUILayout.FloatField("Scale", b.scale);
            b.slamInterval = EditorGUILayout.FloatField("Slam interval (s)", b.slamInterval);
            b.slamRadius = EditorGUILayout.FloatField("Slam radius", b.slamRadius);
            b.slamDamageMult = EditorGUILayout.FloatField("Slam damage x", b.slamDamageMult);
            b.summonInterval = EditorGUILayout.FloatField("Summon interval (s)", b.summonInterval);
            b.summonCount = EditorGUILayout.IntField("Summon count", b.summonCount);
            b.minionLine = EditorGUILayout.Popup("Minion line", b.minionLine, LineNames);
            b.minionLevel = EditorGUILayout.IntSlider("Minion level", b.minionLevel, 1, 8);
            if (EditorGUI.EndChangeCheck()) _dirty = true;
        }

        private void DrawActions(LevelDefinition level)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear"))
            {
                level.enemies.Clear();
                _dirty = true;
            }
            if (GUILayout.Button("Auto-fill to power budget"))
            {
                float budget = new CampaignGenParams().startPower * Mathf.Pow(new CampaignGenParams().growth, level.id - 1);
                level.enemies.Clear();
                var rng = new DeterministicRng(level.id * 17);
                int lines = level.id >= 20 ? 4 : level.id >= 8 ? 3 : 2;
                ProceduralLevelGenerator.FillFormation(level, _db, budget, rng, lines, 40);
                _dirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
