using MergeLegion.Core;
using MergeLegion.Grid;
using NUnit.Framework;

namespace MergeLegion.Tests
{
    public class GridTests
    {
        private static GridModel Grid() => new GridModel(5, 3);

        [Test]
        public void Model_StartsEmpty_AndIndexMathRoundTrips()
        {
            var g = Grid();
            Assert.AreEqual(15, g.Count);
            Assert.AreEqual(15, g.EmptyCount());
            int i = g.Index(3, 2);
            Assert.AreEqual(3, g.ColOf(i));
            Assert.AreEqual(2, g.RowOf(i));
        }

        [Test]
        public void PickRandomEmpty_NeverReturnsOccupied_AndMinusOneWhenFull()
        {
            var g = Grid();
            var rng = new DeterministicRng(5);
            for (int n = 0; n < 15; n++)
            {
                int idx = g.PickRandomEmpty(rng);
                Assert.GreaterOrEqual(idx, 0);
                Assert.IsTrue(g.IsEmpty(idx));
                g.Set(idx, UnitSlot.Of(0, 1));
            }
            Assert.AreEqual(-1, g.PickRandomEmpty(rng));
        }

        [Test]
        public void Drop_OnEmpty_Moves()
        {
            var g = Grid();
            g.Set(0, UnitSlot.Of(1, 3));
            var r = MergeService.Drop(g, 0, 4, 8);
            Assert.AreEqual(DropKind.Move, r.Kind);
            Assert.IsTrue(g.IsEmpty(0));
            Assert.AreEqual(3, g.Get(4).level);
        }

        [Test]
        public void Drop_SameLineSameLevel_MergesUp()
        {
            var g = Grid();
            g.Set(0, UnitSlot.Of(2, 4));
            g.Set(1, UnitSlot.Of(2, 4));
            var r = MergeService.Drop(g, 0, 1, 8);
            Assert.AreEqual(DropKind.Merge, r.Kind);
            Assert.AreEqual(5, r.Level);
            Assert.IsTrue(g.IsEmpty(0));
            Assert.AreEqual(5, g.Get(1).level);
            Assert.AreEqual(2, g.Get(1).line);
        }

        [Test]
        public void Drop_DifferentLevel_Swaps()
        {
            var g = Grid();
            g.Set(0, UnitSlot.Of(0, 1));
            g.Set(1, UnitSlot.Of(0, 2));
            var r = MergeService.Drop(g, 0, 1, 8);
            Assert.AreEqual(DropKind.Swap, r.Kind);
            Assert.AreEqual(2, g.Get(0).level);
            Assert.AreEqual(1, g.Get(1).level);
        }

        [Test]
        public void Drop_DifferentLineSameLevel_Swaps()
        {
            var g = Grid();
            g.Set(0, UnitSlot.Of(0, 3));
            g.Set(1, UnitSlot.Of(1, 3));
            Assert.AreEqual(DropKind.Swap, MergeService.Drop(g, 0, 1, 8).Kind);
        }

        [Test]
        public void Drop_AtMaxLevel_DoesNotMerge()
        {
            var g = Grid();
            g.Set(0, UnitSlot.Of(0, 8));
            g.Set(1, UnitSlot.Of(0, 8));
            var r = MergeService.Drop(g, 0, 1, 8);
            Assert.AreEqual(DropKind.Invalid, r.Kind);
            Assert.AreEqual(8, g.Get(0).level);
            Assert.AreEqual(8, g.Get(1).level);
        }

        [Test]
        public void Drop_InvalidCases()
        {
            var g = Grid();
            g.Set(0, UnitSlot.Of(0, 1));
            Assert.AreEqual(DropKind.Invalid, MergeService.Drop(g, 0, 0, 8).Kind);
            Assert.AreEqual(DropKind.Invalid, MergeService.Drop(g, 3, 4, 8).Kind, "empty source");
            Assert.AreEqual(DropKind.Invalid, MergeService.Drop(g, 0, 99, 8).Kind, "out of range");
            Assert.AreEqual(DropKind.Invalid, MergeService.Drop(g, -1, 2, 8).Kind);
        }

        [Test]
        public void ChainMerging_OneToEight_NeedsAtLeastTwoToTheSevenUnits()
        {
            // Merging 128 level-1 units sequentially must produce exactly one level-8 unit.
            var g = new GridModel(5, 3);
            int units = 0;
            int bought = 0;
            while (g.HighestLevel() < 8 && bought < 1000)
            {
                if (g.EmptyCount() > 0) { g.Set(g.PickRandomEmpty(new DeterministicRng(bought)), UnitSlot.Of(0, 1)); bought++; }
                for (int i = 0; i < g.Count; i++)
                    for (int j = i + 1; j < g.Count; j++)
                        if (MergeService.CanMerge(g.Get(i), g.Get(j), 8)) { MergeService.Drop(g, i, j, 8); i = g.Count; break; }
                units++;
            }
            Assert.AreEqual(8, g.HighestLevel());
            Assert.GreaterOrEqual(bought, 128, "a level-8 unit holds the mass of 128 level-1 units");
        }
    }

    public class DeterministicRngTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new DeterministicRng(123);
            var b = new DeterministicRng(123);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextInt(1000), b.NextInt(1000));
        }

        [Test]
        public void DifferentSeed_DifferentSequence()
        {
            var a = new DeterministicRng(1);
            var b = new DeterministicRng(2);
            bool differs = false;
            for (int i = 0; i < 20; i++) if (a.NextInt(100000) != b.NextInt(100000)) differs = true;
            Assert.IsTrue(differs);
        }

        [Test]
        public void Ranges_AreRespected()
        {
            var r = new DeterministicRng(9);
            for (int i = 0; i < 1000; i++)
            {
                int n = r.Range(3, 7);
                Assert.IsTrue(n >= 3 && n < 7);
                float f = r.NextFloat();
                Assert.IsTrue(f >= 0f && f < 1f);
            }
        }
    }
}
