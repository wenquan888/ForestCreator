using System;
using System.Collections.Generic;
using ATS_API.Buildings;
using ATS_API.Helpers;
using Eremite;
using Eremite.Buildings;
using Eremite.Buildings.UI;
using Eremite.Controller;
using Eremite.Model;
using Eremite.Services;
using HarmonyLib;
using UnityEngine;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: 城镇大厅 (Town Hall) ============
        //
        // A custom building registered through ATS_API, placed in the single real building category the
        // game has: BuildingCategoriesTypes.City_Buildings.
        //
        // Clicking it opens a custom card menu (TownHallUI) listing every playable race; recruiting one
        // costs 2 Amber and spawns a villager of that race.
        //
        // LOOK: the building deliberately copies a native building (Holy Temple) — both the 3D model and
        // the menu icon — so it sits in the world like vanilla content instead of showing the API's
        // placeholder cube. That copy can only happen once Settings are loaded, so registration is
        // deferred out of Awake (see InjectQueryOnlyBuildings).
        internal const string FC_TownHallBuilderName = "Forest Creator Town Hall";
        // Goods are referenced by their full internal model name; the bare words "Wood" / "Amber" do
        // not exist in Settings.Goods, and an unresolvable name yields a null GoodRef (which breaks the
        // cost tooltip and the construction click) or a 0-amount lookup.
        internal const string FC_TownHallCurrency = "[Valuable] Amber";
        internal const string FC_TownHallCostGood = "[Mat Raw] Wood";
        internal const int FC_TownHallPrice = 2;

        // The vanilla building whose model + icon this one reuses.
        private const string FC_LookTemplateName = "Holy Temple";

        internal static InstitutionBuildingBuilder _fcTownHallBuilder;
        internal static string _fcTownHallName;

        private static bool _fcTownHallRegistered;
        private static bool _fcTownHallFailed;
        private static bool _fcTownHallPoolDone;
        private static GameContentService _fcContentSvc;

        /// <summary>
        /// Registers the Town Hall. Called once Settings are available, because copying the template
        /// building's prefab requires the real model.
        /// </summary>
        internal static void CreateTownHall()
        {
            if (_fcTownHallRegistered || _fcTownHallFailed) return;

            // Step 1: constructing the builder queues the building inside BuildingManager.
            InstitutionBuildingBuilder builder;
            try
            {
                builder = new InstitutionBuildingBuilder(
                    PluginInfo.PLUGIN_GUID, FC_TownHallBuilderName, FC_IconPath("fc_town_hall.png"));
            }
            catch (Exception ex)
            {
                _fcTownHallFailed = true;
                Instance.Logger.LogError("FC ERR CreateTownHall(ctor): " + ex);
                FileLog("ERR CreateTownHall(ctor): " + ex);
                return;
            }

            // Step 2: nothing below may throw. The building is already queued, so an exception would
            // leave a half-registered institution behind — which once made
            // StaticRecipesService.MapInstitutionsSinks die on a null recipes array and hung the
            // loading screen forever.
            try
            {
                var instModel = builder.BuildingModel;
                if (instModel != null)
                {
                    if (instModel.recipes == null) instModel.recipes = new InstitutionRecipeModel[0];
                    if (instModel.workplaces == null) instModel.workplaces = new WorkplaceModel[0];
                    if (instModel.activeEffects == null) instModel.activeEffects = new InstitutionEffectModel[0];
                    if (instModel.needsCache == null) instModel.needsCache = new HashSet<NeedModel>();
                    if (instModel.goodsCache == null) instModel.goodsCache = new HashSet<GoodModel>();
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR CreateTownHall(collections): " + ex.Message);
                FileLog("ERR CreateTownHall(collections): " + ex.Message);
            }

            // Step 3: copy the native look (3D model + menu icon).
            try
            {
                var holy = FC_FindLookTemplate();
                if (holy != null)
                {
                    FileLog("FC TOWNHALL look template: " + holy.name);

                    if (holy.prefab != null && holy.prefab.gameObject != null)
                    {
                        builder.SetCustomModel(holy.prefab.gameObject);
                        FileLog("FC TOWNHALL model copied from template prefab");
                    }
                    else
                    {
                        FileLog("FC TOWNHALL: template prefab is null, model will fall back to default");
                    }

                    var model = builder.BuildingModel;
                    if (model != null && holy.icon != null)
                    {
                        model.icon = holy.icon;
                        FileLog("FC TOWNHALL icon copied from template");
                    }
                }
                else
                {
                    FileLog("FC TOWNHALL: look template not found in Settings.Buildings");
                    FC_LogBuildingNames();
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR CreateTownHall(look): " + ex);
                FileLog("ERR CreateTownHall(look): " + ex);
            }

            // Step 4: display data / placement.
            try
            {
                builder.SetDisplayName("Town Hall", SystemLanguage.English);
                builder.SetDisplayName("城镇大厅", SystemLanguage.ChineseSimplified);

                string descEn = "Pay 2 Amber to recruit one villager of any race.";
                string descZh = "花费 2 琥珀，招募一名任意种族的居民。";
                builder.SetDescription(descEn, SystemLanguage.English);
                builder.SetDescription(descZh, SystemLanguage.ChineseSimplified);

                builder.SetLabel("Town Hall", SystemLanguage.English);
                builder.SetLabel("城镇大厅", SystemLanguage.ChineseSimplified);

                builder.SetCategory(BuildingCategoriesTypes.City_Buildings);
                builder.SetFootPrint(3, 3, FieldType.Grass);
                builder.SetOrder(0);

                // Construction cost: 15 Wood. NameToAmount's implicit tuple is (amount, name).
                builder.SetRequiredGoods(new NameToAmount[] { (15, FC_TownHallCostGood) });
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR CreateTownHall(texts): " + ex.Message);
                FileLog("ERR CreateTownHall(texts): " + ex.Message);
            }

            _fcTownHallBuilder = builder;
            _fcTownHallName = builder.BuildingModel != null ? builder.BuildingModel.name : null;
            _fcTownHallRegistered = true;
            Instance.Logger.LogInfo("FC TOWNHALL registered: " + _fcTownHallName);
            FileLog("FC TOWNHALL registered: " + _fcTownHallName);
            try
            {
                var bm = builder.BuildingModel;
                FileLog("FC TOWNHALL internal name = " + (bm != null ? bm.name : "null"));
                FileLog("FC TOWNHALL required goods = " +
                    (bm != null && bm.requiredGoods != null ? bm.requiredGoods.Length.ToString() : "null"));
                foreach (var g in bm != null && bm.requiredGoods != null ? bm.requiredGoods : new GoodRef[0])
                {
                    if (g == null) { FileLog("FC TOWNHALL cost entry = NULL REF"); continue; }
                    FileLog("FC TOWNHALL cost entry = " + (g.good != null ? g.good.name : "NULL GOOD") + " x " + g.amount);
                }
            }
            catch (Exception ex) { FileLog("ERR log required goods: " + ex.Message); }

            // displayName is a LocaText that resolves through the localization service, which is not
            // ready this early; reading it here would throw and swallow the rest of the logging.
            try
            {
                var bm = builder.BuildingModel;
                FileLog("FC TOWNHALL displayName = " + (bm != null && bm.displayName != null ? bm.displayName.Text : "null"));
            }
            catch (Exception ex) { FileLog("FC TOWNHALL displayName unresolved at registration (expected early on): " + ex.Message); }
        }

        /// <summary>
        /// Finds the vanilla building used as the visual template.
        /// </summary>
        private static InstitutionModel FC_FindLookTemplate()
        {
            try
            {
                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null || settings.Buildings == null) return null;

                foreach (var b in settings.Buildings)
                {
                    var inst = b as InstitutionModel;
                    if (inst == null) continue;

                    string n = (inst.name ?? "").Replace(" ", "").ToLowerInvariant();
                    string d = "";
                    try { d = inst.displayName != null ? inst.displayName.Text : ""; } catch { }
                    d = (d ?? "").ToLowerInvariant();

                    if (n.Contains("holytemple") || n.Contains(FC_LookTemplateName.Replace(" ", "").ToLowerInvariant()) ||
                        d.Contains("holy temple") || d.Contains("圣殿") || d.Contains("神庙"))
                        return inst;
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR FC_FindLookTemplate: " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Dumps every institution name once, so the template name can be corrected from the log.
        /// </summary>
        private static void FC_LogBuildingNames()
        {
            try
            {
                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null || settings.Buildings == null) return;

                var sb = new System.Text.StringBuilder();
                sb.Append("FC BUILDINGS (institutions): ");
                foreach (var b in settings.Buildings)
                {
                    if (!(b is InstitutionModel)) continue;
                    sb.Append('[').Append(b.name).Append("] ");
                }
                FileLog(sb.ToString());
            }
            catch { }
        }

        /// <summary>
        /// Adds the town hall to this settlement's unlocked building pool (memory only, never written to
        /// MetaSave) and force-unlocks it.
        /// </summary>
        internal static void InjectTownHallIntoPool(GameContentService svc)
        {
            try
            {
                if (string.IsNullOrEmpty(_fcTownHallName)) return;
                if (svc == null) return;

                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null) return;
                if (settings.GetBuilding(_fcTownHallName) == null)
                {
                    LogDebug("FC TOWNHALL not present in Settings yet, skip pool injection");
                    return;
                }

                var state = svc.State;
                if (state == null || state.buildings == null) return;

                if (state.buildings.Contains(_fcTownHallName))
                {
                    _fcTownHallPoolDone = true;
                    return;
                }

                state.buildings.Add(_fcTownHallName);
                svc.CacheProperties();
                _fcTownHallPoolDone = true;
                LogDebug("FC TOWNHALL injected into this settlement's building pool");
                FileLog("FC TOWNHALL injected into building pool: " + _fcTownHallName);
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR InjectTownHallIntoPool: " + ex.Message);
                FileLog("ERR InjectTownHallIntoPool: " + ex.Message);
            }
        }

        /// <summary>
        /// BuildingManager syncs modded content on its own tick. Registration happens after Settings
        /// load, so the pool injection is retried here until the model actually exists.
        /// </summary>
        /// <summary>
        /// BuildingManager ticks every frame while the game is running. Used for two late jobs that
        /// cannot run in Awake: applying the vanilla look (Settings not loaded then) and injecting the
        /// building into this settlement's pool (the modded model only appears in Settings after the
        /// API syncs it).
        /// </summary>
        [HarmonyPatch(typeof(BuildingManager), "Tick")]
        [HarmonyPostfix]
        private static void AfterBuildingManagerTick()
        {
            ApplyTownHallLookOnce();
            RetryTownHallPoolInjection();
        }

        private static bool _fcLookApplied;

        /// <summary>
        /// Copies the template building's 3D model and menu icon once Settings are readable.
        /// Registration itself must happen earlier (Awake), so the model initially has the API's
        /// placeholder visual and only gets the vanilla one here.
        /// </summary>
        private static void ApplyTownHallLookOnce()
        {
            if (!_fcTownHallRegistered || _fcLookApplied) return;
            try
            {
                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null || string.IsNullOrEmpty(_fcTownHallName)) return;

                var town = settings.GetBuilding(_fcTownHallName) as InstitutionModel;
                if (town == null) return;

                var holy = FC_FindLookTemplate();
                if (holy == null) return;
                if (holy.prefab == null)
                {
                    FileLog("FC TOWNHALL look: template prefab is null, cannot copy model");
                    _fcLookApplied = true;
                    return;
                }

                if (holy.icon != null) town.icon = holy.icon;
                town.prefab = holy.prefab;

                _fcLookApplied = true;
                FileLog("FC TOWNHALL look applied from template: " + holy.name);
                FileLog("FC TOWNHALL icon from template: " + (holy.icon != null ? holy.icon.name : "null"));

                // After the API sync, this is the model's real cost list.
                try
                {
                    int n = town.requiredGoods != null ? town.requiredGoods.Length : -1;
                    FileLog("FC TOWNHALL synced cost entries = " + n);
                    if (town.requiredGoods != null)
                    {
                        foreach (var g in town.requiredGoods)
                        {
                            if (g == null) { FileLog("FC TOWNHALL cost = NULL REF"); continue; }
                            FileLog("FC TOWNHALL cost = " + (g.good != null ? g.good.name : "NULL GOOD") + " x " + g.amount);
                        }
                    }
                }
                catch (Exception cex) { FileLog("ERR log synced cost: " + cex.Message); }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR ApplyTownHallLookOnce: " + ex);
                FileLog("ERR ApplyTownHallLookOnce: " + ex);
            }
        }

        private static void RetryTownHallPoolInjection()
        {
            try
            {
                if (!_fcTownHallRegistered || _fcTownHallPoolDone) return;
                if (_fcContentSvc == null) return;

                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null) return;
                if (string.IsNullOrEmpty(_fcTownHallName) || settings.GetBuilding(_fcTownHallName) == null) return;

                FC_ResolveTownHallName();
                InjectTownHallIntoPool(_fcContentSvc);
            }
            catch { }
        }

        /// <summary>
        /// Opens our card menu when the player selects the town hall.
        /// BuildingPanel.SetUpBuilding is the single place the game sets up the side panel for a selected
        /// building, so hooking it covers every selection path.
        /// </summary>
        [HarmonyPatch(typeof(BuildingPanel), "SetUpBuilding")]
        [HarmonyPostfix]
        private static void AfterSetUpBuilding(BuildingPanel __instance, Building __0)
        {
            try
            {
                var building = __0;
                if (building == null) return;

                var model = building.UpgradableModel as BuildingModel;
                if (!FC_IsTownHall(model))
                {
                    // One-shot: capture the real art (panel background, button sprites, fonts) of a
                    // native building panel, so the custom menu can reuse the game's own visuals
                    // instead of hand-drawn approximations.
                    DumpNativePanelOnce(__instance != null ? __instance.transform : null);
                    return;
                }

                LogDebug("FC TOWNHALL opened");
                TownHallUI.Show();
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR AfterSetUpBuilding: " + ex.Message);
            }
        }

        // ---- recruiting -------------------------------------------------------------

        internal static int FC_AmberAmount()
        {
            try
            {
                var storage = Serviceable.StorageService as StorageService;
                if (storage == null || storage.Main == null) return 0;
                return storage.Main.Goods.GetAmount(FC_TownHallCurrency);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Recruits one villager of the given race, paying FC_TownHallPrice Amber.
        /// </summary>
        internal static string FC_RecruitVillager(RaceModel race)
        {
            if (race == null) return "无效的种族 / invalid race";

            try
            {
                var storage = Serviceable.StorageService as StorageService;
                if (storage == null || storage.Main == null) return "仓库不可用 / warehouse unavailable";

                var goods = storage.Main.Goods;
                if (!goods.Contains(FC_TownHallCurrency, FC_TownHallPrice))
                    return "琥珀不足 / not enough Amber";

                goods.Remove(FC_TownHallCurrency, FC_TownHallPrice);

                var villagers = Serviceable.VillagersService;
                if (villagers == null)
                {
                    goods.Add(FC_TownHallCurrency, FC_TownHallPrice);
                    return "居民服务不可用 / villager service unavailable";
                }

                var spawned = villagers.SpawnNewVillager(race);
                if (spawned == null)
                {
                    goods.Add(FC_TownHallCurrency, FC_TownHallPrice);
                    return "招募失败 / recruit failed";
                }

                string raceName = null;
                try { raceName = race.displayName != null ? race.displayName.Text : race.name; } catch { }
                LogDebug($"FC TOWNHALL recruited {raceName}; amber left {goods.GetAmount(FC_TownHallCurrency)}");
                FileLog($"FC TOWNHALL recruited {raceName}; amber left {goods.GetAmount(FC_TownHallCurrency)}");
                return "+1 " + (raceName ?? race.name);
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR FC_RecruitVillager: " + ex);
                return "错误 / error";
            }
        }

        internal static List<RaceModel> FC_PlayableRaces()
        {
            var list = new List<RaceModel>();
            try
            {
                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null || settings.Races == null) return list;

                foreach (var r in settings.Races)
                {
                    if (r == null) continue;
                    if (!r.isActive) continue;
                    list.Add(r);
                }
                list.Sort((a, b) => a.order.CompareTo(b.order));
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR FC_PlayableRaces: " + ex.Message);
            }
            return list;
        }

        // ---- identity / menu visibility -------------------------------------------------

        internal static bool FC_IsTownHall(BuildingModel b)
        {
            if (b == null) return false;
            try
            {
                string n = b.Name ?? b.name;
                if (!string.IsNullOrEmpty(_fcTownHallName) &&
                    string.Equals(n, _fcTownHallName, StringComparison.Ordinal))
                    return true;

                string d = null;
                try { d = b.displayName != null ? b.displayName.Text : null; } catch { }
                if (!string.IsNullOrEmpty(d) &&
                    (d.Contains("Town Hall") || d.Contains("城镇大厅")))
                    return true;
            }
            catch { }
            return false;
        }

        internal static void FC_ResolveTownHallName()
        {
            try
            {
                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null || settings.Buildings == null) return;

                if (!string.IsNullOrEmpty(_fcTownHallName) && settings.GetBuilding(_fcTownHallName) != null)
                    return;

                foreach (var b in settings.Buildings)
                {
                    if (!FC_IsTownHall(b)) continue;
                    string real = b.Name ?? b.name;
                    if (string.IsNullOrEmpty(real)) continue;

                    if (!string.IsNullOrEmpty(_fcTownHallName) && real != _fcTownHallName)
                        FileLog($"FC TOWNHALL name resolved: '{_fcTownHallName}' -> '{real}'");
                    _fcTownHallName = real;
                    return;
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR FC_ResolveTownHallName: " + ex.Message);
            }
        }

        /// <summary>
        /// The build menu only lists models whose isInShop is true. A freshly registered modded building
        /// defaults to false, so force it visible. Pure query override, writes nothing.
        /// </summary>
        [HarmonyPatch(typeof(ConstructionService), "ShouldShowInShop")]
        [HarmonyPostfix]
        private static void ShowTownHallInShop(BuildingModel __0, ref bool __result)
        {
            try
            {
                if (__result) return;
                if (FC_IsTownHall(__0)) __result = true;
            }
            catch { }
        }
    }
}
