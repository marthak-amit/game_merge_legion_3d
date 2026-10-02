# Balance notes and targets (Phase 10)

## Method
`Tests/EditMode/PlayerSimulator.cs` plays the real services headlessly: it buys units, merges, spends spare coins on research,
fights each level (no skill, no ads, no smart play) and retries on defeat. The campaign curve
(`CampaignGenParams`: start power 110, +3 % per level, bosses x1.25) was tuned against it.

| Metric | Result |
|---|---|
| Baseline player (no commander skill, no ads, naive merging) | clears levels 1-189 first try, wall at the level 190 boss |
| Fight length (sim) | ~9 s average over levels 1-50, 11 s at L100, 18 s at L150, 29 s at L180 (+30-40 s of buy/merge in PREPARE) |
| Battle cost (240 units) | ~0.1 ms per 30 Hz tick, zero allocations once warm (tests enforce < 96 KB over 300 ticks) |

The player has levers the simulator ignores (commander active skills, commander shards, rewarded 3x/revive/free unit,
castle income, VIP), so real players should clear the campaign; endless starts at the end-of-campaign power (`endless.basePower`)
and grows 12 %/wave, so it ends where a maxed army ends. Useful levers: `tools/gen_balance.py: HP_MULT` (fight length),
`CampaignGenParams.growth` (difficulty slope), `rewards.*`, `research.*` (sink), `castle.*` (idle income).

Known design facts: the buy-cost curve (base x 1.15^n per line) makes a level-8 unit roughly 17 billion coins of buys, so
top-end armies are research-limited, not unit-limited; the simulator's armies top out at level-7 units.
The grid-full deadlock is avoided by letting a purchase merge instantly with a level-1 unit when the grid is full.

## Revenue model (to be validated with live data)
Target: ~US$13.5k gross/month -> ~US$12k net (~INR 10 lakh).
* 3,000 US DAU x ARPDAU US$0.15 x 30 = US$13.5k.
* Ads (60 %, ~US$0.09/DAU): ~4 rewarded views (win 3x, chest, spin, free unit) at ~US$18 eCPM = US$0.07, plus ~2 interstitials at ~US$10 eCPM = US$0.02.
* IAP (40 %, ~US$0.06/DAU): 2 % payers x ~US$3 ARPPDAU. Starter pack (US$1.99, level 5, 48 h), battle pass (US$4.99), VIP (US$4.99/week) and piggy bank (US$2.99) are the conversion drivers.
* Retention levers: first-30-second hook with no ads, daily login/spin/chests/castle, 3 missions/day, weekend event, arena.

These are planning numbers, not forecasts. After soft launch, read: `tutorial_step` funnel, D1/D7/D30 by cohort,
`ad_offered -> ad_shown -> ad_rewarded` per placement, `iap_view -> iap_purchase` per SKU, `level_fail` hot spots, then tune through Remote Config.
