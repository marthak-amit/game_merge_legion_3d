using UnityEngine;

namespace BlockBloom
{
    public sealed class Theme
    {
        public string Name;
        public Color BgTop, BgBottom, Board, Cell, Panel, PanelDark, Accent;
        public int Price;   // coins; 0 = free
    }

    public static class Palette
    {
        public static Color Hex(string h)
        {
            Color c; ColorUtility.TryParseHtmlString(h, out c); return c;
        }

        // piece colours (index matches Adventure.PaletteSize = 7)
        public static readonly Color[] Blocks =
        {
            Hex("#19d4c1"), // teal
            Hex("#4a8dff"), // blue
            Hex("#ffca2b"), // yellow
            Hex("#a66cff"), // purple
            Hex("#ff5a96"), // pink
            Hex("#ff8a34"), // orange
            Hex("#55dc57"), // green
        };

        public static Color Block(int i) { return Blocks[((i % Blocks.Length) + Blocks.Length) % Blocks.Length]; }
        public static Color Dark(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }
        public static Color Alpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }
        public static Color Lighten(Color c, float k) { return Color.Lerp(c, Color.white, k); }

        public static readonly Color Gold = Hex("#ffc93c");
        public static readonly Color GoldDark = Hex("#d98a00");
        public static readonly Color Green = Hex("#3fd36a");
        public static readonly Color GreenDark = Hex("#1f9b45");
        public static readonly Color Red = Hex("#ff5468");
        public static readonly Color RedDark = Hex("#c92e47");
        public static readonly Color Blue = Hex("#3b8cff");
        public static readonly Color BlueDark = Hex("#2058c9");
        public static readonly Color Purple = Hex("#9b5cff");
        public static readonly Color PurpleDark = Hex("#6532c4");
        public static readonly Color TextDark = Hex("#2a1f5c");
        public static readonly Color Ink = Hex("#150f3d");

        public static readonly Theme[] Themes =
        {
            new Theme { Name = "Midnight", BgTop = Hex("#3a2a9c"), BgBottom = Hex("#120c3d"), Board = Hex("#1b1646"), Cell = Hex("#2e2573"), Panel = Hex("#3d3197"), PanelDark = Hex("#241b66"), Accent = Hex("#19d4c1"), Price = 0 },
            new Theme { Name = "Sunset",   BgTop = Hex("#ff7a59"), BgBottom = Hex("#5b1d6b"), Board = Hex("#4a1658"), Cell = Hex("#6d2a80"), Panel = Hex("#8a3a9a"), PanelDark = Hex("#531565"), Accent = Hex("#ffd24a"), Price = 600 },
            new Theme { Name = "Ocean",    BgTop = Hex("#1fb6d9"), BgBottom = Hex("#0a2a6b"), Board = Hex("#0b2f6e"), Cell = Hex("#154a94"), Panel = Hex("#1e63b8"), PanelDark = Hex("#0d3c84"), Accent = Hex("#7cf2b0"), Price = 900 },
            new Theme { Name = "Candy",    BgTop = Hex("#ff8fc9"), BgBottom = Hex("#7a3fd1"), Board = Hex("#58209e"), Cell = Hex("#7336c2"), Panel = Hex("#9150e0"), PanelDark = Hex("#5b25a6"), Accent = Hex("#fff176"), Price = 1500 },
        };
    }
}
