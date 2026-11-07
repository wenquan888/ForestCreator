using System;
using Eremite;
using Eremite.Buildings;
using Eremite.Model;
using Eremite.Services;
using HarmonyLib;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: remove the low-tier (Primitive) gathering camps ============
        //
        // The mod already auto-unlocks the advanced gathering camps at game start (see
        // Patches.EmbarkBuildings.cs), so the primitive tier is redundant clutter. Internal names
        // verified from the building dump / BepInEx log:
        //   Primitive Forager's Camp   | 小型觅食者营地
        //   Primitive Herbalist's Camp | 小型草药医生营地
        //   Primitive Trapper's Camp   | 小型设阱捕兽者营地
        //   Primitive Fishing Hut      | 小型捕鱼小屋
        // Harvester Camp and Stonecutter's Camp have no primitive tier, so nothing to remove there.
        //
        // IMPORTANT — why this is a pure query patch and nothing else:
        //   An earlier version called GameContentService.Lock(model) / removed the names from
        //   ContentState.buildings. That mutates the PERSISTED run state: the next autosave no
        //   longer lists the primitive camps while ContentState.essentialBuildings still does, and
        //   that settlement then refuses to load ("定居点无法加载"). So we never touch state —
        //   we only report these four as locked, which is exactly what the build menu filters on
        //   (every building list is built as Settings.Buildings.Where(IsUnlocked)).

        private static readonly string[] LowTierCampNames = new string[]
        {
            "Primitive Forager's Camp",
            "Primitive Herbalist's Camp",
            "Primitive Trapper's Camp",
            "Primitive Fishing Hut",
        };

        // Short-circuits GameContentService.IsUnlocked so the original list lookup never runs.
        [HarmonyPatch(typeof(GameContentService), "IsUnlocked")]
        [HarmonyPrefix]
        private static bool HideLowTierCamps(BuildingModel building, ref bool __result)
        {
            try
            {
                if (building != null && IsLowTierCamp(building.Name))
                {
                    __result = false;
                    return false;
                }
            }
            catch { }
            return true;
        }

        private static bool IsLowTierCamp(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var n in LowTierCampNames)
                if (name == n) return true;
            return false;
        }
    }
}
