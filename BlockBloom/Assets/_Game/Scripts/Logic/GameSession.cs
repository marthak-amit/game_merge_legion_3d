using System.Collections.Generic;

namespace BlockBloom.Logic
{
    public enum Mode { Adventure, Classic, Daily }

    public struct MoveOutcome
    {
        public bool Valid;
        public PlaceResult Place;
        public MoveScore Score;
        public bool TrayRefilled;
        public bool Won, Lost;
        public int[] GoalDeltas;
        public ulong BloomMask;          // cells removed by a Bloom Burst this move (0 = none)
        public int BloomColour, BloomGems, BloomPoints;
    }

    /// <summary>
    /// One play session: board, tray, score, goals, move budget, boosters. Pure logic (no Unity types) so it is fully unit-tested
    /// and the views only animate what happened.
    /// </summary>
    public sealed class GameSession
    {
        public const int TraySize = 3;

        public readonly Mode Mode;
        public readonly LevelDef Level;      // null outside adventure
        public BoardState Board = new BoardState();
        public ScoreKeeper Keeper = new ScoreKeeper();
        public Shape[] Tray = new Shape[TraySize];
        public bool[] Used = new bool[TraySize];
        public int[] TrayColour = new int[TraySize];
        public int MovesLeft;           // adventure only
        public int MovesMade;
        public int[] GoalProgress;
        public bool Finished, Won;
        public int LastWinStars;
        public int Seed;
        public int PerfectClears;
        public int Revives;
        public int BloomMeter;          // lines cleared towards the next Bloom Burst
        public const int BloomLines = 10;

        private TrayGenerator _gen;
        private BbRng _colourRng;
        private Snapshot _undo;

        private sealed class Snapshot
        {
            public BoardState Board; public ScoreKeeper Keeper; public Shape[] Tray; public bool[] Used; public int[] TrayColour;
            public int MovesLeft, MovesMade; public int[] Progress; public int Perfect; public int Bloom;
        }

        public GameSession(Mode mode, int seed, LevelDef level = null)
        {
            Mode = mode; Level = level; Seed = seed;
            _gen = new TrayGenerator(seed, level != null ? ParamsFor(level) : new GenParams());
            _colourRng = new BbRng(seed ^ 0x5bd1e995);
            if (level != null)
            {
                Board.Occ = level.Prefill; Board.Gems = level.Gems;
                for (int i = 0; i < Bits.Cells; i++)
                    if ((level.Prefill & (1UL << i)) != 0) Board.Colors[i] = (byte)(1 + ((i * 7 + level.Index) % Adventure.PaletteSize));
                MovesLeft = level.MaxMoves;
                GoalProgress = new int[level.Goals.Length];
            }
            else GoalProgress = new int[0];
            Refill();
        }

        public static GenParams ParamsFor(LevelDef l)
        {
            var p = new GenParams();
            p.HardScore = 3000 + l.Index * 40;
            p.AssistChance = l.Index < 20 ? 0.65f : 0.5f;
            return p;
        }

        public int Score { get { return Keeper.Score; } }
        public bool HasMoveLimit { get { return Level != null; } }
        public bool CanUndo { get { return _undo != null && !Finished; } }
        public bool TrayEmpty { get { for (int i = 0; i < TraySize; i++) if (!Used[i]) return false; return true; } }

        public Shape[] Remaining()
        {
            var l = new List<Shape>();
            for (int i = 0; i < TraySize; i++) if (!Used[i] && Tray[i] != null) l.Add(Tray[i]);
            return l.ToArray();
        }

        private void Refill()
        {
            var t = _gen.Next(Board.Occ, Keeper.Score);
            for (int i = 0; i < TraySize; i++)
            {
                Tray[i] = t[i]; Used[i] = false;
                TrayColour[i] = (Tray[i].Family + _colourRng.Range(0, 3)) % Adventure.PaletteSize;
            }
        }

