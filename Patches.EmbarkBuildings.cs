using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Eremite;
using Eremite.Buildings;
using Eremite.Controller;
using Eremite.Model;
using Eremite.Model.State;
using Eremite.Services;
using Eremite.Services.Meta;
using HarmonyLib;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: Embark building options ============
        //
        // Two related changes:
        //   1) REMOVE all gathering buildings (camps, gatherer huts, fishing huts) and all planting
        //      buildings (farms / grove / greenhouse) from the embark "which buildings to bring"
        //      offer list, so they no longer clutter / consume embark picks.
        //   2) AUTO-UNLOCK those buildings at game start, so they are directly buildable without
        //      being picked at embark or drawn as a blueprint.
        //
        // IMPORTANT (v1.0.5 fix): the auto-unlock must NEVER unlock a building the game does not
        // put in the normal build menu. `Settings.Buildings` also contains placeholder / ruined /
        // event-only entries (Homestead 自耕农场, Farmfield 田地, Ruined Grove, Ruined Greenhouse,
        // 各种 "… (no reward)" 变体). Unlocking one of those makes it buildable, and building it
        // writes data the game cannot reload — which the player sees as a corrupted settlement.
        //
        // The gate that actually decides "is this a normal, buildable building" is
        // BuildingModel.isInShop — verified from ConstructionService.ShouldShowInShop IL:
        //     ShouldShowInShop(model) = model.isInShop || DebugModes.Construction
        // A building that is not in the shop is not part of the normal techtree, so we skip it.

        private const string PathGameContentServiceIsUnlocked = "IsUnlocked";

        [HarmonyPatch(typeof(MetaStateService), "CreateEmbarkBonuses")]
        [HarmonyPostfix]
        private static void FilterEmbarkBuildingOptions(ref EmbarkBonusesState __result)
        {
            try
            {
                if (__result == null || __result.buildingsOptions == null) return;
                var settings = MainController.Instance.Settings;
                if (settings == null) return;

                int before = __result.buildingsOptions.Count;
                var removed = new List<string>();
                __result.buildingsOptions.RemoveAll(opt =>
                {
                    if (opt == null || string.IsNullOrEmpty(opt.name)) return false;
                    var b = settings.GetBuilding(opt.name);
                    if (b != null && IsAutoUnlockCandidate(b)) { removed.Add(opt.name); return true; }
                    return false;
                });
                LogDebug($"FC EMBARK-FILTER: CreateEmbarkBonuses fired. buildingsOptions {before} -> {__result.buildingsOptions.Count}; removed: {string.Join(", ", removed)}");
                FileLog($"FC EMBARK-FILTER: CreateEmbarkBonuses fired. buildingsOptions {before} -> {__result.buildingsOptions.Count}; removed: {string.Join(", ", removed)}");
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR FilterEmbarkBuildingOptions: " + ex.Message);
                FileLog("ERR FilterEmbarkBuildingOptions: " + ex.Message);
            }
        }

        // Gathering = camps + gatherer huts + fishing huts; Planting = farms / grove / greenhouse.
        // NOTE: the model classes are *Model (GathererHutModel / FishingHutModel / CampModel /
        // FarmModel) which all derive from UpgradableBuildingModel : BuildingModel. The bare
        // GathererHut / FishingHut types are runtime View MonoBehaviours and must NOT be used here.
        private static bool IsGatherOrFarm(BuildingModel b)
        {
            return b is CampModel || b is GathererHutModel || b is FishingHutModel || b is FarmModel;
        }

        // Grove (护林人的小屋) and the Greenhouse (温室) do not use FarmModel, so they are caught
        // by the display-name fallback.
        private static readonly string[] PlantingDisplayKeywords = new string[]
        {
            "温室", "护林", "greenhouse", "forester", "grove",
        };

        private static bool IsPlantingByDisplayName(BuildingModel b)
        {
            try
            {
                string d = b.displayName != null ? b.displayName.Text : null;
                if (string.IsNullOrEmpty(d)) return false;
                string lower = d.ToLowerInvariant();
                foreach (var kw in PlantingDisplayKeywords)
                    if (lower.Contains(kw)) return true;
            }
            catch { }
            return false;
        }

        // Buildings that exist in Settings.Buildings but must NOT be written into
        // MetaSave.content.essentialBuildings. Homestead 曾经被当成"半成品"排除，但运行时侦察
        // 显示它其实是完整资源：
        //     type=FarmModel  isInShop=True  size=(3,3)
        //     farmfieldModel=Farmfield  prefab=PRESENT  recipes=4  workplaces=4
        // 也就是说游戏本身就认为它属于建造商店（ShouldShowInShop = isInShop），之前菜单里看不到
        // 纯粹是这里的黑名单把它藏了。历史上那次"建完读档失败"的真正元凶是 essentialBuildings
        // 被重复追加到 535 项（Trapper's Camp x190），不是这栋建筑本身。
        //
        // 所以对 Homestead 的放行只走 QueryOnlyUnlock（纯查询层），仍然不写入 essentialBuildings，
        // 元存档保持干净。
        //
        // 注意 Farmfield（田地）在原版 essentialBuildings 里本来就有，属于正常内容，
        // 不能排除——否则会破坏农田机制。
        private static readonly string[] NonShopBuildingNames = new string[]
        {
            "Homestead",            // 自耕农场 —— 只允许查询层放行，不写入 essentialBuildings
        };

        // 只在"显示/解锁"两个纯查询点放行的建筑，永远不会被写进 MetaSave。
        private static readonly string[] QueryOnlyUnlockNames = new string[]
        {
            "Homestead",
        };

        private static bool IsQueryOnlyUnlock(BuildingModel b)
        {
            if (b == null) return false;
            string n = b.Name ?? "";
            foreach (var q in QueryOnlyUnlockNames)
                if (string.Equals(n, q, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsDeniedBuilding(BuildingModel b)
        {
            if (b == null) return true;
            string n = b.Name ?? "";
            foreach (var denied in NonShopBuildingNames)
                if (string.Equals(n, denied, StringComparison.Ordinal)) return true;

            // Ruined ... / "... (no reward)"：地图遗迹或事件建筑，不是玩家能造的
            if (n.StartsWith("Ruined", StringComparison.Ordinal)) return true;
            if (n.EndsWith("(no reward)", StringComparison.Ordinal)) return true;
            // 注意：Hallowed 变体（受祝福的小农场 / 草药园）属于正常可建造内容，不能排除，
            // 上一版在这里误杀过一次，导致种植菜单缺项。
            return false;
        }

        // v1.0.5b: isInShop 不能当过滤器用。
        // 实测：绝大多数正常种植建筑（SmallFarm / Plantation / Herb Garden / Grove /
        // Greenhouse Workshop）的 isInShop 都是 false，只有 Farmfield、Field Kitchen 少数几项
        // 为 true。用它做正向闸门会把整个种植菜单只剩两项。
        // 这里改为"黑名单排除"：采集/种植类一律放行，只拦掉确认不可建造的那几个。
        private static bool IsAutoUnlockCandidate(BuildingModel b)
        {
            if (b == null) return false;
            if (IsDeniedBuilding(b)) return false;
            if (b.Name == "Biome Grass Spawner") return true;   // 沃土
            return IsGatherOrFarm(b) || IsPlantingByDisplayName(b);
        }

        // 纯查询层隐藏：在建造菜单里把黑名单建筑过滤掉。只返回布尔，不写任何存档状态，
        // 因此不可能影响定居点读档（与 Patches.LowTierCamps 的同类补丁互不冲突）。
        [HarmonyPatch(typeof(ConstructionService), "ShouldShowInShop")]
        [HarmonyPrefix]
        private static bool HideNonShopBuildings(BuildingModel __0, ref bool __result)
        {
            try
            {
                if (__0 == null) return true;
                // Homestead：不拦，走原版逻辑（isInShop=True -> 显示在建造菜单）
                if (IsQueryOnlyUnlock(__0)) return true;
                if (IsDeniedBuilding(__0))
                {
                    __result = false;
                    return false;
                }
            }
            catch { }
            return true;
        }

        // ============ 让 QueryOnlyUnlock 建筑进入"本局已解锁池" ============
        //
        // 建造菜单的项来自 GameContentService.Buildings，它在服务加载时由 CacheProperties()
        // 从 State.buildings（本局 ContentState 的名字列表）映射出来：
        //     Buildings.Value = State.buildings.Select(Settings.GetBuilding).OrderBy(Order).ToList()
        // 老存档的 State.buildings 是开局那一刻生成的名单，里面本来就没有 Homestead，
        // 所以无论怎么在解锁判定上放行，菜单都看不到它。
        //
        // 这里在服务加载完成后把名字补进内存列表并重建池：
        //   - 只改内存里的名字列表，不写 MetaSave；
        //   - 名字本身在 MetaSave.content.buildings 里已经存在（侦察：Homestead 出现 1 次），
        //     属于合法内容名，因此即便随定居点存档落盘也不会解析失败；
        //   - 与"每局 append 到 essentialBuildings"那种膨胀写法完全不同。
        [HarmonyPatch(typeof(GameContentService), "OnLoading")]
        [HarmonyPostfix]
        private static void InjectQueryOnlyBuildings(GameContentService __instance)
        {
            try
            {
                _fcContentSvc = __instance;

                // Register the custom Town Hall here rather than in Awake: it copies a vanilla
                // building's prefab, and Settings are only readable from this point on.
                if (!_fcTownHallRegistered && !_fcTownHallFailed) CreateTownHall();

                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                var state = __instance != null ? __instance.State : null;
                if (settings == null || state == null || state.buildings == null) return;

                bool changed = false;
                var added = new List<string>();
                foreach (var name in QueryOnlyUnlockNames)
                {
                    var model = settings.GetBuilding(name);
                    if (model == null) continue;
                    if (state.buildings.Contains(name)) continue;
                    state.buildings.Add(name);
                    added.Add(name);
                    changed = true;
                }

                if (changed)
                {
                    __instance.CacheProperties();
                    FileLog($"FC POOL INJECT: added {string.Join(", ", added)}; State.buildings={state.buildings.Count}");
                }
                else
                {
                    LogDebug("FC POOL INJECT: already present, nothing to add.");
                }

                // Custom ATS_API buildings need the same treatment: they exist in Settings but are
                // not part of the settlement's building pool, so the build menu would never list them.
                InjectTownHallIntoPool(__instance);
            }
            catch (Exception ex)
            {
                FileLog("ERR POOL INJECT: " + ex.Message);
            }
        }

        // ============ FEATURE: auto-unlock buildings at game start ============

        // Verified against the EssentialCamps reference: to make buildings directly buildable at
        // game start you add their internal names to MetaStateService.Content.essentialBuildings
        // (and Content.buildings). GameContentService.EnsureBuildings() then puts them into the
        // unlocked Buildings pool, so they show up in the building menu without needing a blueprint
        // or an embark pick.
        //
        // v1.0.5: the list is now de-duplicated before use. Earlier builds appended on every call,
        // which ballooned MetaSave to 535 entries ("Trapper's Camp" x190, "Herbalist's Camp" x190).
        private static void DedupeNames(List<string> list)
        {
            if (list == null) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int w = 0;
            for (int r = 0; r < list.Count; r++)
            {
                string s = list[r];
                if (s == null || !seen.Add(s)) continue;
                list[w++] = s;
            }
            if (w < list.Count) list.RemoveRange(w, list.Count - w);
        }

        [HarmonyPatch(typeof(MetaPerksService), "GetBasePreparationPoints")]
        [HarmonyPrefix]
        private static void AddAutoUnlockedBuildings()
        {
            try
            {
                var content = MB.MetaStateService.Content;
                if (content == null)
                {
                    Instance.Logger.LogError("FC AUTOUNLOCK: Content is null, skip.");
                    return;
                }
                var settings = MainController.Instance.Settings;
                var added = new List<string>();
                int deduped = 0;

                if (content.buildings != null)
                {
                    int before = content.buildings.Count;
                    DedupeNames(content.buildings);
                    deduped += before - content.buildings.Count;
                }
                if (content.essentialBuildings != null)
                {
                    int before = content.essentialBuildings.Count;
                    DedupeNames(content.essentialBuildings);
                    deduped += before - content.essentialBuildings.Count;
                }

                var want = new List<string>();

                // 1) buildings that really exist in this game build and really belong to the shop
                if (settings != null && settings.Buildings != null)
                {
                    foreach (var b in settings.Buildings)
                    {
                        if (b == null || string.IsNullOrEmpty(b.Name)) continue;
                        if (!IsAutoUnlockCandidate(b)) continue;
                        want.Add(b.Name);
                    }
                }

                // 注意：这里绝对不能去改 content.buildings（那是"全部内容池"，原版本来就含
                // Homestead / Farmfield 这些非菜单建筑）。从内容池里删名字会让引用它们的定居点
                // 解析失败，表现就是"定居点无法加载"（v1.0.5 已踩过一次）。
                // 阻止解锁只需要"不往 essentialBuildings 里加"，并让 IsUnlocked / ShouldShowInShop
                // 在对 Homestead 放行时说 false。

                foreach (var name in want)
                {
                    if (!content.buildings.Contains(name)) content.buildings.Add(name);
                    if (!content.essentialBuildings.Contains(name))
                    {
                        content.essentialBuildings.Add(name);
                        added.Add(name);
                    }
                }
                if (added.Count > 0)
                    LogDebug("FC AUTOUNLOCK added to essentialBuildings: " + string.Join(", ", added));
                else
                    LogDebug("FC AUTOUNLOCK ran; nothing new added.");
                if (deduped > 0) FileLog($"FC AUTOUNLOCK: 去重删掉 {deduped} 条重复建筑名");
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR AddAutoUnlockedBuildings: " + ex.Message);
            }
        }

        [HarmonyPatch(typeof(GameContentService), PathGameContentServiceIsUnlocked)]
        [HarmonyPostfix]
        private static void AutoUnlockBuildings(GameContentService __instance, BuildingModel building, ref bool __result)
        {
            try
            {
                if (__result || building == null) return;
                if (IsAutoUnlocked(building)) __result = true;
            }
            catch (Exception ex)
            {
                FileLog("ERR AutoUnlockBuildings: " + ex.Message);
            }
        }

        private static bool IsAutoUnlocked(BuildingModel b)
        {
            if (b == null) return false;

            // The custom Town Hall: always unlocked so it can be built right away, and it is already
            // part of the pool through InjectTownHallIntoPool().
            if (FC_IsTownHall(b)) return true;

            // 纯查询层放行：只影响"能不能选"，不写任何存档状态。
            if (IsQueryOnlyUnlock(b)) return true;

            // 只拦黑名单（自耕农场/田地/Ruined/…）。不能用 isInShop 做正向闸门：
            // 绝大多数正常种植建筑的 isInShop 都是 false，会把菜单筛空。
            if (IsDeniedBuilding(b)) return false;

            if (b.Name == "Biome Grass Spawner") return true;

            if (IsGatherOrFarm(b) || IsPlantingByDisplayName(b)) return true;

            // advanced gathering camps and the advanced rain collector: match by localized name.
            string d = null;
            try { d = b.displayName != null ? b.displayName.Text : null; } catch { }
            string lower = (d ?? "").ToLowerInvariant();
            if (lower.Contains("高级") || lower.Contains("advanced")) return true;

            return false;
        }
    }
}
