using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using Eremite;
using Eremite.Buildings;
using Eremite.Controller;
using Eremite.Characters;
using Eremite.Characters.Behaviours;
using Eremite.Model;
using Eremite.Model.Effects;
using Eremite.Model.Meta;
using Eremite.Model.State;
using Eremite.Services;
using Eremite.Services.Meta;
using Eremite.Buildings.UI;
using Eremite.View.HUD.Construction;
using Eremite.View.Menu.Pick;
using Eremite.View.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: starting preparation points 100 (default 10) ============

        /// <summary>
        /// Raises the base preparation (embark) points from the default 10 to 100.
        /// The pick screen budget, the tooltip and GetBasePreparationPoints() all read
        /// Settings.embarkConfig.preparationPoints, so changing the config keeps them consistent.
        /// </summary>
        [HarmonyPatch(typeof(MainController), "OnServicesReady")]
        [HarmonyPostfix]
        private static void SetStartingPreparationPoints()
        {
            try
            {
                var settings = MainController.Instance.Settings;
                if (settings != null && settings.embarkConfig != null)
                {
                    int oldValue = settings.embarkConfig.preparationPoints;
                    int newValue = FCConfig.EmbarkPreparationPoints.Value;
                    settings.embarkConfig.preparationPoints = newValue;
                    LogDebug($"Starting preparation points changed from {oldValue} to {newValue}.");
                }
                else
                {
                    Instance.Logger.LogError("Could not find embarkConfig to change starting preparation points!");
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("Failed to change starting preparation points: " + ex.Message);
            }
        }

        // ============ FEATURE: starting picks limit 27 (default 9, 1 row -> 3 rows) ============

        /// <summary>
        /// Raises the maximum number of selectable starting items on the embark screen
        /// from the default 9 (1 row) to 27 (3 rows). BuildingsPickScreen.maxInOffer is the
        /// gate for CanPickMore() (buildings + goods + rewards combined).
        /// </summary>
        [HarmonyPatch(typeof(BuildingsPickScreen), "Show")]
        [HarmonyPrefix]
        private static void ExpandStartingPicks(BuildingsPickScreen __instance)
        {
            __instance.maxInOffer = FCConfig.EmbarkMaxPicks.Value;
            LogDebug($"Starting picks limit raised to {__instance.maxInOffer}.");
        }

        // ============ FEATURE: add extra items (Dew/Copper/Fiber/Herb/OceanEssence/Dye) and effects (cart/insect line) to the embark offer ============

        /// <summary>
        /// Adds extra selectable items to the embark "starting bonus" offer.
        ///   goodsOptions   <- 晶露, 铜矿石, 铜条, 植物纤维, 香草, 海洋精华, 染料
        ///   effectsOptions <- 双重运输系统, 昆虫输送线
        /// IMPORTANT: this must NOT run inside EmbarkBonusesGenerator.Generate — that is invoked during
        /// early game init (MetaStateService loading) when the localization/TextsService is not ready,
        /// and touching displayName.Text there deadlocks the main thread (verified: game froze). Instead
        /// we inject here, on BuildingsPickScreen.Show, when all services are ready; then re-render the
        /// offer slots so the new items appear immediately. Internal names are resolved at runtime by
        /// scanning Settings.Goods / Settings.effects and matching the localized display name (Chinese +
        /// English fallback) — never hard-coded, so Settings.GetGood/GetEffect always resolve.
        /// </summary>
        [HarmonyPatch(typeof(BuildingsPickScreen), "Show")]
        [HarmonyPostfix]
        private static void AddExtraEmbarkOffers(BuildingsPickScreen __instance)
        {
            try
            {
                EmbarkBonusesState state = null;
                var prop = typeof(BuildingsPickScreen).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic);
                if (prop != null) state = prop.GetValue(__instance) as EmbarkBonusesState;
                if (state == null)
                {
                    FileLog("EXTRA: state null, skip.");
                    return;
                }

                // ---- diagnostic: log the building options actually read by the embark screen ----
                try
                {
                    var ss = MainController.Instance.Settings;
                    string KindOf(string n)
                    {
                        if (ss == null) return "";
                        try
                        {
                            var b = ss.GetBuilding(n);
                            if (b is CampModel) return "Camp";
                            if (b is GathererHutModel) return "GathererHut";
                            if (b is FishingHutModel) return "FishingHut";
                            if (b is FarmModel) return "Farm";
                        }
                        catch { return "(bld-key?)"; }
                        return "";
                    }
                    string EffTypeOf(string n)
                    {
                        if (ss == null) return "";
                        try { return ss.GetEffect(n)?.GetType().Name ?? "(no-effect)"; } catch { return "(eff-key?)"; }
                    }

                    var bnames = new List<string>();
                    foreach (var o in state.buildingsOptions)
                    {
                        if (o == null) { bnames.Add("(null)"); continue; }
                        string k = KindOf(o.name);
                        bnames.Add(o.name + (k != "" ? "[" + k + "]" : ""));
                    }
                    LogDebug("FC EMBARK-STATE buildingsOptions(" + state.buildingsOptions.Count + "): " + string.Join(", ", bnames));
                    FileLog("FC EMBARK-STATE buildingsOptions(" + state.buildingsOptions.Count + "): " + string.Join(", ", bnames));

                    var eff = new List<string>();
                    foreach (var o in state.effectsOptions)
                    {
                        if (o == null) { eff.Add("(null)"); continue; }
                        string k = KindOf(o.name);
                        eff.Add(o.name + (k != "" ? "[" + k + "]" : "") + "<" + EffTypeOf(o.name) + ">");
                    }
                    LogDebug("FC EMBARK-STATE effectsOptions(" + state.effectsOptions.Count + "): " + string.Join(", ", eff));
                    FileLog("FC EMBARK-STATE effectsOptions(" + state.effectsOptions.Count + "): " + string.Join(", ", eff));

                    var goods = new List<string>();
                    foreach (var o in state.goodsOptions)
                    {
                        if (o == null) { goods.Add("(null)"); continue; }
                        string k = KindOf(o.name);
                        goods.Add(o.name + (k != "" ? "[" + k + "]" : ""));
                    }
                    LogDebug("FC EMBARK-STATE goodsOptions(" + state.goodsOptions.Count + "): " + string.Join(", ", goods));
                    FileLog("FC EMBARK-STATE goodsOptions(" + state.goodsOptions.Count + "): " + string.Join(", ", goods));
                }
                catch (Exception de)
                {
                    Instance.Logger.LogError("FC ERR EMBARK-STATE dump: " + de.Message);
                    FileLog("ERR EMBARK-STATE dump: " + de.Message);
                }

                var settings = MainController.Instance.Settings;
                if (settings == null)
                {
                    FileLog("EXTRA: settings null, skip.");
                    return;
                }

                // ---- remove gather/farm blueprint effects from the embark offer ----
                // The embark pick screen only renders goods + effects slots, so the "采集/种植建筑"
                // the user sees in the starting-bonus panel are EFFECTS whose internal name is a
                // building blueprint (e.g. "Herbalist Camp Blueprint"). Strip the "Blueprint" suffix
                // to resolve the building, and drop it when it is a Camp/GathererHut/FishingHut/Farm.
                // TEMP-DISABLED: re-enable once the diagnostic confirms which effects are gather/farm.
                bool REMOVE_GATHER_FARM_EFFECTS = true;
                if (REMOVE_GATHER_FARM_EFFECTS)
                {
                    try
                    {
                        int removedEff = state.effectsOptions.RemoveAll(o => o != null && IsGatherOrFarmEffect(o.name));
                        if (removedEff > 0)
                            FileLog($"FC REMOVED {removedEff} gather/farm blueprint effects from effectsOptions.");
                    }
                    catch (Exception rex)
                    {
                        FileLog("ERR removing gather/farm effects: " + rex.Message);
                        Instance.Logger.LogError("removing gather/farm effects: " + rex.Message);
                    }
                }

                // ---- resolve the extra goods by localized display name ----
                foreach (var good in settings.Goods)
                {
                    if (good == null) continue;
                    string disp = null;
                    try { disp = good.displayName != null ? good.displayName.Text : null; } catch { }
                    string lower = (disp ?? "").ToLowerInvariant();
                    bool isDew = lower.Contains("晶露") || lower.Contains("露水") || lower.Contains("dew");
                    bool isCopper = lower.Contains("铜矿") || lower.Contains("copper ore") || lower.Contains("copper");
                    bool isCopperBar = lower.Contains("铜条") || lower.Contains("copper bar");
                    bool isFiber = lower.Contains("植物纤维") || lower.Contains("纤维") || lower.Contains("fiber") || lower.Contains("fibre");
                    bool isHerb = lower.Contains("香草") || lower.Contains("herb") || lower.Contains("vanilla");
                    bool isFossil = lower.Contains("海洋精华") || lower.Contains("ocean essence");
                    bool isDye = lower.Contains("染料") || lower.Contains("dye");
                    bool isPipe = lower.Contains("管道") || lower.Contains("pipe");
                    bool isFertilizer = lower.Contains("肥料") || lower.Contains("fertilizer");
                    if ((isDew || isCopper || isCopperBar || isFiber || isHerb || isFossil || isDye || isPipe || isFertilizer) && !HasGoodOption(state, good.Name))
                    {
                        state.goodsOptions.Add(new GoodPickState { name = good.Name, amount = 10, cost = 1 });
                        if (isPipe || isFertilizer)
                            LogDebug($"FC EXTRA-GOOD added: {good.Name} | {disp}");
                    }
                }

                // ---- supply-line effects keep as SELECTABLE (insect line only; double-transport is now direct-injected) ----
                foreach (var effect in settings.effects)
                {
                    if (effect == null) continue;
                    string disp = null;
                    try { disp = effect.displayName != null ? effect.displayName.Text : null; } catch { }
                    string lower = (disp ?? "").ToLowerInvariant();
                    bool isInsectLine = lower.Contains("昆虫输送线") || lower.Contains("昆虫输送") || lower.Contains("insect supply") || lower.Contains("insect line");
                    if (isInsectLine && effect is CompositeEffectModel && !HasEffectOption(state, effect.Name))
                    {
                        state.effectsOptions.Add(new ConditionPickState { name = effect.Name, cost = 1 });
                        FileLog($"EXTRA-EFFECT added(optional): {effect.Name} | {disp}");
                    }
                    // diagnostic: even if the exact name didn't match, log any effect mentioning dye/insect
                    if (lower.Contains("染料") || lower.Contains("dye")) FileLog($"EXTRA-EFFECT[dye?] {effect.Name} | {disp}");
                    if (lower.Contains("昆虫") || lower.Contains("insect")) FileLog($"EXTRA-EFFECT[insect?] {effect.Name} | {disp}");
                }

                // ---- golden cornerstones (搬运车/双重运输/镇静之水/伐木税/过于勤劳/强化斧/强劲之水) ----
                // are now auto-applied directly at game start (see Patches.StartingCornerstones.cs,
                // GameController.StartGame postfix). They are NO LONGER injected into the embark offer.

                // ---- re-render the offer slots so the new items show up right away ----
                var t = __instance.GetType();
                var m1 = t.GetMethod("UpdateGoodsOfferSlots", BindingFlags.Instance | BindingFlags.NonPublic);
                var m2 = t.GetMethod("UpdateRewardsOfferSlots", BindingFlags.Instance | BindingFlags.NonPublic);
                if (m1 != null) m1.Invoke(__instance, null);
                if (m2 != null) m2.Invoke(__instance, null);

                FileLog($"EXTRA done. goodsOptions={state.goodsOptions.Count} effectsOptions={state.effectsOptions.Count}");
            }
            catch (Exception ex)
            {
                FileLog("ERR AddExtraEmbarkOffers: " + ex.Message);
                Instance.Logger.LogError("AddExtraEmbarkOffers failed: " + ex.Message);
            }
        }

        private static bool HasGoodOption(EmbarkBonusesState state, string name)
        {
            foreach (var o in state.goodsOptions)
                if (o != null && o.name == name) return true;
            return false;
        }

        private static bool HasEffectOption(EmbarkBonusesState state, string name)
        {
            foreach (var o in state.effectsOptions)
                if (o != null && o.name == name) return true;
            return false;
        }

        private static bool HasRewardPicked(EmbarkBonusesState state, string name)
        {
            foreach (var o in state.rewardsPicked)
                if (o != null && o.name == name) return true;
            return false;
        }

        /// <summary>
        /// True when an effect's internal name represents a gather/farm building blueprint
        /// (e.g. "Herbalist Camp Blueprint"). Resolution order:
        ///   1) preferred: the effect is a BaseBuildingEffectModel whose `building` is a
        ///      Camp/GathererHut/FishingHut/Farm;
        ///   2) fallback: strip a trailing "Blueprint" suffix and resolve the remainder as a
        ///      building name, then check its type.
        /// </summary>
        private static bool IsGatherOrFarmEffect(string effectName)
        {
            if (string.IsNullOrEmpty(effectName)) return false;
            var settings = MainController.Instance.Settings;
            if (settings == null) return false;

            try
            {
                var e = settings.GetEffect(effectName);
                if (e is BaseBuildingEffectModel bbe && bbe.building != null)
                    return IsGatherOrFarm(bbe.building);
            }
            catch { }

            foreach (var suffix in new[] { " Blueprint", "Blueprint", " blueprint", " blueprint " })
            {
                if (!effectName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                string cand = effectName.Substring(0, effectName.Length - suffix.Length).Trim();
                if (cand.Length == 0) continue;
                try
                {
                    var b = settings.GetBuilding(cand);
                    if (b != null && IsGatherOrFarm(b)) return true;
                }
                catch { }
            }
            return false;
        }
    }
}
