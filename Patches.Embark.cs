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
                    settings.embarkConfig.preparationPoints = 100;
                    Instance.Logger.LogInfo($"Starting preparation points changed from {oldValue} to 100.");
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
            __instance.maxInOffer = 27;
            Instance.Logger.LogInfo($"Starting picks limit raised to {__instance.maxInOffer} (3 rows).");
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

                var settings = MainController.Instance.Settings;
                if (settings == null)
                {
                    FileLog("EXTRA: settings null, skip.");
                    return;
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
                    if ((isDew || isCopper || isCopperBar || isFiber || isHerb || isFossil || isDye) && !HasGoodOption(state, good.Name))
                        state.goodsOptions.Add(new GoodPickState { name = good.Name, amount = 10, cost = 1 });
                }

                // ---- resolve the cart buff / supply-line effects by localized display name ----
                foreach (var effect in settings.effects)
                {
                    if (effect == null) continue;
                    string disp = null;
                    try { disp = effect.displayName != null ? effect.displayName.Text : null; } catch { }
                    string lower = (disp ?? "").ToLowerInvariant();
                    bool isCart = lower.Contains("双重运输系统") || lower.Contains("双重运输") || lower.Contains("double transport");
                    bool isInsectLine = lower.Contains("昆虫输送线") || lower.Contains("昆虫输送") || lower.Contains("insect supply") || lower.Contains("insect line");
                    if ((isCart || isInsectLine) && !HasEffectOption(state, effect.Name))
                    {
                        state.effectsOptions.Add(new ConditionPickState { name = effect.Name, cost = 1 });
                        FileLog($"EXTRA-EFFECT added: {effect.Name} | {disp}");
                    }
                    // diagnostic: even if the exact name didn't match, log any effect mentioning dye/insect
                    if (lower.Contains("染料") || lower.Contains("dye")) FileLog($"EXTRA-EFFECT[dye?] {effect.Name} | {disp}");
                    if (lower.Contains("昆虫") || lower.Contains("insect")) FileLog($"EXTRA-EFFECT[insect?] {effect.Name} | {disp}");
                }

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
    }
}
