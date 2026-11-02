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
        // ============ FEATURE: housing capacity x10 ============

        /// <summary>
        /// Multiplies the population capacity of every existing housing building by 10.
        /// Patch the runtime capacity getter (both overloads) so that all reads
        /// (moving villagers in, UI panel, housing lookup service) see the x10 value.
        /// </summary>
        [HarmonyPatch(typeof(EffectsService), "GetHouseCapacity", new Type[] { typeof(House) })]
        [HarmonyPostfix]
        private static void GetHouseCapacity_House_Postfix(ref int __result)
        {
            __result *= 10;
        }

        [HarmonyPatch(typeof(EffectsService), "GetHouseCapacity", new Type[] { typeof(HouseModel) })]
        [HarmonyPostfix]
        private static void GetHouseCapacity_HouseModel_Postfix(ref int __result)
        {
            __result *= 10;
        }
    }
}
