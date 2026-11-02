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
        // ============ FEATURE: rain catchers (incl. upgraded) are continuously placeable ============
        //
        // BuildingsPanel.ShouldRepeat() only keeps the placement active (Repeat) when the building's
        // BuildingModel.repeatable is true — that is why HOUSING is continuously placeable. Every
        // other building (rain catchers, pumps, workshops...) has repeatable=false, so placing one
        // returns to the build menu. We flip repeatable=true on every RainCatcherModel, which covers
        // BOTH the basic rain collector and the upgraded one (they share the RainCatcherModel type).
        [HarmonyPatch(typeof(BuildingsPanel), "Start")]
        [HarmonyPostfix]
        private static void MakeRainCatchersRepeatable()
        {
            try
            {
                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null || settings.Buildings == null) return;
                int n = 0;
                foreach (var b in settings.Buildings)
                {
                    if (b == null || !(b is RainCatcherModel)) continue;
                    if (!b.repeatable)
                    {
                        b.repeatable = true;
                        n++;
                        FileLog($"REPEAT: {b.Name} set repeatable=true (continuously placeable)");
                    }
                }
                if (n > 0) FileLog($"REPEAT: made {n} rain-catcher building(s) continuously placeable");
            }
            catch (Exception ex) { FileLog("ERR MakeRainCatchersRepeatable: " + ex.Message); }
        }

        // ============ FEATURE: the Trading Post (商栈) can now be moved ============
        // ConstructionService.CanBeMoved(model) ultimately returns model.movable. TradingPostModel
        // inherits BuildingModel.movable, which is false in the data asset, so the Trading Post can't
        // be moved. Flip it to true so the move button appears and works.
        [HarmonyPatch(typeof(BuildingsPanel), "Start")]
        [HarmonyPostfix]
        private static void MakeTradingPostMovable()
        {
            try
            {
                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings == null || settings.Buildings == null) return;
                int m = 0;
                foreach (var b in settings.Buildings)
                {
                    if (b == null || !(b is TradingPostModel)) continue;
                    if (!b.movable)
                    {
                        b.movable = true;
                        m++;
                        FileLog($"MOVE: {b.Name} set movable=true (trading post can now be moved)");
                    }
                }
                if (m > 0) FileLog($"MOVE: made {m} trading-post building(s) movable");
            }
            catch (Exception ex) { FileLog("ERR MakeTradingPostMovable: " + ex.Message); }
        }
    }
}