        private void TakeSnapshot()
        {
            _undo = new Snapshot
            {
                Board = Board.Clone(), Keeper = Keeper.Clone(), Tray = (Shape[])Tray.Clone(), Used = (bool[])Used.Clone(),
                TrayColour = (int[])TrayColour.Clone(), MovesLeft = MovesLeft, MovesMade = MovesMade,
                Progress = (int[])GoalProgress.Clone(), Perfect = PerfectClears, Bloom = BloomMeter
            };
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            var s = _undo; _undo = null;
            Board.CopyFrom(s.Board); Keeper.CopyFrom(s.Keeper);
            Tray = s.Tray; Used = s.Used; TrayColour = s.TrayColour;
            MovesLeft = s.MovesLeft; MovesMade = s.MovesMade; GoalProgress = s.Progress; PerfectClears = s.Perfect; BloomMeter = s.Bloom;
            return true;
        }

        public bool CanPlace(int slot, int r, int c)
        {
            return !Finished && slot >= 0 && slot < TraySize && !Used[slot] && Tray[slot] != null && Board.CanPlace(Tray[slot], r, c);
        }

        public MoveOutcome Place(int slot, int r, int c)
        {
            var o = new MoveOutcome { GoalDeltas = new int[GoalProgress.Length] };
            if (!CanPlace(slot, r, c)) return o;
            TakeSnapshot();
            var shape = Tray[slot];
            var pr = Board.Place(shape, r, c, TrayColour[slot]);
            Used[slot] = true;
            MovesMade++;
            if (HasMoveLimit) MovesLeft--;
            var ms = Keeper.Apply(pr.CellsPlaced, pr.Lines, pr.PerfectClear);
            if (pr.PerfectClear) PerfectClears++;
            o.Valid = true; o.Place = pr; o.Score = ms;

            // Bloom Burst: every BloomLines cleared lines, all blocks of the most common colour burst away
            byte[] bloomColours = null;
            if (pr.Lines > 0)
            {
                BloomMeter += pr.Lines;
                if (BloomMeter >= BloomLines)
                {
                    ulong bm; int bc;
                    if (TryBloom(out bm, out bc))
                    {
                        BloomMeter -= BloomLines;
                        o.BloomMask = bm; o.BloomColour = bc;
                        o.BloomGems = Bits.PopCount(Board.Gems & bm);
                        o.BloomPoints = Bits.PopCount(bm) * 5;
                        Board.Remove(bm, out bloomColours);
                        Keeper.Score += o.BloomPoints;
                    }
                }
            }

            if (Level != null)
            {
                for (int g = 0; g < Level.Goals.Length; g++)
                {
                    var goal = Level.Goals[g];
                    int before = GoalProgress[g];
                    switch (goal.Type)
                    {
                        case GoalType.Score: GoalProgress[g] = Keeper.Score; break;
                        case GoalType.Lines: GoalProgress[g] = Keeper.TotalLines; break;
                        case GoalType.Gems: GoalProgress[g] += pr.GemsCollected + o.BloomGems; break;
                        case GoalType.Combo: GoalProgress[g] = System.Math.Max(GoalProgress[g], Keeper.BestCombo); break;
                        case GoalType.Colour:
                            if (pr.ClearedColors != null)
                                for (int i = 0; i < Bits.Cells; i++)
                                    if ((pr.Cleared & (1UL << i)) != 0 && pr.ClearedColors[i] == goal.Colour + 1) GoalProgress[g]++;
                            if (bloomColours != null)
                                for (int i = 0; i < Bits.Cells; i++)
                                    if ((o.BloomMask & (1UL << i)) != 0 && bloomColours[i] == goal.Colour + 1) GoalProgress[g]++;
                            break;
                    }
                    o.GoalDeltas[g] = GoalProgress[g] - before;
                }
                if (GoalsMet()) { Finished = true; Won = true; o.Won = true; LastWinStars = StarsNow(); return o; }
                if (MovesLeft <= 0) { Finished = true; Won = false; o.Lost = true; return o; }
            }

            if (TrayEmpty) { Refill(); o.TrayRefilled = true; }
            if (Solver.GameOver(Board.Occ, Remaining()))
            {
                Finished = true; Won = false; o.Lost = true;
            }
            return o;
        }

