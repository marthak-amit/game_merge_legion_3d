using System;
using System.Collections.Generic;
using MergeLegion.Meta;
using MergeLegion.Services;
using UnityEngine;

namespace MergeLegion.Monetization
{
    [Serializable]
    public sealed class ProductDef
    {
        public string sku;
        public ProductKind kind;
        public float priceUsd;
        public string titleKey;
        public string descKey;
        public string group;          // gems, bundle, pass, vip, special
        public bool oneTime;
        public int bonusPct;          // shown as "+x% bonus" on gem packs
        public string commanderId;    // commander bundles
        public List<Reward> rewards = new List<Reward>();
    }

    public enum OfferTrigger { LevelReached, LoseStreak, ChapterClear, ReturningPlayer, GemsLow }

    [Serializable]
    public sealed class OfferDef
    {
        public string id;
        public string sku;
        public OfferTrigger trigger;
        public int triggerValue;
        public float durationHours = 48f;
        public float cooldownHours = 24f;
        public int maxActivations = 1;     // 0 = unlimited
        public int discountPct;
        public string titleKey;
    }

    [Serializable]
    public sealed class PiggyConfig
    {
        public int gemsPerWin = 3;
        public int gemsPerLoss = 1;
        public int capGems = 1500;
        public int minBreakGems = 100;
        public string sku = "piggy_bank";
    }

    [Serializable]
    public sealed class VipConfig
    {
        public string sku = "vip_weekly";
        public int days = 7;
        public int dailyGems = 50;
    }

    [Serializable]
    public sealed class BattlePassTierDef
    {
        public Reward free;
        public Reward premium;
    }

    [Serializable]
    public sealed class BattlePassConfig
    {
        public int seasonDays = 30;
        public int baseXp = 100;
        public int xpStep = 10;
        public string premiumSku = "battle_pass_premium";
        public List<BattlePassTierDef> tiers = new List<BattlePassTierDef>();
    }

    [Serializable]
    public sealed class EventTrackEntry
    {
        public int tokens;
        public Reward reward;
    }

    /// <summary>Weekend event definition. Delivered by Remote Config (weekend_event_json) so events need no app update.</summary>
    [Serializable]
    public sealed class EventConfig
    {
        public string id = "weekend";
        public string nameKey = "event.weekend";
        public string tokenKey = "event.tokens";
        public string startUtc;          // ISO 8601; empty = recurring weekend (Sat 00:00 - Mon 00:00 local)
        public string endUtc;
        public int levelCount = 30;
        public float difficulty = 1f;    // x the campaign power curve at the player's level
        public int tokensPerStar = 2;
        public int minPlayerLevel = 5;
        public List<EventTrackEntry> track = new List<EventTrackEntry>();
    }

    [Serializable]
    public sealed class MonetizationConfig
    {
        public List<ProductDef> products = new List<ProductDef>();
        public List<OfferDef> offers = new List<OfferDef>();
        public PiggyConfig piggy = new PiggyConfig();
        public VipConfig vip = new VipConfig();
        public BattlePassConfig battlePass = new BattlePassConfig();
        public EventConfig weekendEvent = new EventConfig();

        public ProductDef Product(string sku)
        {
            for (int i = 0; i < products.Count; i++)
                if (products[i].sku == sku) return products[i];
            return null;
        }

        public OfferDef Offer(string id)
        {
            for (int i = 0; i < offers.Count; i++)
                if (offers[i].id == id) return offers[i];
            return null;
        }

        public static MonetizationConfig FromJson(string json, string overrideJson = null)
        {
            var cfg = new MonetizationConfig();
            if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, cfg);
            if (!string.IsNullOrEmpty(overrideJson))
            {
                try { JsonUtility.FromJsonOverwrite(overrideJson, cfg); }
                catch (Exception e) { Debug.LogWarning("[Config] ignoring bad monetization override: " + e.Message); }
            }
            return cfg;
        }

        public static MonetizationConfig Load(IRemoteConfigService remote)
        {
            var asset = Resources.Load<TextAsset>("Config/monetization_config");
            string over = remote != null ? remote.GetString(RemoteKeys.MonetizationOverride, "") : "";
            return FromJson(asset != null ? asset.text : null, over);
        }
    }
}
