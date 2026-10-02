namespace MergeLegion.Services
{
    /// <summary>Event and parameter names from the analytics spec. One place so SDK adapters stay consistent.</summary>
    public static class AnalyticsEvents
    {
        public const string TutorialStep = "tutorial_step";
        public const string LevelStart = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelFail = "level_fail";
        public const string Merge = "merge";
        public const string UnitBuy = "unit_buy";
        public const string CommanderSkillUsed = "commander_skill_used";
        public const string AdOffered = "ad_offered";
        public const string AdShown = "ad_shown";
        public const string AdRewarded = "ad_rewarded";
        public const string IapView = "iap_view";
        public const string IapPurchase = "iap_purchase";
        public const string CurrencyEarn = "currency_earn";
        public const string CurrencySpend = "currency_spend";
        public const string ChestOpen = "chest_open";
        public const string MissionComplete = "mission_complete";
        public const string BattlePassTier = "battle_pass_tier";
        public const string ArenaMatch = "arena_match";
        public const string SessionStart = "session_start";
        public const string SessionEnd = "session_end";
    }

    public static class AnalyticsParams
    {
        public const string Step = "step";
        public const string Level = "level";
        public const string Stars = "stars";
        public const string DurationSec = "duration";
        public const string ArmyPower = "army_power";
        public const string Line = "line";
        public const string ToLevel = "to_level";
        public const string Placement = "placement";
        public const string Sku = "sku";
        public const string Price = "price";
        public const string Currency = "currency";
        public const string Amount = "amount";
        public const string Source = "source";
        public const string Sink = "sink";
    }
}
