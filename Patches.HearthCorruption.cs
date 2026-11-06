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
using HarmonyLib;
using UnityEngine;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: Hearth corruption resistance scaling ============
        //
        // Game default: GetHearthMaxHP() = max(1, blightConfig.hearthBaseHP + effects.hearthBonusHP)
        //   - hearthBaseHP  = 1000 (base corruption resistance for an un-upgraded Ancient Hearth)
        //   - hearthBonusHP = +150 per Hearth upgrade (hub tier)
        // We x10 the upgrade bonus (150 -> 1500 per level).
        //
        // ============ FEATURE: Decoration buildings grant extra corruption resistance ============
        // For every placed Decoration building, grant extra resistance based on its footprint
        // and its tier (DecorationTier.order):
        //   order 0 (green/common)  : +50  per tile
        //   order 1 (blue/rare)     : +150 per tile
        //   order 2 (gold/legendary): +300 per tile

        [HarmonyPatch(typeof(EffectsService), "GetHearthMaxHP")]
        [HarmonyPostfix]
        private static void ForestCreator_HearthMaxHP_X10(EffectsService __instance, ref int __result)
        {
            try
            {
                var settings = MainController.Instance.Settings;
                if (settings == null) return;
                var baseHP = settings.blightConfig.hearthBaseHP;
                var effects = __instance.Effects;
                if (effects == null) return;
                var bonusHP = effects.hearthBonusHP;

                // Decoration-derived resistance
                int decorationResistance = 0;
                if (FCConfig.EnableDecorationResistance.Value)
                try
                {
                    var bs = GameMB.BuildingsService;
                    if (bs != null && bs.Decorations != null)
                    {
                        foreach (var kv in bs.Decorations)
                        {
                            var dec = kv.Value as Decoration;
                            if (dec == null) continue;
                            var dm = dec.BuildingModel as DecorationModel;
                            if (dm == null) continue;

                            int tiles = Mathf.Max(1, dm.size.x * dm.size.y);
                            int perTile = FCConfig.DecorationGreenPerTile.Value;
                            if (dm.tier != null)
                            {
                                switch (dm.tier.order)
                                {
                                    case 0: perTile = FCConfig.DecorationGreenPerTile.Value; break;  // green
                                    case 1: perTile = FCConfig.DecorationBluePerTile.Value; break;   // blue
                                    case 2: perTile = FCConfig.DecorationGoldPerTile.Value; break;   // gold
                                    default: perTile = FCConfig.DecorationGreenPerTile.Value; break;
                                }
                            }
                            decorationResistance += tiles * perTile;
                        }
                    }
                }
                catch (Exception exDec)
                {
                    FileLog("ERR decoration resistance: " + exDec.Message);
                }

                __result = Mathf.Max(1, baseHP + bonusHP * FCConfig.HearthUpgradeBonusMultiplier.Value + decorationResistance);
            }
            catch (Exception ex)
            {
                FileLog("ERR HearthMaxHP x10: " + ex.Message);
            }
        }
    }
}
