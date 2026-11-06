using System;
using System.Reflection;
using HarmonyLib;
using Eremite;
using Eremite.Buildings;
using Eremite.Buildings.UI;
using UnityEngine;

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
                Instance.Logger.LogInfo($"FC FREE-UPGRADE: AllGoodsAvailableFor skipped for {__instance.GetType().Name}");
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
                Instance.Logger.LogInfo($"FC FREE-UPGRADE: RemoveGoods skipped for {__instance.GetType().Name}");
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

                // Diagnostic: log the type so we can identify the geyser class name.
                Instance.Logger.LogInfo($"FC UPGRADE-PANEL: building={building.name} type={building.GetType().Name}");

                if (!IsFreeUpgradeBuilding(building)) return;

                // Match the reference mod exactly: goodsLayout is a Component, noGoodsText is a GameObject.
                // Both are hidden (SetActive(false)).
                var goodsLayoutComp = AccessTools.Field(typeof(BuildingUpgradesPanel), "goodsLayout")?.GetValue(__instance) as Component;
                var noGoodsTextGo = AccessTools.Field(typeof(BuildingUpgradesPanel), "noGoodsText")?.GetValue(__instance) as GameObject;
                if (goodsLayoutComp != null) goodsLayoutComp.gameObject.SetActive(false);
                if (noGoodsTextGo != null) noGoodsTextGo.SetActive(false);
            }
            catch (Exception ex) { Instance.Logger.LogError("FC UPGRADE-PANEL: " + ex.Message); }
        }

        private static bool IsFreeUpgradeBuilding(UpgradableBuilding b)
        {
            if (b == null) return false;
            if (b is Mine) return true;
            if (b is Extractor) return true; // 间歇泉泵
            if (b is House) return true;    // 所有种族住房
            return false;
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
                    Instance.Logger.LogInfo($"FC SYNC-UPGRADE: synced {synced} other houses of model {model.name}");
            }
            catch (Exception ex) { Instance.Logger.LogError("FC SYNC-UPGRADE: " + ex.Message); }
        }
    }
}
