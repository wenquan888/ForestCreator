using System;
using Eremite;
using Eremite.Buildings;
using Eremite.Services;
using HarmonyLib;
using UnityEngine;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: hearth level boosts raw-good production ============
        //
        // Ancient Hearth level 1 / 2 / 3 multiplies the output of buildings that produce
        // "primary" goods, i.e. things you take straight off the map by gathering or farming:
        //   Camp (woodcutter camp & other camps), GathererHut, FishingHut, Farm, RainCatcher.
        //
        // Hook note (verified IL): GetProductionMultiplier only returns the 1/2/3 crit
        // "extra production" factor and is NOT the amount path. The produced amount comes from
        //   ProductionCalculator.GetResult(building, good, recipe, isExtra)
        //     good = good + GetRawProductionBonusFor(good.name)
        //     good = good * GetProductionFactor(building, recipe, isExtra)
        //     good.Clamp()
        // where GetProductionFactor = 1 + buildingBonus + buildingExtraBonus + recipeBonus.
        // Scaling GetProductionFactor therefore scales the final amount.
        // Harmony binds patch arguments by NAME; the parameter of
        // GetProductionFactor(Building, RecipeModel, Boolean) is not guaranteed to be called
        // "building", so positional __0 is used. A wrong name throws
        // 'Parameter "..." not found in method' and aborts patch application.
        [HarmonyPatch(typeof(ProductionCalculator), "GetProductionFactor")]
        [HarmonyPostfix]
        private static void HearthBoostPrimaryProduction(Building __0, ref float __result)
        {
            try
            {
                if (__result <= 0f) return;
                if (!IsPrimaryGoodProducer(__0)) return;

                int multiplier = GetHearthProductionMultiplier();
                if (multiplier <= 1) return;

                __result *= multiplier;
                DebugLogProduction(__0, multiplier);
            }
            catch (Exception ex) { FileLog("ERR hearth production: " + ex.Message); }
        }

        private static int _hearthProductionLogBudget = 24;

        [System.Diagnostics.Conditional("DEBUG")]
        private static void DebugLogProduction(Building building, int multiplier)
        {
            if (_hearthProductionLogBudget <= 0) return;
            _hearthProductionLogBudget--;
            LogDebug($"FC HEARTH-PROD: {building?.ModelName} hearthLevel={GetAncientHearthLevel()} x{multiplier}");
        }

        private static bool IsPrimaryGoodProducer(Building building)
        {
            if (building == null) return false;
            return building is Camp
                || building is GathererHut
                || building is FishingHut
                || building is Farm
                || building is RainCatcher;
        }

        private static int GetHearthProductionMultiplier()
        {
            int level = GetAncientHearthLevel();
            if (level >= 3) return FCConfig.HearthLevel3ProductionMultiplier.Value;
            if (level == 2) return FCConfig.HearthLevel2ProductionMultiplier.Value;
            return FCConfig.HearthLevel1ProductionMultiplier.Value;
        }

        // Level of the Ancient Hearth; falls back to the highest level among all hearths.
        private static int GetAncientHearthLevel()
        {
            try
            {
                var bs = GameMB.BuildingsService;
                if (bs == null) return 1;

                var main = bs.GetMainHearth();
                if (main != null) return Mathf.Max(1, main.GetCurrentLevel());

                int best = 0;
                if (bs.Hearths != null)
                {
                    foreach (var kv in bs.Hearths)
                    {
                        var h = kv.Value;
                        if (h == null) continue;
                        best = Mathf.Max(best, h.GetCurrentLevel());
                    }
                }
                return Mathf.Max(1, best);
            }
            catch { return 1; }
        }

        // ============ FEATURE: decorations raise every race's resolve ============
        //
        // Each placed Decoration adds resolve to all races by its footprint and tier:
        //   tier.order 0 (green) 1 / order 1 (blue) 2 / order 2 (gold) 3 per tile.
        // GetRoundedResolveFor() is just RoundToIntMath(GetResolveFor()), so patching
        // GetResolveFor covers both the raw value and the displayed number.
        [HarmonyPatch(typeof(ResolveService), "GetResolveFor")]
        [HarmonyPostfix]
        private static void DecorationResolveBonus(ref float __result)
        {
            try
            {
                int bonus = GetDecorationResolveBonus();
                if (bonus > 0) __result += bonus;
            }
            catch (Exception ex) { FileLog("ERR decoration resolve: " + ex.Message); }
        }

        private static int GetDecorationResolveBonus()
        {
            try
            {
                var bs = GameMB.BuildingsService;
                if (bs == null || bs.Decorations == null) return 0;

                int total = 0;
                foreach (var kv in bs.Decorations)
                {
                    var dec = kv.Value as Decoration;
                    if (dec == null) continue;
                    var dm = dec.BuildingModel as DecorationModel;
                    if (dm == null) continue;

                    int tiles = Mathf.Max(1, dm.size.x * dm.size.y);
                    int perTile = FCConfig.DecorationGreenResolvePerTile.Value;
                    if (dm.tier != null)
                    {
                        switch (dm.tier.order)
                        {
                            case 1: perTile = FCConfig.DecorationBlueResolvePerTile.Value; break;
                            case 2: perTile = FCConfig.DecorationGoldResolvePerTile.Value; break;
                            default: perTile = FCConfig.DecorationGreenResolvePerTile.Value; break;
                        }
                    }
                    total += tiles * perTile;
                }
                return total;
            }
            catch { return 0; }
        }
    }
}