        /// <summary>Finds the most common block colour on the board (needs at least 4 blocks of it).</summary>
        public bool TryBloom(out ulong mask, out int colour)
        {
            var counts = new int[16];
            for (int i = 0; i < Bits.Cells; i++)
                if ((Board.Occ & (1UL << i)) != 0) counts[Board.Colors[i] & 15]++;
            int best = 0, bestCount = 0;
            for (int c = 1; c < 16; c++) if (counts[c] > bestCount) { bestCount = counts[c]; best = c; }
            mask = 0; colour = best - 1;
            if (best == 0 || bestCount < 4) return false;
            for (int i = 0; i < Bits.Cells; i++)
                if ((Board.Occ & (1UL << i)) != 0 && Board.Colors[i] == best) mask |= 1UL << i;
            return true;
        }

        public bool GoalsMet()
        {
            if (Level == null) return false;
            for (int g = 0; g < Level.Goals.Length; g++) if (GoalProgress[g] < Level.Goals[g].Target) return false;
            return true;
        }

        public float GoalFraction(int g)
        {
            float t = Level.Goals[g].Target;
            return t <= 0 ? 1f : System.Math.Min(1f, GoalProgress[g] / t);
        }

        /// <summary>Stars if the level ended now: efficiency = moves left over the budget (3 stars: 40%+, 2 stars: 15%+).</summary>
        public int StarsNow()
        {
            if (Level == null) return 0;
            float left = Level.MaxMoves <= 0 ? 0f : (float)MovesLeft / Level.MaxMoves;
            if (left >= 0.40f) return 3;
            if (left >= 0.15f) return 2;
            return 1;
        }

        // ---------- boosters ----------
        /// <summary>Bomb: clears the 3x3 area around a cell (gems inside are collected).</summary>
        public bool UseBomb(int r, int c, out ulong cleared, out int gems)
        {
            cleared = 0; gems = 0;
            if (Finished) return false;
            TakeSnapshot();
            ulong area = Bits.Area3x3(r, c) & Board.Occ;
            if (area == 0) { _undo = null; return false; }
            gems = Bits.PopCount(area & Board.Gems);
            byte[] before;
            Board.Remove(area, out before);
            cleared = area;
            if (Level != null)
                for (int g = 0; g < Level.Goals.Length; g++)
                    if (Level.Goals[g].Type == GoalType.Gems) GoalProgress[g] += gems;
            if (GoalsMet()) { Finished = true; Won = true; LastWinStars = StarsNow(); }
            return true;
        }

        /// <summary>Shuffle: replaces the unused pieces with a fresh solvable tray.</summary>
        public void Shuffle()
        {
            TakeSnapshot();
            Refill();
        }

        public void AddMoves(int n) { if (HasMoveLimit) { MovesLeft += n; if (Finished && !Won) Finished = false; } }

        /// <summary>Second chance after a loss: clear a band of the board and give a fresh tray.</summary>
        public ulong Revive(int extraMoves)
        {
            Revives++;
            Finished = false; Won = false;
            // clear the three fullest rows' worth of cells (the middle band is enough room and feels generous)
            int bestRow = 0, bestCount = -1;
            for (int r = 0; r + 2 < Bits.N; r++)
            {
                int cnt = Bits.PopCount(Board.Occ & (Bits.RowMasks[r] | Bits.RowMasks[r + 1] | Bits.RowMasks[r + 2]));
                if (cnt > bestCount) { bestCount = cnt; bestRow = r; }
            }
            ulong band = (Bits.RowMasks[bestRow] | Bits.RowMasks[bestRow + 1] | Bits.RowMasks[bestRow + 2]) & Board.Occ;
            byte[] before;
            Board.Remove(band, out before);
            if (extraMoves > 0) AddMoves(extraMoves);
            Refill();
            _undo = null;
            return band;
        }
    }
}
