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
        // Patches.EmbarkBuildings.cs), so the primitive tier is redundant clutter.
        // Internal names verified from the building dump / BepInEx log:
        //   Primitive Forager's Camp   | 小型觅食者营地
        //   Primitive Herbalist's Camp | 小型草药医生营地
        //   Primitive Trapper's Camp   | 小型设阱捕兽者营地
        //   Primitive Fishing Hut      | 小型捕鱼小屋
        //
        // Two layers, deliberately kept separate:
        //
        // 1) META CONTENT (the approach used by the EssentialCamps mod, verified safe):
        //    drop the four names from MB.MetaStateService.Content.buildings while the embark
        //    screen asks for preparation points. This edits the PROFILE-level (meta) content
        //    list only — never the per-settlement ContentState, and never via
        //    GameContentService.Lock() (that one deletes entries from the run's unlocked list
        //    and left settlements unloadable).
        //
        // 2) SHOP QUERY (belt and braces for settlements that were created before this mod ran
        //    and therefore still carry the primitive camps in their own run state):
        //    ConstructionService.ShouldShowInShop(b) is the per-building gate of the build menu,
        //    verified IL: BuildingCategorySlot.ShouldShow = IsCategory && ShouldShowInShop.
        //    The prefix below only returns a boolean — it writes nothing, so it cannot affect
        //    any save.

        private static readonly string[] LowTierCampNames = new string[]
        {
            "Primitive Forager's Camp",
            "Primitive Herbalist's Camp",
            "Primitive Trapper's Camp",
            "Primitive Fishing Hut",
        };

        private static bool _metaCampsCleaned;

        [HarmonyPatch(typeof(MetaPerksService), "GetBasePreparationPoints")]
        [HarmonyPrefix]
        private static void RemoveLowTierCampsFromMetaContent()
        {
            try
            {
                var content = MB.MetaStateService.Content;
                if (content == null || content.buildings == null) return;

                int removed = 0;
                foreach (var name in LowTierCampNames)
                {
                    // v1.0.5: 只从 essentialBuildings（"开局必得/解锁"）里删，不动 buildings
                    // 内容池。内容池是原版"全部内容"的索引，从里面删名字会让引用它的定居点
                    // 解析失败（表现＝定居点无法加载）。菜单不再显示由下面的 ShouldShowInShop
                    // 纯查询补丁保证。
                    if (content.essentialBuildings != null)
                    {
                        while (content.essentialBuildings.Contains(name))
                        {
                            content.essentialBuildings.Remove(name);
                            removed++;
                        }
                    }
                }

                if (removed > 0 || !_metaCampsCleaned)
                {
                    _metaCampsCleaned = true;
                    LogDebug($"FC META-CAMPS: removed {removed} primitive camp entries from meta content");
                }
            }
            catch (Exception ex) { FileLog("ERR meta camps: " + ex.Message); }
        }

        // Harmony binds patch arguments by NAME, and the original parameter is named "model"
        // (not "building"), so positional __0 is used instead — a wrong name is logged as
        // 'Parameter "building" not found in method ...ShouldShowInShop' and turns into a
        // HarmonyException that aborts the patch application.
        [HarmonyPatch(typeof(ConstructionService), "ShouldShowInShop")]
        [HarmonyPrefix]
        private static bool HideLowTierCampsFromShop(BuildingModel __0, ref bool __result)
        {
            try
            {
                if (__0 != null && IsLowTierCamp(__0.Name))
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
