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
        // ============ FEATURE: fixed picked layout + reposition button ============

        // IMPORTANT DESIGN NOTE (verified by experiments, do not revert):
        // Cloning blank picked slots (to show "27 empty cells") is NOT viable: the game's
        // GoodPickSlot/RewardPickSlot tooltip handlers crash with ArgumentNullException when
        // hovered with null model (GoodTooltip.Show(good=null) -> Settings.GetGood(null) ->
        // Dictionary.FindEntry(null)). No CanvasGroup/blocksRaycasts flag stops EventSystem's
        // hover dispatch reliably. So we do NOT clone slots. Instead we make the picked grid
        // layout FIXED: grow the Picked container so the extra rows fit without overlapping the
        // points/button, and pin the bottom-anchored points/button just below the last slot row.
        // The game still populates slots dynamically (one per picked item) — that is fine.

        [HarmonyPatch(typeof(BuildingsPickScreen), "Show")]
        [HarmonyPostfix]
        private static void ReservePickedRows(BuildingsPickScreen __instance)
        {
            try
            {
                if (__instance == null) return;
                // The screen GameObject is still INACTIVE inside Show, so we cannot StartCoroutine on it.
                // Start the coroutine on the (always-active) plugin instead; it waits for the screen to
                // become active and then reads the real, post-layout geometry.
                Instance.StartCoroutine(ReservePickedRowsDeferred(__instance));
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("ReservePickedRows failed: " + ex.Message);
            }
        }

        private static IEnumerator ReservePickedRowsDeferred(BuildingsPickScreen __instance)
        {
            // wait until the pick screen is active, then two frames for layout to fully apply
            while (__instance == null || !__instance.gameObject.activeInHierarchy)
                yield return null;
            yield return null;
            yield return null;

            try
            {
                RectTransform root = __instance.transform as RectTransform;
                Transform content = root != null ? root.Find("Content") : null;
                Transform pickedNode = content != null ? content.Find("Picked") : null;
                RectTransform picked = pickedNode as RectTransform;
                if (picked == null) { FileLog("NOPICKED"); DumpTree(__instance, "NOPICKED"); yield break; }

                // ---- read row height from the slot grid ----
                Transform gridContentT = picked.Find("Content"); // the grid that holds the slot children
                RectTransform gridContent = gridContentT as RectTransform;
                GridLayoutGroup grid = gridContent != null ? gridContent.GetComponent<GridLayoutGroup>() : null;
                int columns = 9;
                float rowH = 0f;
                if (grid != null)
                {
                    if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount) columns = grid.constraintCount;
                    rowH = grid.cellSize.y + grid.spacing.y;
                }
                if (rowH <= 0f) // fallback: Picked's own grid
                {
                    grid = pickedNode.GetComponent<GridLayoutGroup>();
                    if (grid != null)
                    {
                        if (grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount) columns = grid.constraintCount;
                        rowH = grid.cellSize.y + grid.spacing.y;
                    }
                }
                if (rowH <= 0f) // fallback: first active slot height
                    foreach (Transform c in picked)
                        if (c.gameObject.activeSelf) { rowH = ((RectTransform)c).rect.height; break; }

                // ---- grow Picked so the bottom-anchored points/button sit just below the 3rd row ----
                Transform confirmT = picked.Find("ConfirmButton");
                if (rowH > 0f && confirmT != null)
                {
                    RectTransform confirm = confirmT as RectTransform;
                    int rowsNeeded = 3; // 27 picks @ 9 columns = 3 rows
                    float gridTopFromPicked = gridContent != null ? -gridContent.localPosition.y : 0f; // grid top below Picked top
                    float lastRowBottom = gridTopFromPicked + rowsNeeded * rowH;                       // bottom of the last slot row
                    float btnOffset = confirm.anchoredPosition.y;                                     // bottom-anchored: center is this far above Picked bottom
                    float btnH = confirm.rect.height;
                    const float gap = 15f; // desired gap below the last slot row
                    float buttonTopFromPicked = picked.rect.height - btnOffset - btnH / 2f;
                    float delta = (lastRowBottom + gap) - buttonTopFromPicked;

                    FileLog($"LAYOUT rowH={rowH:F0} columns={columns} rowsNeeded={rowsNeeded} gridTopFromPicked={gridTopFromPicked:F0} lastRowBottom={lastRowBottom:F0} btnOffset={btnOffset:F0} btnH={btnH:F0} gap={gap:F0} buttonTopFromPicked={buttonTopFromPicked:F0} delta={delta:F0} pickedH={picked.rect.height:F0}");

                    if (delta > 0f)
                    {
                        float py = picked.pivot.y;
                        picked.sizeDelta = new Vector2(picked.sizeDelta.x, picked.rect.height + delta);
                        picked.anchoredPosition = new Vector2(picked.anchoredPosition.x, picked.anchoredPosition.y - delta * (1f - py));
                        FileLog($"GROW delta={delta:F0} newH={picked.rect.height:F0}");
                    }
                }
                else
                {
                    FileLog($"SKIP rowH={rowH:F0} confirm={confirmT != null} (no grow)");
                }

                DumpTree(__instance, "LAYOUT");
                DumpSlots(__instance, picked, gridContent);
            }
            catch (Exception ex)
            {
                FileLog("ERR ReservePickedRowsDeferred: " + ex.Message);
                Instance.Logger.LogError("ReservePickedRowsDeferred failed: " + ex.Message);
            }
        }

        private static void DumpSlots(BuildingsPickScreen __instance, RectTransform picked, RectTransform gridContent)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"SLOTS pickedH={picked.rect.height:F0} anchorMin={picked.anchorMin} anchorMax={picked.anchorMax} pivot={picked.pivot}");
                if (gridContent != null)
                {
                    sb.Append($" | grid(name={gridContent.name},h={gridContent.rect.height:F0},lp={gridContent.localPosition.y:F0},anchorMin={gridContent.anchorMin},anchorMax={gridContent.anchorMax},pivot={gridContent.pivot})");
                    int i = 0;
                    foreach (Transform c in gridContent)
                    {
                        RectTransform r = c as RectTransform;
                        sb.Append($" |slot{i++}(name={c.name},active={c.gameObject.activeSelf},h={(r!=null?r.rect.height:0):F0},lp={(r!=null?r.localPosition.y:0):F0},anchorMin={(r!=null?r.anchorMin:Vector2.zero)},anchorMax={(r!=null?r.anchorMax:Vector2.zero)},pivot={(r!=null?r.pivot:Vector2.zero)})");
                    }
                }
                foreach (string name in new[] { "PointsLeft", "Plus", "BonusPointsLeft", "ConfirmButton", "EarlyEndMarker" })
                {
                    Transform t = picked.Find(name);
                    if (t == null) continue;
                    RectTransform r = t as RectTransform;
                    sb.Append($" |{name}(apY={r.anchoredPosition.y:F0},lp={r.localPosition.y:F0},h={r.rect.height:F0},anchorMin={r.anchorMin},anchorMax={r.anchorMax},pivot={r.pivot})");
                }
                FileLog(sb.ToString());
            }
            catch (Exception ex)
            {
                FileLog("ERR DumpSlots: " + ex.Message);
            }
        }
    }
}
