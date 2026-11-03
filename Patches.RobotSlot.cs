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
        // ============ FEATURE: add a "Robot" slot to the Races radial menu ============

        /// <summary>
        /// Mirrors RadialMenu.GetPositionFor (slotsRadius). Computes the circle position for a
        /// slot index given the total number of slots. Raised above the game's default 80 so the
        /// slots (84px round icons) do not overlap when there are many races + the robot slot.
        /// </summary>
        private const float SlotRadius = 125f;

        private static Vector2 GetRadialPosition(int index, int maxSlots)
        {
            float f = index * Mathf.PI * 2f / maxSlots;
            return new Vector2(Mathf.Sin(f) * SlotRadius, Mathf.Cos(f) * SlotRadius);
        }

        // Decide whether the currently-open building can accept a robot. Mirrors the actual
        // spawn logic in RobotMenuSlot.OnRobotClicked:
        //   * ICollector (rain collector / pump / mine)            -> native CollectorWorker
        //   * IWorkshop / Institution WITH a real profession        -> spawned with that behaviour
        //   * anything else                                         -> blocked
        private static bool BuildingSupportsRobot(RacesMenu menu)
        {
            try
            {
                var bf = typeof(RacesMenu).GetField("building", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var building = bf != null ? bf.GetValue(menu) as Building : null;
                if (!(building is ProductionBuilding prod)) return false;

                if (prod is ICollector) return true;

                if (prod is IWorkshop || prod is Institution)
                {
                    var bm = prod.BuildingModel;
                    var prof = bm != null ? bm.Profession : null;
                    var behaviour = prof != null ? prof.behaviour : ActorTaskType.None;
                    return behaviour != ActorTaskType.None;
                }
                return false;
            }
            catch { return false; }
        }

        [HarmonyPatch(typeof(RacesMenu), "SetUp")]
        [HarmonyPostfix]
        private static void AddRobotSlotToRacesMenu(RacesMenu __instance)
        {
            try
            {
                if (__instance == null) { Instance.Logger.LogInfo("ROBOT: fired but instance is null"); return; }
                Instance.Logger.LogInfo("ROBOT: AddRobotSlot fired.");
                var slotsField = typeof(RacesMenu).GetField("slots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (slotsField == null) { Instance.Logger.LogInfo("ROBOT: RacesMenu.slots field not found"); return; }
                var slots = slotsField.GetValue(__instance) as List<RacesMenuSlot>;
                if (slots == null || slots.Count == 0) { Instance.Logger.LogInfo($"ROBOT: no slots ({(slots == null ? "null" : slots.Count.ToString())})"); return; }

                // Diagnostic so this path is visible in LogOutput.log (FileLog target is dead).
                var diagBuilding = (Building)(
                    typeof(RacesMenu).GetField("building", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(__instance));
                Instance.Logger.LogInfo($"ROBOT: slots={slots.Count}, building={(diagBuilding != null ? diagBuilding.GetType().Name : "null")}, supports={BuildingSupportsRobot(__instance)}");

                // RacesMenu is a REUSED object: a RobotSlot we cloned on a previous open stays in the
                // hierarchy (it is NOT part of the slots list). Clear any leftover RobotSlot first so
                // switching from a robot-capable building back to a non-capable one doesn't leave a
                // stale robot button on the wheel. NOTE: slots live under a nested container, not
                // directly under the menu root, so we must search recursively.
                foreach (var t in __instance.transform.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == "RobotSlot")
                    {
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                        FileLog("ROBOT: cleared leftover RobotSlot");
                    }
                }

                // Enlarge the slot circle: raise slotsRadius so slots no longer overlap. NOTE we must
                // NOT scale the wheel's RectTransform here: RacesMenu is a REUSED object, so a
                // `sizeDelta *= 1.4` runs on every open and accumulates, pushing the wheel off screen.
                try
                {
                    var radialField = typeof(RadialMenu).GetField("slotsRadius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (radialField != null) radialField.SetValue(__instance, SlotRadius);
                }
                catch (Exception ex) { FileLog("ERR enlarge wheel: " + ex.Message); }

                int raceCount = slots.Count;

                // If this building can't accept a robot, just re-lay the existing race slots in a
                // plain circle and return — no robot slot, no greyed-out ghost button.
                if (!BuildingSupportsRobot(__instance))
                {
                    int total = raceCount;
                    for (int i = 0; i < raceCount; i++)
                    {
                        var rt = slots[i].GetComponent<RectTransform>();
                        if (rt != null) rt.anchoredPosition = GetRadialPosition(i, total);
                    }
                    FileLog($"ROBOT: building does not support robot, skipped. races={raceCount}");
                    return;
                }

                // Re-lay the existing slots so the new robot slot fits in the circle.
                int totalWithRobot = raceCount + 1; // + robot slot
                for (int i = 0; i < raceCount; i++)
                {
                    var rt = slots[i].GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = GetRadialPosition(i, totalWithRobot);
                }

                // Clone a VISIBLE (revealed) slot as the robot slot visual template.
                RacesMenuSlot template = null;
                foreach (var s in slots)
                {
                    if (s == null) continue;
                    var contentNode = s.transform.Find("Content");
                    if (contentNode != null && contentNode.gameObject.activeSelf) { template = s; break; }
                }
                if (template == null) template = slots[raceCount - 1];

                var robotGo = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                robotGo.name = "RobotSlot";

                // Force the robot slot visible even if the template race was unrevealed.
                var rContent = robotGo.transform.Find("Content");
                if (rContent != null) rContent.gameObject.SetActive(true);
                var rEmpty = robotGo.transform.Find("EmptyMarker");
                if (rEmpty != null) rEmpty.gameObject.SetActive(false);
                robotGo.SetActive(true);

                // Remove the game's own slot behaviour so it won't try to act as a race pick.
                var origSlot = robotGo.GetComponent<RacesMenuSlot>();
                if (origSlot != null)
                {
                    origSlot.enabled = false;
                    UnityEngine.Object.DestroyImmediate(origSlot);
                }

                // Disable its button (we re-point the same button to our handler below).
                var btn = robotGo.GetComponentInChildren<Button>();
                if (btn != null) btn.onClick.RemoveAllListeners();

                // Hide the "recommended race" tag badges (leftTagIcon / rightTagIcon) that were
                // cloned from the template. On buildings where the template race is the preferred
                // worker the game shows a small wheat/badge icon next to the portrait; the robot
                // slot must NOT inherit that badge.
                HideClonedTagIcon(template, robotGo, "leftTagIcon");
                HideClonedTagIcon(template, robotGo, "rightTagIcon");

                // Position the robot slot in the new (n+1) circle.
                var robotRt = robotGo.GetComponent<RectTransform>();
                if (robotRt != null) robotRt.anchoredPosition = GetRadialPosition(raceCount, totalWithRobot);

                var robotComp = robotGo.AddComponent<RobotMenuSlot>();
                robotComp.SetUp(__instance);
                DumpImages(robotGo, "after-robot-SetUp");

                FileLog($"ROBOT: appended robot slot. races={raceCount} total={totalWithRobot} slot={robotGo.name}");
            }
            catch (Exception ex)
            {
                FileLog("ERR AddRobotSlotToRacesMenu: " + ex.Message);
                Instance.Logger.LogError("AddRobotSlotToRacesMenu failed: " + ex.Message);
            }
        }

        // Resolve the given RacesMenuSlot tag-icon field on the template, walk up to build its
        // transform path relative to the template root, then find and hide the same node on the clone.
        private static void HideClonedTagIcon(RacesMenuSlot template, GameObject robotGo, string fieldName)
        {
            try
            {
                var f = typeof(RacesMenuSlot).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) return;
                var img = f.GetValue(template) as Image;
                if (img == null) return;

                Transform src = img.transform;
                Transform root = template.transform;
                string path = src.name;
                Transform cur = src.parent;
                while (cur != null && cur != root)
                {
                    path = cur.name + "/" + path;
                    cur = cur.parent;
                }

                var dest = robotGo.transform.Find(path);
                if (dest != null)
                {
                    dest.gameObject.SetActive(false);
                    var di = dest.GetComponent<Image>();
                    if (di != null) di.sprite = null;
                    FileLog($"ROBOT: hidden cloned tag {fieldName} at {path}");
                }
            }
            catch (Exception ex) { FileLog("ERR HideClonedTagIcon(" + fieldName + "): " + ex.Message); }
        }
    }
}
