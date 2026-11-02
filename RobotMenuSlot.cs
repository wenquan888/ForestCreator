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
    /// <summary>
    /// A small self-contained component that turns a cloned RacesMenuSlot GameObject into a
    /// "Robot" pick: shows the Pump Automaton round icon, a counter of how many robots can be
    /// placed (available Parts / 2), and on click consumes 2 Parts and spawns a Pump Automaton
    /// into the building whose worker-slot menu is open.
    /// </summary>
    public class RobotMenuSlot : MonoBehaviour
    {
        private const int PartsCost = 2;

        private RacesMenu menu;
        private Building building;
        private Image iconImage;
        private TMP_Text counterText;
        private Button button;
        private bool iconResolved;

        public void SetUp(RacesMenu menu)
        {
            this.menu = menu;

            // building comes from the RacesMenu's private field.
            building = ReadPrivateField(menu, "building") as Building;

            // Robust lookup: try the standard path first, then fall back to scanning by name so we
            // never silently bind to null (which left the cloned race icon/counter on screen).
            iconImage = FindImage(transform, "Content/Mask/Icon", "Icon");
            counterText = FindText(transform, "Content/Counter", "Counter");
            button = GetComponentInChildren<Button>();

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnRobotClicked);
            }

            // Try to set the robot icon now; retry in Update until it succeeds, because at
            // SetUp time MainController.Instance.Settings may not be ready yet (which made
            // ResolvePumpAutomaton return null and left the cloned template race icon visible).
            iconResolved = false;
            TrySetIcon();

            UpdateCounter();
        }

        private void Update()
        {
            // keep the counter live while the menu is open
            if (menu != null && menu.gameObject.activeInHierarchy)
            {
                UpdateCounter();
                if (!iconResolved) TrySetIcon();
            }
        }

        private void TrySetIcon()
        {
            try
            {
                if (iconImage == null) return;
                var pump = ResolvePumpAutomaton();
                if (pump == null) { Plugin.FileLog("ROBOT: ResolvePumpAutomaton returned null at TrySetIcon"); return; }
                var spr = pump.roundIcon != null ? pump.roundIcon : pump.icon;
                if (spr == null) { Plugin.FileLog($"ROBOT: pump {pump.Name} has null roundIcon AND null icon"); return; }
                iconImage.sprite = spr;
                iconImage.material = null;
                iconResolved = true;
                Plugin.FileLog($"ROBOT: robot icon set name={pump.Name} behaviour={pump.behaviour} roundIcon={pump.roundIcon?.name} icon={pump.icon?.name} iconImageSpr={iconImage?.sprite?.name}");
            }
            catch (Exception ex)
            {
                Plugin.FileLog("ERR TrySetIcon: " + ex.Message);
            }
        }

        private void UpdateCounter()
        {
            // Show how many robots can be placed (available parts / 2). We never display the cloned
            // race's population count. We also keep the icon ungreyed and the button interactive so
            // this slot can never inherit the first race's "exhausted / greyed" state.
            if (counterText != null)
                counterText.text = (GetAvailableParts() / PartsCost).ToString();
            if (button != null)
                button.interactable = building is ProductionBuilding prod && (prod is IWorkshop || prod is ICollector || prod is Institution);
            if (iconImage != null)
                iconImage.material = null;
        }

        private static Image FindImage(Transform root, string path, string fallbackName)
        {
            var t = root != null ? root.Find(path) : null;
            var img = t != null ? t.GetComponent<Image>() : null;
            if (img != null) return img;
            if (root == null) return null;
            foreach (var img2 in root.GetComponentsInChildren<Image>(true))
                if (img2.gameObject.name == fallbackName) return img2;
            return null;
        }

        private static TMP_Text FindText(Transform root, string path, string fallbackName)
        {
            var t = root != null ? root.Find(path) : null;
            var txt = t != null ? t.GetComponent<TMP_Text>() : null;
            if (txt != null) return txt;
            if (root == null) return null;
            foreach (var txt2 in root.GetComponentsInChildren<TMP_Text>(true))
                if (txt2.gameObject.name == fallbackName) return txt2;
            return null;
        }

        private void OnRobotClicked()
        {
            try
            {
                if (!(building is ProductionBuilding prod)) { Plugin.FileLog("ROBOTBTN: S0 building is not ProductionBuilding"); CloseMenu(); return; }

                var pump = ResolvePumpAutomaton();
                if (pump == null) { Plugin.FileLog("ROBOTBTN: pump automaton not found"); CloseMenu(); return; }

                var bm = prod.BuildingModel;
                var prof = bm != null ? bm.Profession : null;
                var behaviour = prof != null ? prof.behaviour : ActorTaskType.None;

                // 1) Workshop-type building (crafts goods) AND service buildings (Institution, e.g.
                //    tavern/clinic/church) -> spawn the robot DIRECTLY with the building's own
                //    behaviour (Craftsman for workshops, Clerk for institutions). We must NOT use the
                //    game's SpawnNewAutomaton here: it always builds a CollectorWorker brain whose
                //    Collect task NullRefs on a non-Collector building, which is exactly the crash we
                //    were seeing. Spawning with the building's own behaviour bypasses that crash AND
                //    makes the robot work (workshop crafts goods; institution clerk hauls goods to
                //    the building so it can serve villagers).
                if ((prod is IWorkshop || prod is Institution) && behaviour != ActorTaskType.None)
                {
                    SpawnRobotWithBehaviour(prod, pump, behaviour);
                }
                // 2) Collector-type buildings (rain collector / pump / MINE, all ICollector) -> keep
                //    the native CollectorWorker (collects into the ICollector production pipeline,
                //    e.g. water for collectors, ore for mines). Stable.
                else if (prod is ICollector)
                {
                    GameMB.AutomatonsService.SpawnNewAutomaton(pump, prod);
                    Plugin.FileLog($"ROBOTBTN: native spawn into {prod.name} (Collector, water/ore)");
                }
                // 3) Everything else -> blocked (no crash, no fake empty slot).
                else
                {
                    Plugin.FileLog("ROBOTBTN: building is neither IWorkshop/Institution nor ICollector, robot placement blocked");
                    CloseMenu();
                    return;
                }

                CloseMenu();
            }
            catch (Exception ex)
            {
                Plugin.FileLog("ERR RobotMenuSlot.OnRobotClicked: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
                try { CloseMenu(); } catch { }
            }
        }

        // Spawn a robot that starts in the building's own production behaviour (e.g. Craftsman) so
        // it immediately produces recipe goods. We re-point state.behaviour BEFORE SetUpAutomatonBrain
        // builds the task stack, so the brain is created for the production behaviour and the
        // CollectorWorker path (which NullRefs on non-Collector buildings) is never entered.
        private static void SpawnRobotWithBehaviour(ProductionBuilding prod, AutomatonModel pump, ActorTaskType behaviour)
        {
            try
            {
                if (pump.prefab == null) { Plugin.FileLog("ROBOTBTN: pump.prefab is null"); return; }
                var automaton = UnityEngine.Object.Instantiate(pump.prefab);
                automaton.transform.position = prod.Entrance;
                automaton.transform.rotation = Quaternion.identity;
                automaton.state.model = pump.Name;
                automaton.SetUp(prod);
                // CRITICAL: switch to the building's behaviour BEFORE the brain is set up.
                automaton.state.behaviour = behaviour;
                if (pump.occupiesWorkplace) prod.AddAutomaton(automaton);
                else prod.AddLooseAutomaton(automaton.Id);
                automaton.SetUpAutomatonBrain();
                Plugin.FileLog($"ROBOTBTN: spawned {pump.Name} into {prod.name} as {behaviour}");
            }
            catch (Exception ex)
            {
                Plugin.FileLog("ERR SpawnRobotWithBehaviour: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        private void CloseMenu()
        {
            // The radial menu MUST close after a robot is placed (or the click is refused),
            // otherwise the player is stuck with an open circle that never dismisses.
            try { if (menu != null && menu.gameObject != null) menu.gameObject.SetActive(false); }
            catch (Exception ex) { Plugin.FileLog("ERR CloseMenu: " + ex.Message); }
        }

        private static int GetAvailableParts()
        {
            try
            {
                var partsGood = ResolvePartsGood();
                if (partsGood == null) return 0;
                var storage = GameMB.StorageService.Main;
                if (storage == null) return 0;
                return storage.GetAmount(partsGood.Name);
            }
            catch { return 0; }
        }

        private static GoodModel ResolvePartsGood()
        {
            var settings = MainController.Instance.Settings;
            if (settings == null || settings.Goods == null) return null;
            foreach (var g in settings.Goods)
            {
                if (g == null) continue;
                string d = null;
                try { d = g.displayName != null ? g.displayName.Text : null; } catch { }
                string l = (d ?? "").ToLowerInvariant();
                if (l.Contains("零件") || l.Contains("部件") || l.Contains("part"))
                    return g;
            }
            return null;
        }

        private static AutomatonModel ResolvePumpAutomaton()
        {
            try
            {
                var settings = MainController.Instance.Settings;
                if (settings == null || settings.Automatons == null)
                {
                    Plugin.FileLog("ROBOT: ResolvePumpAutomaton - settings/Automatons null");
                    return null;
                }
                AutomatonModel fallback = null;
                foreach (var a in settings.Automatons)
                {
                    if (a == null) continue;
                    string beh = a.behaviour.ToString();
                    Plugin.FileLog($"ROBOT: automaton candidate name={a.Name} behaviour={beh} occupiesWorkplace={a.occupiesWorkplace} roundIconNull={a.roundIcon == null}");
                    if (fallback == null) fallback = a;
                    if (a.Name.IndexOf("Pump", StringComparison.OrdinalIgnoreCase) >= 0)
                        return a;
                    if (a.behaviour.ToString().IndexOf("Collector", StringComparison.OrdinalIgnoreCase) >= 0)
                        return a;
                }
                // nothing matched by name/behaviour: fall back to any automaton that occupies a workplace
                if (fallback != null)
                {
                    Plugin.FileLog($"ROBOT: no pump match, fallback to first automaton {fallback.Name}");
                    return fallback;
                }
                Plugin.FileLog("ROBOT: no automaton models found at all");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.FileLog("ERR ResolvePumpAutomaton: " + ex.Message);
                return null;
            }
        }

        private static object ReadPrivateField(object target, string fieldName)
        {
            var f = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(target);
        }
    }
}
