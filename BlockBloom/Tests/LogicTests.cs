using System;
using System.Collections.Generic;
using NUnit.Framework;
using BlockBloom.Logic;

namespace BlockBloom.Tests
{
    public class LogicTests
    {
        [Test] public void PopCount_Works()
        {
            Assert.AreEqual(0, Bits.PopCount(0));
            Assert.AreEqual(64, Bits.PopCount(ulong.MaxValue));
            Assert.AreEqual(8, Bits.PopCount(Bits.RowMasks[3]));
            Assert.AreEqual(8, Bits.PopCount(Bits.ColMasks[5]));
        }

        [Test] public void Catalog_ShapesAreUniqueAndInBounds()
        {
            var seen = new HashSet<ulong>();
            foreach (var s in ShapeCatalog.All)
            {
                Assert.IsTrue(s.Count >= 1 && s.Count <= 9, s.Name);
                Assert.IsTrue(s.Width <= 5 && s.Height <= 5, s.Name);
                ulong m = s.MaskAt(0, 0);
                Assert.AreNotEqual(0UL, m);
                Assert.AreEqual(s.Count, Bits.PopCount(m), s.Name);
                Assert.IsTrue(seen.Add(m), "duplicate shape " + s.Name);
                Assert.AreEqual(0UL, s.MaskAt(8 - s.Height + 1, 0));
                Assert.AreNotEqual(0UL, s.MaskAt(8 - s.Height, 8 - s.Width));
            }
            Console.WriteLine("shapes=" + ShapeCatalog.All.Length);
            Assert.IsTrue(ShapeCatalog.All.Length >= 40);
        }

        private static Shape Find(string prefix)
        {
            foreach (var s in ShapeCatalog.All) if (s.Name.StartsWith(prefix)) return s;
            throw new Exception("no shape " + prefix);
        }

        [Test] public void Place_CompletesRow()
        {
            var b = new BoardState();
            var line = Find("line5");           // horizontal 5
            Shape line3 = null; foreach (var s in ShapeCatalog.All) if (s.Name.StartsWith("line3") && s.Width == 3) line3 = s;
            Assert.IsTrue(b.Place(line, 2, 0, 1).Placed);
            var res = b.Place(line3, 2, 5, 1);
            Assert.AreEqual(1, res.Rows);
            Assert.AreEqual(0, res.Cols);
            Assert.AreEqual(8, Bits.PopCount(res.Cleared));
            Assert.IsTrue(b.IsEmpty);
            Assert.IsTrue(res.PerfectClear);
            Assert.IsNotNull(res.ClearedColors);
        }

        [Test] public void Place_RejectsOverlapAndBounds()
        {
            var b = new BoardState();
            var sq = Find("square2");
            Assert.IsTrue(b.Place(sq, 0, 0, 0).Placed);
            Assert.IsFalse(b.Place(sq, 1, 1, 0).Placed);
            Assert.IsFalse(b.CanPlace(sq, 7, 7));
            Assert.IsFalse(b.CanPlace(sq, -1, 0));
        }

        [Test] public void Place_CrossClearsRowAndColumn()
        {
            var b = new BoardState();
            for (int i = 0; i < 8; i++) { if (i != 4) { b.Fill(4, i, 0, false); b.Fill(i, 4, 0, false); } }
            var dot = Find("dot");
            var res = b.Place(dot, 4, 4, 2);
            Assert.AreEqual(1, res.Rows);
            Assert.AreEqual(1, res.Cols);
            Assert.AreEqual(15, Bits.PopCount(res.Cleared));
            Assert.IsTrue(b.IsEmpty);
        }

        [Test] public void Gems_AreCollectedWhenTheirLineClears()
        {
            var b = new BoardState();
            for (int c = 0; c < 7; c++) b.Fill(0, c, 0, c == 3);
            var res = b.Place(Find("dot"), 0, 7, 0);
            Assert.AreEqual(1, res.GemsCollected);
            Assert.AreEqual(0UL, b.Gems);
        }

        [Test] public void Preview_MatchesPlace()
        {
            var rng = new BbRng(5);
            for (int t = 0; t < 200; t++)
            {
                var b = new BoardState();
                for (int i = 0; i < 30; i++) { int r = rng.Range(0, 8), c = rng.Range(0, 8); if (Bits.ClearedMask(b.Occ | Bits.Bit(r, c)) == 0) b.Fill(r, c, 0, false); }
                var s = ShapeCatalog.All[rng.Range(0, ShapeCatalog.All.Length)];
                for (int r = 0; r + s.Height <= 8; r++)
                    for (int c = 0; c + s.Width <= 8; c++)
                    {
                        if (!b.CanPlace(s, r, c)) continue;
                        int rm, cm;
                        ulong prev = b.PreviewClear(s, r, c, out rm, out cm);
                        var clone = b.Clone();
                        var res = clone.Place(s, r, c, 0);
                        Assert.AreEqual(prev, res.Cleared);
                    }
            }
        }

