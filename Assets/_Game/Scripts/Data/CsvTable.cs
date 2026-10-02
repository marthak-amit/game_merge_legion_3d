using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MergeLegion.Data
{
    /// <summary>Minimal CSV reader (header row, quoted cells, # comments). Used for the balance sheet.</summary>
    public sealed class CsvTable
    {
        private readonly Dictionary<string, int> _columns = new Dictionary<string, int>();
        private readonly List<string[]> _rows = new List<string[]>();

        public int RowCount => _rows.Count;

        public static CsvTable Parse(string text)
        {
            var table = new CsvTable();
            if (string.IsNullOrEmpty(text)) return table;

            bool header = true;
            foreach (var line in SplitLines(text))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var cells = SplitCells(line);
                if (header)
                {
                    for (int i = 0; i < cells.Count; i++) table._columns[cells[i].Trim().ToLowerInvariant()] = i;
                    header = false;
                }
                else table._rows.Add(cells.ToArray());
            }
            return table;
        }

        public bool Has(string column) => _columns.ContainsKey(column);

        public string Str(int row, string column, string fallback = "")
        {
            if (!_columns.TryGetValue(column, out int c)) return fallback;
            var r = _rows[row];
            return c < r.Length ? r[c].Trim() : fallback;
        }

        public int Int(int row, string column, int fallback = 0)
        {
            return int.TryParse(Str(row, column, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
        }

        public float Float(int row, string column, float fallback = 0f)
        {
            return float.TryParse(Str(row, column, null), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }

        public bool Bool(int row, string column, bool fallback = false)
        {
            string s = Str(row, column, null);
            if (s == null || s.Length == 0) return fallback;
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '\n')
                {
                    int end = i;
                    if (end > start && text[end - 1] == '\r') end--;
                    yield return text.Substring(start, end - start);
                    start = i + 1;
                }
            }
        }

        private static List<string> SplitCells(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else sb.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { cells.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(ch);
            }
            cells.Add(sb.ToString());
            return cells;
        }
    }
}
