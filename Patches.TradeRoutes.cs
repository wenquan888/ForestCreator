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
        // ============ FEATURE: trade routes allow up to 10 (was 3) ============
        // TradeRoutesService.GetSlotsLeft() = EffectsService.GetTradeRoutesAmount() - Routes.Count.
        // GetTradeRoutesAmount() returns MetaPerksService.GetMetaTradeRoutesLimit() + Effects.bonusTradeRoutes,
        // whose base is IronmanService.MetaConfig.baseTradeRoutesLimit (= 3). We lift the result to
        // at least 10 so the player can run up to 10 routes.
        [HarmonyPatch(typeof(EffectsService), "GetTradeRoutesAmount")]
        [HarmonyPostfix]
        private static void TradeRoutesLimitRaise(ref int __result)
        {
            var limit = FCConfig.MaxTradeRoutes.Value;
            if (__result < limit) __result = limit;
        }
    }
}