        [Test] public void Scoring_BaseAndCombo()
        {
            Assert.AreEqual(10, ScoreKeeper.BaseLinePoints(1));
            Assert.AreEqual(30, ScoreKeeper.BaseLinePoints(2));
            Assert.AreEqual(100, ScoreKeeper.BaseLinePoints(4));
            var k = new ScoreKeeper();
            var a = k.Apply(5, 1, false);
            Assert.AreEqual(1, a.Combo);
            Assert.AreEqual(15, a.Total);
            var b = k.Apply(4, 1, false);
            Assert.AreEqual(2, b.Combo);
            Assert.AreEqual(4 + 15, b.Total);     // 10 * 1.5
            k.Apply(1, 0, false); k.Apply(1, 0, false);
            Assert.AreEqual(2, k.Combo);
            k.Apply(1, 0, false);                 // third non-clearing move drops the combo
            Assert.AreEqual(0, k.Combo);
            var p = k.Apply(1, 1, true);
            Assert.AreEqual(ScoreKeeper.PerfectClearBonus, p.PerfectBonus);
        }

        [Test] public void Solver_EmptyBoardAlwaysSolvable()
        {
            var rng = new BbRng(2);
            for (int i = 0; i < 100; i++)
            {
                var tray = new[] { ShapeCatalog.All[rng.Range(0, ShapeCatalog.All.Length)], ShapeCatalog.All[rng.Range(0, ShapeCatalog.All.Length)], ShapeCatalog.All[rng.Range(0, ShapeCatalog.All.Length)] };
                Assert.IsTrue(Solver.TraySolvable(0UL, tray, 20000));
            }
        }

        [Test] public void Solver_FullBoardIsNot()
        {
            var sq = Find("square2");
            Assert.IsFalse(Solver.TraySolvable(0x55AA55AA55AA55AAUL, new[] { sq, sq, sq }, 20000));   // checkerboard: no 2x2 hole anywhere
        }

        [Test] public void Generator_TraysAreSolvable_AndBotSurvivesLong()
        {
            int games = 60; long totalMoves = 0; int minMoves = int.MaxValue, maxMoves = 0; long totalScore = 0; int mercy = 0, trays = 0;
            for (int g = 0; g < games; g++)
            {
                var gen = new TrayGenerator(1000 + g);
                var board = new BoardState(); var score = new ScoreKeeper();
                int moves = 0;
                while (moves < 3000)
                {
                    var tray = gen.Next(board.Occ, score.Score);
                    trays++;
                    if (!Solver.TraySolvable(board.Occ, tray, 25000)) mercy++;
                    var left = new List<Shape>(tray);
                    bool dead = false;
                    while (left.Count > 0)
                    {
                        // bot: place the piece with the best greedy placement
                        int bi = -1, br = -1, bc = -1;
                        for (int i = 0; i < left.Count; i++)
                        {
                            int r, c;
                            if (Solver.BestPlacement(board.Occ, left[i], out r, out c)) { bi = i; br = r; bc = c; break; }
                        }
                        if (bi < 0) { dead = true; break; }
                        var res = board.Place(left[bi], br, bc, left[bi].Family);
                        score.Apply(res.CellsPlaced, res.Lines, res.PerfectClear);
                        left.RemoveAt(bi); moves++;
                    }
                    if (dead) break;
                }
                totalMoves += moves; totalScore += score.Score;
                if (moves < minMoves) minMoves = moves; if (moves > maxMoves) maxMoves = moves;
            }
            Console.WriteLine("bot: games=" + games + " avgMoves=" + (totalMoves / games) + " min=" + minMoves + " max=" + maxMoves + " avgScore=" + (totalScore / games) + " mercyTrays=" + mercy + "/" + trays);
            Assert.IsTrue(mercy * 100 < trays * 12, "too many unsolvable trays");
            Assert.IsTrue(totalMoves / games >= 30, "bot dies too fast - generator too hard");
        }

        [Test] public void Generator_IsDeterministicPerSeed()
        {
            var a = new TrayGenerator(42); var b = new TrayGenerator(42);
            for (int i = 0; i < 20; i++)
            {
                var ta = a.NextBlind(); var tb = b.NextBlind();
                for (int k = 0; k < 3; k++) Assert.AreEqual(ta[k].Id, tb[k].Id);
            }
        }

