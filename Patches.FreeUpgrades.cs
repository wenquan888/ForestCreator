using System;
using System.Reflection;
using HarmonyLib;
using Eremite;
using Eremite.Buildings;
using Eremite.Buildings.UI;
using Eremite.Buildings.UI.Rainpunk;
using UnityEngine;
using TMPro;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: Free upgrades for Mine and Geyser ============

        [HarmonyPatch(typeof(UpgradableBuilding), "AllGoodsAvailableFor")]
        [HarmonyPrefix]
        private static bool FreeUpgrade_AllGoodsAvailableFor(UpgradableBuilding __instance, ref bool __result)
        {
            if (IsFreeUpgradeBuilding(__instance))
            {
                LogDebug($"FC FREE-UPGRADE: AllGoodsAvailableFor skipped for {__instance.GetType().Name}");
                __result = true;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(UpgradableBuilding), "RemoveGoods")]
        [HarmonyPrefix]
        private static bool FreeUpgrade_RemoveGoods(UpgradableBuilding __instance, int __0, System.Func<int, bool> __1)
        {
            if (IsFreeUpgradeBuilding(__instance))
            {
                LogDebug($"FC FREE-UPGRADE: RemoveGoods skipped for {__instance.GetType().Name}");
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(BuildingUpgradesPanel), "SetUpGoods")]
        [HarmonyPostfix]
        private static void FreeUpgrade_SetUpGoods(BuildingUpgradesPanel __instance)
        {
            try
            {
                var field = AccessTools.Field(typeof(BuildingUpgradesPanel), "building");
                var building = field?.GetValue(__instance) as UpgradableBuilding;
                if (building == null) return;

                // Diagnostic: log the type so we can identify which classes have upgrade panels.
                LogDebug($"FC UPGRADE-PANEL: building={building.name} type={building.GetType().Name}");

                if (!IsFreeUpgradeBuilding(building)) return;

                HideGoodsArea(__instance);
            }
            catch (Exception ex) { Instance.Logger.LogError("FC UPGRADE-PANEL: " + ex.Message); }
        }

        // The upgrade button is only interactable when CanUpgrade() says yes, and CanUpgrade()
        // additionally checks the level/perk slot state. Force it to true so the button stays
        // clickable even when no goods are placed in the panel's goods slots.
        [HarmonyPatch(typeof(UpgradableBuilding), "CanUpgrade")]
        [HarmonyPrefix]
        private static bool FreeUpgrade_CanUpgrade(UpgradableBuilding __instance, ref bool __result)
        {
            if (IsFreeUpgradeBuilding(__instance))
            {
                __result = true;
                return false;
            }
            return true;
        }

        // UpdateContent() runs every frame and re-lays the panel out; re-hide the goods area
        // after it so the "put pipes here" row never comes back.
        [HarmonyPatch(typeof(BuildingUpgradesPanel), "UpdateContent")]
        [HarmonyPostfix]
        private static void FreeUpgrade_UpdateContent(BuildingUpgradesPanel __instance)
        {
            try
            {
                var field = AccessTools.Field(typeof(BuildingUpgradesPanel), "building");
                var building = field?.GetValue(__instance) as UpgradableBuilding;
                if (!IsFreeUpgradeBuilding(building)) return;
                HideGoodsArea(__instance);
            }
            catch { }
        }

        // The unlock button label is built by SetUpUnlockPrice as
        //   MB.GetText("GameUI_RainpunkPanel_UnlockButton") + " (" + count + " " + Good.ToIconString(price) + ")"
        // i.e. "连接管道 (4 <pipe icon>)". Unlocking no longer costs anything, so strip the whole
        // parenthesised price part and keep only the button caption.
        [HarmonyPatch(typeof(BuildingRainpunkPanel), "SetUpUnlockPrice")]
        [HarmonyPostfix]
        private static void StripRainpunkUnlockPrice(BuildingRainpunkPanel __instance)
        {
            try
            {
                var label = __instance.unlockCounter;
                if (label == null) return;
                string s = label.text ?? "";
                int cut = s.IndexOf('(');
                if (cut < 0) cut = s.IndexOf('（');
                if (cut >= 0)
                {
                    label.text = s.Substring(0, cut).TrimEnd();
                }
            }
            catch (Exception ex) { Instance.Logger.LogError("FC STRIP-RAINPUNK-PRICE: " + ex.Message); }
        }

        private static void HideGoodsArea(BuildingUpgradesPanel panel)
        {
            var goodsLayoutComp = AccessTools.Field(typeof(BuildingUpgradesPanel), "goodsLayout")?.GetValue(panel) as Component;
            var noGoodsTextGo = AccessTools.Field(typeof(BuildingUpgradesPanel), "noGoodsText")?.GetValue(panel) as GameObject;
            if (goodsLayoutComp != null && goodsLayoutComp.gameObject.activeSelf) goodsLayoutComp.gameObject.SetActive(false);
            if (noGoodsTextGo != null && noGoodsTextGo.activeSelf) noGoodsTextGo.SetActive(false);
        }

        // ============ FEATURE: free rainpunk unlock (no pipes) ============
        //
        // Verified IL of Workshop.UnlockRainpunk():
        //   GameMB.StorageService.Remove(GetRainpunkUnlockPrice(), model.Name, Id, 8)   <- the pipes
        //   state.rainpunkUnlocked = true
        //   GameMB.GameBlackboardService.OnRainpunkUnlocked.OnNext(this)
        //
        // The unlock price comes from WorkshopModel.rainpunk.unlockPrice (Pipes). We replicate
        // the original body minus the storage charge.
        [HarmonyPatch(typeof(Workshop), "UnlockRainpunk")]
        [HarmonyPrefix]
        private static bool FreeRainpunkUnlock(Workshop __instance)
        {
            try
            {
                __instance.state.rainpunkUnlocked = true;

                // Subject<Workshop>.OnNext lives in UniRx, which this project does not reference,
                // so fire it through reflection instead of touching the type at compile time.
                var bb = GameMB.GameBlackboardService;
                if (bb != null)
                {
                    var prop = AccessTools.Property(bb.GetType(), "OnRainpunkUnlocked");
                    var subject = prop?.GetValue(bb);
                    if (subject != null)
                    {
                        var onNext = AccessTools.Method(subject.GetType(), "OnNext", new[] { typeof(Workshop) });
                        onNext?.Invoke(subject, new object[] { __instance });
                    }
                }
                LogDebug($"FC FREE-RAINPUNK: unlocked for {__instance.model?.Name} without paying pipes");
                return false;
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC FREE-RAINPUNK: " + ex.Message);
                return true;
            }
        }

        // Diagnostic: measure the pipe stock immediately before and after an upgrade click so we
        // can tell whether the click itself charges pipes (upgrade path) or whether they were
        // already taken earlier (panel / other systems).
        [HarmonyPatch(typeof(BuildingUpgradesPanel), "UpgradeClicked")]
        [HarmonyPrefix]
        private static void Diag_PipesBeforeUpgrade()
        {
            try { LogDebug("FC PIPE-BEFORE-UPGRADE: pipes=" + PipeAmount()); }
            catch { }
        }

        [HarmonyPatch(typeof(BuildingUpgradesPanel), "UpgradeClicked")]
        [HarmonyPostfix]
        private static void Diag_PipesAfterUpgrade()
        {
            try { LogDebug("FC PIPE-AFTER-UPGRADE: pipes=" + PipeAmount()); }
            catch { }
        }

        private static string PipeAmount()
        {
            try
            {
                var svc = GameMB.StorageService;
                if (svc == null) return "?";
                return svc.GetAmount("Pipes").ToString();
            }
            catch { return "?"; }
        }

        private static bool IsFreeUpgradeBuilding(UpgradableBuilding b)
        {
            // Every building that can show an upgrade panel is an UpgradableBuilding, so there is
            // no need to whitelist concrete types. Verified class tree (base class in brackets):
            //   House [UpgradableBuilding]
            //   Decoration [UpgradableBuilding]
            //   ProductionBuilding [UpgradableBuilding] ->
            //     Mine, Extractor (geyser pump), Farm, RainCatcher, BlightPost, Institution,
            //     Port, Relic, Workshop, Hearth, Camp, GathererHut, FishingHut
            // So "has an upgrade panel" == "is an UpgradableBuilding".
            return b != null;
        }

        // Sync-upgrade: when one house is upgraded, upgrade all other houses of the same model.
        [HarmonyPatch(typeof(UpgradableBuilding), "Upgrade")]
        [HarmonyPostfix]
        private static void FreeUpgrade_SyncUpgrade(UpgradableBuilding __instance, int __0, int __1)
        {
            try
            {
                if (!(__instance is House house)) return;
                var model = house.BuildingModel;
                if (model == null) return;

                var houses = GameMB.BuildingsService.Houses;
                if (houses == null) return;

                // Reflection: GrantUpgrade(int level, int upgradeIndex) and RiseLevel()
                var grantMethod = AccessTools.Method(typeof(UpgradableBuilding), "GrantUpgrade");
                var riseMethod = AccessTools.Method(typeof(UpgradableBuilding), "RiseLevel");

                int synced = 0;
                foreach (var kv in houses)
                {
                    var other = kv.Value;
                    if (other == null || other == house) continue;
                    if (other.BuildingModel != model) continue;
                    if (other.IsMaxLevelReached()) continue;

                    // Grant the same upgrade and raise level, bypassing CanUpgrade check.
                    grantMethod?.Invoke(other, new object[] { __0, __1 });
                    riseMethod?.Invoke(other, null);
                    synced++;
                }
                if (synced > 0)
                    LogDebug($"FC SYNC-UPGRADE: synced {synced} other houses of model {model.name}");
            }
            catch (Exception ex) { Instance.Logger.LogError("FC SYNC-UPGRADE: " + ex.Message); }
        }
    }
}
