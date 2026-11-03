using System;
using HarmonyLib;
using Eremite.Buildings;
using Eremite.Characters;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: robots get the rainpunk-engine production bonus ============
        //
        // Production chain (verified via Cecil):
        //   ProductionBuilding.GetProductionMultiplier(worker, recipe, dt)
        //     -> EffectsService.GetProductionMultiplier(building, recipe, worker, ...)
        //     -> ProductionCalculator.GetProductionMultiplier(building, recipe, worker, ...)
        //
        // ProductionCalculator.GetProductionMultiplier does:
        //   if roll(GetNoProductionChance) -> 0
        //   if !worker.CanCritProduction  -> return 1   <-- robots (Automatons) have
        //                                                     CanCritProduction == false, so
        //                                                     they exit here
        //   extra = ... + GetBuildingExtraProductionChance(building)  // rainpunk engine bonus
        //   return 1 + (int)extra
        //
        // GetBuildingExtraProductionChance(building) reads
        //   building.UpgradableState.chanceForExtraProductionBonus, which is where the
        //   rainpunk-engine perks are applied. Because robots never reach it, a robot-worked
        //   building always outputs with multiplier == 1 and the engine bonus is lost.
        //
        // Fix: as a Postfix, when the worker is a robot (CanCritProduction == false) and the
        // vanilla result was the flat 1, add the building-level extra production (rainpunk
        // engine) -- but do NOT grant crit, preserving the robot's no-crit behaviour.

        [HarmonyPatch(typeof(ProductionCalculator), "GetProductionMultiplier")]
        [HarmonyPostfix]
        private static void RobotGetsEngineBonus(ProductionCalculator __instance, Building building, Actor actor, ref int __result)
        {
            try
            {
                // Only boost the robot path: no crit means an automated worker; skip crit workers.
                if (actor == null || actor.CanCritProduction) return;
                // Vanilla returns 0 on a no-production roll (keep that) or 1 here (boost only 1).
                if (__result != 1) return;

                float extra = 0f;
                var us = building != null ? building.UpgradableState : null;
                if (us != null) extra = us.chanceForExtraProductionBonus;

                __result = 1 + (int)extra;
                // Replicate the vanilla rounding of the fractional part.
                float frac = extra - (int)extra;
                if (frac > 0f && UnityEngine.Random.value < frac) __result++;
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR RobotGetsEngineBonus: " + ex.Message);
            }
        }
    }
}
