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
        // Hook: ProductionCalculator.GetProductionMultiplier(Building, RecipeModel, Actor, Single)
        // returns the integer output multiplier for one production cycle. We scale that result,
        // which raises the produced amount without touching production time.
        // A result of 0 means "no production this cycle" (failed roll) and is left alone.
        [HarmonyPatch(typeof(ProductionCalculator), "GetProductionMultiplier")]
        [HarmonyPostfix]
        private static void HearthBoostPrimaryProduction(Building building, ref int __result)
        {
            try
            {
                if (__result <= 0) return;
                if (!IsPrimaryGoodProducer(building)) return;

                int multiplier = GetHearthProductionMultiplier();
                if (multiplier <= 1) return;

                __result *= multiplier;
            }
            catch (Exception ex) { FileLog("ERR hearth production: " + ex.Message); }
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
