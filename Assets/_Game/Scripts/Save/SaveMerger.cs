namespace MergeLegion.Save
{
    public enum MergeChoice { UseLocal, UseCloud }

    public readonly struct MergeDecision
    {
        public readonly MergeChoice Choice;
        /// <summary>True when both sides hold real, different progress: UI must let the player confirm.</summary>
        public readonly bool PromptRequired;

        public MergeDecision(MergeChoice choice, bool promptRequired)
        {
            Choice = choice; PromptRequired = promptRequired;
        }
    }

    /// <summary>Cloud/local conflict policy: higher progress wins; the player is prompted when both have progress.</summary>
    public static class SaveMerger
    {
        public static MergeDecision Resolve(SaveData local, SaveData cloud)
        {
            if (cloud == null) return new MergeDecision(MergeChoice.UseLocal, false);
            if (local == null) return new MergeDecision(MergeChoice.UseCloud, false);

            long l = local.ProgressScore, c = cloud.ProgressScore;

            if (l == c)
            {
                var newer = cloud.lastSavedUtcTicks > local.lastSavedUtcTicks ? MergeChoice.UseCloud : MergeChoice.UseLocal;
                return new MergeDecision(newer, false);
            }

            var winner = c > l ? MergeChoice.UseCloud : MergeChoice.UseLocal;
            bool bothHaveProgress = l > 0 && c > 0;
            return new MergeDecision(winner, bothHaveProgress);
        }
    }
}
