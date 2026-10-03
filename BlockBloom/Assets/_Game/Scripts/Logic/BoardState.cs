namespace BlockBloom.Logic
{
    public struct PlaceResult
    {
        public bool Placed;
        public int CellsPlaced;
        public ulong PlacedMask;
        public ulong Cleared;        // cells removed by completed lines
        public int Rows, Cols;       // completed lines
        public int RowMask, ColMask;
        public int GemsCollected;
        public bool PerfectClear;
        public byte[] ClearedColors; // colour per cell index before clearing (null when nothing cleared)
        public int Lines { get { return Rows + Cols; } }
    }

    /// <summary>Board model: occupancy bits + colour per cell + gem cells (adventure collectibles).</summary>
    public sealed class BoardState
    {
        public ulong Occ;
        public ulong Gems;
        public readonly byte[] Colors = new byte[Bits.Cells]; // 0 = empty, otherwise colour index + 1

        public int Filled { get { return Bits.PopCount(Occ); } }
        public bool IsEmpty { get { return Occ == 0; } }
        public bool IsOccupied(int r, int c) { return (Occ & Bits.Bit(r, c)) != 0; }
        public int ColorAt(int r, int c) { return Colors[r * Bits.N + c]; }

        public bool CanPlace(Shape s, int r, int c)
        {
            ulong m = s.MaskAt(r, c);
            return m != 0 && (Occ & m) == 0;
        }

        public bool CanPlaceAnywhere(Shape s)
        {
            for (int r = 0; r + s.Height <= Bits.N; r++)
                for (int c = 0; c + s.Width <= Bits.N; c++)
                    if ((Occ & s.MaskAt(r, c)) == 0) return true;
            return false;
        }

        /// <summary>Lines a placement would complete, without changing the board (drives the clear preview glow).</summary>
        public ulong PreviewClear(Shape s, int r, int c, out int rowMask, out int colMask)
        {
            rowMask = 0; colMask = 0;
            ulong m = s.MaskAt(r, c);
            if (m == 0 || (Occ & m) != 0) return 0;
            int rows, cols;
            return Bits.FullLines(Occ | m, out rows, out cols, out rowMask, out colMask);
        }

        public PlaceResult Place(Shape s, int r, int c, int colorIndex)
        {
            var res = new PlaceResult();
            ulong m = s.MaskAt(r, c);
            if (m == 0 || (Occ & m) != 0) return res;
            res.Placed = true; res.PlacedMask = m; res.CellsPlaced = s.Count;
            Occ |= m;
            for (int i = 0; i < s.Count; i++) Colors[(r + s.Rows[i]) * Bits.N + c + s.Cols[i]] = (byte)(colorIndex + 1);

            int rows, cols, rowMask, colMask;
            ulong cleared = Bits.FullLines(Occ, out rows, out cols, out rowMask, out colMask);
            if (cleared != 0)
            {
                res.Cleared = cleared; res.Rows = rows; res.Cols = cols; res.RowMask = rowMask; res.ColMask = colMask;
                res.ClearedColors = (byte[])Colors.Clone();
                res.GemsCollected = Bits.PopCount(Gems & cleared);
                Gems &= ~cleared;
                Occ &= ~cleared;
                for (int i = 0; i < Bits.Cells; i++) if ((cleared & (1UL << i)) != 0) Colors[i] = 0;
                res.PerfectClear = Occ == 0;
            }
            return res;
        }

        /// <summary>Removes a set of cells (bomb power-up / revive). Returns gems collected.</summary>
        public int Remove(ulong mask, out byte[] colorsBefore)
        {
            colorsBefore = (byte[])Colors.Clone();
            int gems = Bits.PopCount(Gems & mask & Occ);
            Occ &= ~mask; Gems &= ~mask;
            for (int i = 0; i < Bits.Cells; i++) if ((mask & (1UL << i)) != 0) Colors[i] = 0;
            return gems;
        }

        public void Fill(int r, int c, int colorIndex, bool gem)
        {
            Occ |= Bits.Bit(r, c);
            Colors[r * Bits.N + c] = (byte)(colorIndex + 1);
            if (gem) Gems |= Bits.Bit(r, c);
        }

        public BoardState Clone()
        {
            var b = new BoardState { Occ = Occ, Gems = Gems };
            System.Array.Copy(Colors, b.Colors, Colors.Length);
            return b;
        }

        public void CopyFrom(BoardState o)
        {
            Occ = o.Occ; Gems = o.Gems;
            System.Array.Copy(o.Colors, Colors, Colors.Length);
        }
    }
}
