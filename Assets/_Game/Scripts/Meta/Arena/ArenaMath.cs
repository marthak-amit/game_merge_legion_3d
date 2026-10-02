using System;

namespace MergeLegion.Meta.Arena
{
    /// <summary>Elo-style trophy exchange: beating a stronger opponent pays more, losing to a weaker one costs more.</summary>
    public static class ArenaMath
    {
        public static float ExpectedWin(int myTrophies, int theirTrophies, float scale)
        {
            return 1f / (1f + (float)Math.Pow(10.0, (theirTrophies - myTrophies) / scale));
        }

        public static int WinGain(ArenaConfig cfg, int my, int theirs)
        {
            float e = ExpectedWin(my, theirs, cfg.eloScale);
            return Math.Max(cfg.minGain, (int)Math.Round(cfg.trophyK * (1f - e)));
        }

        public static int LossCost(ArenaConfig cfg, int my, int theirs)
        {
            float e = ExpectedWin(my, theirs, cfg.eloScale);
            return Math.Max(cfg.minLoss, (int)Math.Round(cfg.trophyK * e));
        }
    }
}
