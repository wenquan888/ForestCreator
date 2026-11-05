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

        private const float ImpatienceMultiplier = 0.1337f;
        private const float WoodcutterWalkBonus = 13.37f;
        private const float WoodcutterWorkMultiplier = 13.37f;
        private const int StormGlobalResolve = 8;
        private const float RewardMultiplier = 13.37f;
        private const int StartHostility = -1337;

        [HarmonyPatch(typeof(GameController), "StartGame")]
        [HarmonyPostfix]
        private static void ApplyCheatsAtStart()
        {
            try
            {
                if (!GameController.IsGameActive) return;

                // 1) Quell forest hostility at game start.
                try { SO.HostilityService.AddEffectHostility(StartHostility); } catch { }

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
            __result *= ImpatienceMultiplier;
        }

        // 4) Woodcutter walking speed bonus.
        [HarmonyPatch(typeof(EffectsService), "GetProfessionBonusSpeed")]
        [HarmonyPostfix]
        private static void PatchWoodcutterWalk(string profession, ref float __result)
        {
            if (profession != null && profession.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0)
                __result += WoodcutterWalkBonus;
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
                    __result *= WoodcutterWorkMultiplier;
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
                for (int i = 0; i < __result.Count; i++)
                    __result[i] = __result[i] * RewardMultiplier;
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
                for (int i = 0; i < __result.Count; i++)
                    __result[i] = __result[i] * RewardMultiplier;
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
    }
}
