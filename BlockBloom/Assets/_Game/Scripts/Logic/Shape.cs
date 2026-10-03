using System;
using System.Collections.Generic;
using System.Text;

namespace BlockBloom.Logic
{
    public sealed class Shape
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int Family;      // drives the block colour
        public readonly float Weight;
        public readonly int Width, Height, Count;
        public readonly int[] Rows, Cols;
        private readonly ulong[] _masks; // index r*N+c = placement of the top-left corner; 0 when out of bounds

        public Shape(int id, string name, int family, float weight, int[] rows, int[] cols)
        {
            Id = id; Name = name; Family = family; Weight = weight; Rows = rows; Cols = cols; Count = rows.Length;
            int maxR = 0, maxC = 0;
            for (int i = 0; i < rows.Length; i++) { if (rows[i] > maxR) maxR = rows[i]; if (cols[i] > maxC) maxC = cols[i]; }
            Height = maxR + 1; Width = maxC + 1;
            _masks = new ulong[Bits.Cells];
            for (int r = 0; r + Height <= Bits.N; r++)
                for (int c = 0; c + Width <= Bits.N; c++)
                {
                    ulong m = 0;
                    for (int i = 0; i < rows.Length; i++) m |= Bits.Bit(r + rows[i], c + cols[i]);
                    _masks[r * Bits.N + c] = m;
                }
        }

        public ulong MaskAt(int r, int c)
        {
            if (r < 0 || c < 0 || r + Height > Bits.N || c + Width > Bits.N) return 0UL;
            return _masks[r * Bits.N + c];
        }

        public bool Has(int r, int c)
        {
            for (int i = 0; i < Rows.Length; i++) if (Rows[i] == r && Cols[i] == c) return true;
            return false;
        }

        public override string ToString() { return Name + "#" + Id; }
    }

    public static class ShapeCatalog
    {
        public static readonly Shape[] All = Build();

        public static Shape ById(int id) { return All[id]; }

        private struct Def { public string Name; public int Family; public float Weight; public bool Rotate, Mirror; public string[] Art; }

        private static Shape[] Build()
        {
            var defs = new List<Def>
            {
                D("dot",     0, 3.0f, false, false, "X"),
                D("line2",   1, 4.5f, true,  false, "XX"),
                D("line3",   1, 4.5f, true,  false, "XXX"),
                D("line4",   1, 3.0f, true,  false, "XXXX"),
                D("line5",   1, 1.8f, true,  false, "XXXXX"),
                D("square2", 2, 4.5f, false, false, "XX", "XX"),
                D("square3", 2, 2.2f, false, false, "XXX", "XXX", "XXX"),
                D("rect23",  3, 3.2f, true,  false, "XXX", "XXX"),
                D("corner2", 4, 3.8f, true,  false, "X.", "XX"),
                D("corner3", 5, 2.8f, true,  false, "X..", "X..", "XXX"),
                D("ell4",    6, 3.4f, true,  true,  "X.", "X.", "XX"),
                D("tee4",    0, 3.2f, true,  false, "XXX", ".X."),
                D("ess4",    4, 3.2f, true,  true,  ".XX", "XX."),
                D("tee5",    5, 1.4f, true,  false, "XXX", ".X.", ".X."),
                D("plus",    2, 1.1f, false, false, ".X.", "XXX", ".X."),
            };

            var list = new List<Shape>();
            var seen = new HashSet<string>();
            foreach (var d in defs)
            {
                var cells = Parse(d.Art);
                var variants = new List<List<int[]>>();
                variants.Add(cells);
                if (d.Rotate)
                {
                    var cur = cells;
                    for (int i = 0; i < 3; i++) { cur = Rotate(cur); variants.Add(cur); }
                }
                if (d.Mirror)
                {
                    int count = variants.Count;
                    for (int i = 0; i < count; i++) variants.Add(Mirror(variants[i]));
                }
                int n = 0;
                foreach (var v in variants)
                {
                    var norm = Normalise(v);
                    if (!seen.Add(Key(norm))) continue; // identical cell set already exists (e.g. 180° line)
                    var rows = new int[norm.Count]; var cols = new int[norm.Count];
                    for (int i = 0; i < norm.Count; i++) { rows[i] = norm[i][0]; cols[i] = norm[i][1]; }
                    list.Add(new Shape(list.Count, d.Name + (n++), d.Family, d.Weight, rows, cols));
                }
            }
            return list.ToArray();
        }

        private static Def D(string name, int family, float weight, bool rotate, bool mirror, params string[] art)
        {
            return new Def { Name = name, Family = family, Weight = weight, Rotate = rotate, Mirror = mirror, Art = art };
        }

        private static List<int[]> Parse(string[] art)
        {
            var cells = new List<int[]>();
            for (int r = 0; r < art.Length; r++)
                for (int c = 0; c < art[r].Length; c++)
                    if (art[r][c] == 'X') cells.Add(new[] { r, c });
            return cells;
        }

        private static List<int[]> Rotate(List<int[]> cells)
        {
            int maxR = 0; foreach (var p in cells) if (p[0] > maxR) maxR = p[0];
            var o = new List<int[]>();
            foreach (var p in cells) o.Add(new[] { p[1], maxR - p[0] });
            return o;
        }

        private static List<int[]> Mirror(List<int[]> cells)
        {
            int maxC = 0; foreach (var p in cells) if (p[1] > maxC) maxC = p[1];
            var o = new List<int[]>();
            foreach (var p in cells) o.Add(new[] { p[0], maxC - p[1] });
            return o;
        }

        private static List<int[]> Normalise(List<int[]> cells)
        {
            int minR = int.MaxValue, minC = int.MaxValue;
            foreach (var p in cells) { if (p[0] < minR) minR = p[0]; if (p[1] < minC) minC = p[1]; }
            var o = new List<int[]>();
            foreach (var p in cells) o.Add(new[] { p[0] - minR, p[1] - minC });
            o.Sort((a, b) => a[0] != b[0] ? a[0].CompareTo(b[0]) : a[1].CompareTo(b[1]));
            return o;
        }

        private static string Key(List<int[]> norm)
        {
            var sb = new StringBuilder();
            foreach (var p in norm) sb.Append(p[0]).Append(',').Append(p[1]).Append(';');
            return sb.ToString();
        }
    }
}
