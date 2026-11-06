using System;
using System.Collections.Generic;
using HarmonyLib;
using Eremite;
using Eremite.Buildings;
using Eremite.Controller;
using Eremite.Model;
using Eremite.Model.Effects;
using Eremite.Services;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: Cheats / QoL boosts (ported from Chosen Viceroy of the Forest) ============
        //
        // All values mirror the reference mod's defaults. Implemented WITHOUT a custom HookedEffect:
        //   - hostility: direct HostilityService.AddEffectHostility call at game start
        //   - impatience / woodcutter speed / rewards: Harmony Postfix patches
        //   - storm resolve: directly new a GlobalResolveEffectEffectModel and Apply it

        // All tunable values now live in FCConfig (BepInEx/config/ForestCreator.cfg).

        [HarmonyPatch(typeof(GameController), "StartGame")]
        [HarmonyPostfix]
        private static void ApplyCheatsAtStart()
        {
            try
            {
                if (!GameController.IsGameActive) return;

                // 1) Quell forest hostility at game start.
                try { SO.HostilityService.AddEffectHostility(FCConfig.StartHostility.Value); } catch { }

                // 2) Storm resolve: DISABLED for now — directly newing a GlobalResolveEffectEffectModel
                //    without a full ScriptableObject setup throws "Failed to compare two elements in
                //    the array" (internal perk sorting hits null icon/label). Re-enable later by
                //    finding a built-in resolve cornerstone and Applying it instead.
                // try
                // {
                //     var resolveEffect = new ResolveEffectModel();
                //     resolveEffect.resolve = StormGlobalResolve;
                //     var globalResolve = new GlobalResolveEffectEffectModel();
                //     globalResolve.effect = resolveEffect;
                //     globalResolve.Apply(EffectContextType.None, null, 0);
                // }
                // catch (Exception ex) { Instance.Logger.LogError("FC CHEATS resolve: " + ex.Message); }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR ApplyCheatsAtStart: " + ex.Message);
            }
        }

        // 3) Queens impatience growth greatly reduced.
        [HarmonyPatch(typeof(ReputationService), "GetReputationPenaltySpeed")]
        [HarmonyPostfix]
        private static void PatchImpatienceRate(ref float __result)
        {
            __result *= FCConfig.ImpatienceMultiplier.Value;
        }

        // 4) Woodcutter walking speed bonus.
        [HarmonyPatch(typeof(EffectsService), "GetProfessionBonusSpeed")]
        [HarmonyPostfix]
        private static void PatchWoodcutterWalk(string profession, ref float __result)
        {
            if (profession != null && profession.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0)
                __result += FCConfig.WoodcutterWalkBonus.Value;
        }

        // 5) Woodcutter working speed multiplier.
        [HarmonyPatch(typeof(EffectsService), "GetInternalProductionRate")]
        [HarmonyPostfix]
        private static void PatchWoodcutterWork(Building building, ref float __result)
        {
            try
            {
                if (building == null) return;
                var bm = building.BuildingModel;
                var prof = bm != null ? bm.Profession : null;
                string pname = prof != null ? prof.name : null;
                if (pname != null && pname.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0)
                    __result *= FCConfig.WoodcutterWorkMultiplier.Value;
            }
            catch { }
        }

        // 6) Daily run reward multiplier.
        [HarmonyPatch(typeof(DailyService), "GetRewardsFor")]
        [HarmonyPostfix]
        private static void PatchDailyRewards(ref List<MetaCurrency> __result)
        {
            try
            {
                if (__result == null) return;
                var mult = FCConfig.DailyRewardMultiplier.Value;
                for (int i = 0; i < __result.Count; i++)
                    __result[i] = __result[i] * mult;
            }
            catch { }
        }

        // 7) Regular run reward multiplier.
        [HarmonyPatch(typeof(MetaEconomyService), "GetCurrenciesFor")]
        [HarmonyPostfix]
        private static void PatchRegularRewards(ref List<MetaCurrency> __result)
        {
            try
            {
                if (__result == null) return;
                var mult = FCConfig.RegularRewardMultiplier.Value;
                for (int i = 0; i < __result.Count; i++)
                    __result[i] = __result[i] * mult;
            }
            catch { }
        }

        // Clean up any perks with an empty/null name left over from an earlier broken build.
        // Prefix on PerksService.Sort: we must clean the internal SortedPerks list (List<PerkState>),
        // not the StateService.Effects.perks dictionary, because the comparer reads PerkState.Name.
        [HarmonyPatch(typeof(PerksService), "Sort")]
        [HarmonyPrefix]
        private static void CleanBrokenPerks(PerksService __instance)
        {
            try
            {
                var t = __instance.GetType();
                var field = t.GetField("<SortedPerks>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field == null) return;
                var list = field.GetValue(__instance) as System.Collections.IList;
                if (list == null) return;

                var bad = new List<object>();
                foreach (var ps in list)
                {
                    var nameProp = ps.GetType().GetField("name") ?? ps.GetType().GetField("perkName");
                    string n = nameProp != null ? (nameProp.GetValue(ps) as string) : null;
                    if (string.IsNullOrEmpty(n)) bad.Add(ps);
                }
                foreach (var ps in bad)
                {
                    list.Remove(ps);
                    Instance.Logger.LogInfo("FC: removed broken empty-name PerkState from SortedPerks");
                }
            }
            catch (Exception ex) { Instance.Logger.LogError("FC CLEAN perks: " + ex.Message); }
        }

        // Double the coverage range of hearths (hubRange) and gathering camps (maxDistance).
        [HarmonyPatch(typeof(MainController), "OnServicesReady")]
        [HarmonyPostfix]
        private static void DoubleCoverageRange()
        {
            try
            {
                var settings = MainController.Instance.Settings;
                if (settings == null || settings.Buildings == null) return;
                int hearth = 0, camp = 0;
                foreach (var b in settings.Buildings)
                {
                    if (b is HearthModel hearthModel)
                    {
                        hearthModel.hubRange *= FCConfig.HearthRangeMultiplier.Value;
                        hearth++;
                    }
                    else if (b is CampModel campModel)
                    {
                        campModel.maxDistance *= FCConfig.CampRangeMultiplier.Value;
                        camp++;
                    }
                }
                Instance.Logger.LogInfo($"FC: doubled coverage for {hearth} hearths and {camp} camps");
            }
            catch (Exception ex) { Instance.Logger.LogError("FC COVERAGE: " + ex.Message); }
        }

        // Robot production multiplier: when an Automaton is working, multiply output by 5.
        [HarmonyPatch(typeof(Eremite.Buildings.ProductionCalculator), "GetProductionMultiplier")]
        [HarmonyPostfix]
        private static void Robot_ProductionMultiplier(Eremite.Buildings.Building __0, Eremite.Buildings.RecipeModel __1, Eremite.Characters.Actor __2, float __3, ref int __result)
        {
            try
            {
                if (__2 is Eremite.Characters.Automatons.Automaton)
                {
                    __result = (int)(__result * FCConfig.RobotProductionMultiplier.Value);
                }
            }
            catch { }
        }

        // Robot production speed: double production rate (halve production time) for automatons.
        [HarmonyPatch(typeof(Eremite.Services.EffectsService), "GetProductionRate")]
        [HarmonyPostfix]
        private static void Robot_ProductionRate(Eremite.Buildings.Building __0, Eremite.Characters.Actor __1, Eremite.Buildings.RecipeModel __2, ref float __result)
        {
            try
            {
                if (__1 is Eremite.Characters.Automatons.Automaton)
                {
                    __result *= FCConfig.RobotProductionRateMultiplier.Value;
                }
            }
            catch { }
        }

        // Custom text for automaton description.
        [HarmonyPatch(typeof(Eremite.MB), "GetText", new System.Type[] { typeof(string) })]
        [HarmonyPostfix]
        private static void MB_GetText(string key, ref string __result)
        {
            try
            {
                if (key == "GameUI_WorkerSlot_AutomatonTooltip_Desc")
                {
                    __result = "使用先进的机器人技术代替工人工作，生产时间大幅缩短，产出倍率大幅提升。";
                }
            }
            catch { }
        }
    }
}
