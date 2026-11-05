using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Eremite;
using Eremite.Controller;
using Eremite.Model;
using Eremite.Model.Effects;
using Eremite.Services;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: auto-apply starting golden cornerstones at game start ============
        //
        // These cornerstones take effect automatically at the start of every game, WITHOUT the
        // player picking them in the embark bonus offer. Following the reference mod approach:
        //   GameController.StartGame (postfix) -> EffectModel.Apply(EffectContextType.None, null, 0)
        // guarded by EffectsState.perks.ContainsKey(name) so we never double-apply on a loaded save.
        //
        // Match by EXACT internal effect name (verified from BepInEx log), so we do NOT accidentally
        // apply the -main/-small sub-effects or multiple tiers of the same cornerstone.
        //   搬运车         -> Hauling Cart in All Warehouses
        //   双重运输系统   -> 2 Hauling Carts in Main Warehouse
        //   过于勤劳的木工 -> Accidental Barrels
        //   伐木税         -> Amber for Wood
        //   强劲之水       -> Woodcutting speed for water
        //   强化斧         -> Lumber Speed +50 (top tier)
        //   迷失荒野       -> [Altar] Villager for glade
        //   镇静之水       -> (pending: real internal name not yet identified)
        private static readonly string[] FC_StartingCornerstoneNames = new[]
        {
            "Hauling Cart in All Warehouses",
            "2 Hauling Carts in Main Warehouse",
            "Accidental Barrels",
            "Amber for Wood",
            "Woodcutting speed for water",
            "Lumber Speed +50",
            "[Altar] Villager for glade",
        };

        [HarmonyPatch(typeof(GameController), "StartGame")]
        [HarmonyPostfix]
        private static void InjectStartingCornerstones()
        {
            try
            {
                var settings = MainController.Instance.Settings;
                if (settings == null) return;

                var state = Serviceable.StateService;
                if (state == null) return;

                var perks = state.Effects != null ? state.Effects.perks : null;

                // ---- top up every item already in the main warehouse to at least 200 ----
                TopUpWarehouseTo200();

                foreach (var effectName in FC_StartingCornerstoneNames)
                {
                    EffectModel effect = null;
                    try { effect = settings.GetEffect(effectName); } catch { }
                    if (effect == null)
                    {
                        LogDebug($"FC START-CORNERSTONE not found: {effectName}");
                        continue;
                    }

                    string disp = null;
                    try { disp = effect.displayName != null ? effect.displayName.Text : null; } catch { }

                    // already applied (e.g. loaded save) -> skip to avoid double buff
                    if (perks != null && perks.ContainsKey(effect.name)) continue;

                    effect.Apply(EffectContextType.None, null, 0);
                    LogDebug($"FC START-CORNERSTONE applied: {effect.name} | {disp}");
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR InjectStartingCornerstones: " + ex.Message);
            }
        }

        // Raises every item already present in the main warehouse to at least 200 (only tops up;
        // items already above 200 are left untouched). Runs once at game start.
        private static void TopUpWarehouseTo200()
        {
            try
            {
                if (!GameController.IsGameActive) return;
                var storage = Serviceable.StorageService;
                if (storage == null) return;
                var main = storage.Main;
                if (main == null) return;
                var goods = main.Goods;
                if (goods == null) return;

                var names = new List<string>(goods.goods.Keys);
                int topped = 0;
                foreach (var name in names)
                {
                    int amount = goods.GetAmount(name);
                    if (amount < 200)
                    {
                        goods.Add(name, 200 - amount);
                        topped++;
                    }
                }
                LogDebug($"FC: topped up {topped} warehouse item(s) to 200");
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR TopUpWarehouseTo200: " + ex.Message);
            }
        }
    }
}