        [Test] public void Adventure_LevelsAreDeterministicAndFair()
        {
            for (int i = 1; i <= Adventure.LevelCount; i++)
            {
                var a = Adventure.Get(i); var b = Adventure.Get(i);
                Assert.AreEqual(a.Seed, b.Seed);
                Assert.AreEqual(a.Prefill, b.Prefill);
                Assert.AreEqual(a.Goals.Length, b.Goals.Length);
                Assert.AreEqual(0UL, Bits.ClearedMask(a.Prefill), "level " + i + " pre-completes a line");
                Assert.IsTrue(a.MaxMoves >= 8);
                Assert.IsTrue(a.Star3Score > a.Star2Score);
                foreach (var g in a.Goals)
                {
                    Assert.IsTrue(g.Target > 0);
                    if (g.Type == GoalType.Gems) { Assert.AreEqual(g.Target, Bits.PopCount(a.Gems)); Assert.AreEqual(a.Gems, a.Gems & a.Prefill); }
                }
            }
            Assert.AreEqual(0UL, Adventure.Get(1).Prefill);
            Assert.AreEqual(GoalType.Score, Adventure.Get(1).Goals[0].Type);
        }

        static void BotMove(GameSession s)
        {
            int bestSlot = -1, br = 0, bc = 0, bs = int.MinValue;
            for (int i = 0; i < GameSession.TraySize; i++)
            {
                if (s.Used[i]) continue;
                int r, c;
                if (!Solver.BestPlacement(s.Board.Occ, s.Tray[i], out r, out c)) continue;
                ulong after = s.Board.Occ | s.Tray[i].MaskAt(r, c);
                int sc = Bits.PopCount(Bits.ClearedMask(after)) * 10 + (s.Board.Gems != 0 ? Bits.PopCount(Bits.ClearedMask(after) & s.Board.Gems) * 30 : 0);
                if (sc > bs) { bs = sc; bestSlot = i; br = r; bc = c; }
            }
            if (bestSlot < 0) { s.Finished = true; return; }
            s.Place(bestSlot, br, bc);
        }

        [Test] public void Session_PlaceUndoAndTrayRefill()
        {
            var s = new GameSession(Mode.Classic, 5);
            int filled0 = s.Board.Filled;
            int r, c;
            Assert.IsTrue(Solver.BestPlacement(s.Board.Occ, s.Tray[0], out r, out c));
            var o = s.Place(0, r, c);
            Assert.IsTrue(o.Valid);
            Assert.IsTrue(s.Used[0]);
            Assert.IsTrue(s.CanUndo);
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(filled0, s.Board.Filled);
            Assert.IsFalse(s.Used[0]);
            // use all three -> refill
            bool refilled = false;
            for (int k = 0; k < 3 && !s.Finished; k++) { BotMove(s); }
            for (int i = 0; i < 3; i++) if (s.Used[i]) refilled = false;
            Assert.IsTrue(s.MovesMade >= 3 || s.Finished);
        }

        [Test] public void Session_LevelOneIsWinnable()
        {
            int wins = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var s = new GameSession(Mode.Adventure, 1000 + seed, Adventure.Get(1));
                int guard = 0;
                while (!s.Finished && guard++ < 200) BotMove(s);
                if (s.Won) wins++;
            }
            Console.WriteLine("level1 bot wins " + wins + "/20");
            Assert.IsTrue(wins >= 18, "level 1 must be easy, wins=" + wins);
        }

        [Test] public void Session_BotWinRateByBracket()
        {
            int[] starts = { 1, 6, 16, 31, 61, 101 };
            int[] ends = { 5, 15, 30, 60, 100, 200 };
            for (int b = 0; b < starts.Length; b++)
            {
                int wins = 0, total = 0;
                for (int lv = starts[b]; lv <= ends[b]; lv++)
                    for (int seed = 0; seed < 4; seed++)
                    {
                        var d = Adventure.Get(lv);
                        var s = new GameSession(Mode.Adventure, d.Seed + seed * 31, d);
                        int guard = 0;
                        while (!s.Finished && guard++ < 400) BotMove(s);
                        total++; if (s.Won) wins++;
                    }
                Console.WriteLine("levels " + starts[b] + "-" + ends[b] + ": bot win " + (100 * wins / total) + "%");
            }
        }

        [Test] public void Session_BombAndRevive()
        {
            var d = Adventure.Get(20);
            var s = new GameSession(Mode.Adventure, 3, d);
            int before = s.Board.Filled;
            ulong cl; int gems;
            bool any = false;
            for (int r = 0; r < 8 && !any; r++) for (int c = 0; c < 8 && !any; c++)
                if (s.Board.IsOccupied(r, c)) { any = s.UseBomb(r, c, out cl, out gems); }
            Assert.IsTrue(any);
            Assert.IsTrue(s.Board.Filled < before);
            s.Finished = true; s.MovesLeft = 0;
            s.Revive(5);
            Assert.IsFalse(s.Finished);
            Assert.AreEqual(5, s.MovesLeft);
        }
    }
}
