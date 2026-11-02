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
        // Layout / robot / embark diagnostics go to a txt file (so the game console doesn't need to be open).
        private const string LayoutLogPath =
            @"C:\Users\WenQuan\Doubao\chats\2026-10-18\new-chat-2\housing10x\layout_dump.txt";

        internal static void FileLog(string msg)
        {
            try { System.IO.File.AppendAllText(LayoutLogPath, msg + "\n"); }
            catch { }
        }

        // Diagnostic: dump every Image component under a GameObject with its sprite name, so we can
        // see exactly which Image is actually rendered by the robot slot (icon-not-showing bug).
        private static void DumpImages(GameObject go, string tag)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"IMAGES[{tag}] go={go.name}");
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                    sb.Append($" |[{img.gameObject.name} spr={img.sprite?.name} act={img.gameObject.activeSelf} mat={(img.material != null)}]");
                FileLog(sb.ToString());
            }
            catch (Exception ex) { FileLog("ERR DumpImages: " + ex.Message); }
        }

        private static void DumpTree(BuildingsPickScreen __instance, string tag)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"TREE[{tag}] root={__instance.name}");
                DumpNode(sb, __instance.transform as RectTransform, 0, 3);
                FileLog(sb.ToString());
            }
            catch (Exception ex)
            {
                FileLog("ERR DumpTree: " + ex.Message);
            }
        }

        private static void DumpNode(System.Text.StringBuilder sb, RectTransform rt, int depth, int maxDepth)
        {
            if (rt == null || depth > maxDepth) return;
            sb.Append($" |{new string('.', depth)}[{rt.name} apY={rt.anchoredPosition.y:F0} h={rt.rect.height:F0} worldY={rt.position.y:F0}]");
            foreach (Transform c in rt)
                DumpNode(sb, c as RectTransform, depth + 1, maxDepth);
        }
    }
}
