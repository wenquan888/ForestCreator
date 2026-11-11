using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
            @"C:\Users\WenQuan\Doubao\chats\2026-11-07\new-chat\fc_diag.txt";

        // Helpers so feature files that live outside the Plugin class (e.g. TownHallUI) can log
        // without touching the protected BaseUnityPlugin.Logger.
        internal static void LogInfo(string msg)
        {
            try { Instance?.Logger?.LogInfo(msg); } catch { }
        }

        internal static void LogError(string msg)
        {
            try { Instance?.Logger?.LogError(msg); } catch { }
        }

        [Conditional("DEBUG")]
        internal static void FileLog(string msg)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(LayoutLogPath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(LayoutLogPath, msg + "\n");
            }
            catch { }

            // Mirror into the BepInEx log as well, so a missing/unwritable file can never hide a probe.
            try { Instance?.Logger?.LogInfo("[DIAG] " + msg); } catch { }
        }

        // One-shot capture of a native UI panel's real art: object paths, Image sprites, TMP fonts
        // and button markers. Used to rebuild the custom Town Hall menu out of the game's own visuals.
        private static bool _fcNativePanelDumped;

        internal static void DumpNativePanelOnce(Transform root)
        {
            if (_fcNativePanelDumped || root == null) return;
            _fcNativePanelDumped = true;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("==== NATIVE PANEL DUMP (TownHall reference) ====");
                WalkUI(root, 0, sb);
                FileLog(sb.ToString());
            }
            catch (Exception ex) { FileLog("ERR DumpNativePanelOnce: " + ex.Message); }
        }

        private static void WalkUI(Transform t, int depth, System.Text.StringBuilder sb)
        {
            if (t == null || depth > 5 || sb.Length > 150000) return;
            try
            {
                sb.Append(' ', depth * 2).Append('[').Append(t.gameObject.name).Append(']');
                if (!t.gameObject.activeSelf) sb.Append(" inactive");

                var img = t.GetComponent<Image>();
                if (img != null)
                    sb.Append(" img(spr=").Append(img.sprite != null ? img.sprite.name : "null")
                      .Append(", type=").Append(img.type)
                      .Append(", c=").Append(img.color).Append(')');

                var tmp = t.GetComponent<TMP_Text>();
                if (tmp != null)
                    sb.Append(" tmp(\"").Append((tmp.text ?? "").Replace("\n", "\\n")).Append("\" font=")
                      .Append(tmp.font != null ? tmp.font.name : "null")
                      .Append(" size=").Append(tmp.fontSize)
                      .Append(" c=").Append(tmp.color).Append(')');

                if (t.GetComponent<Button>() != null) sb.Append(" <BUTTON>");
                if (t.GetComponent<ScrollRect>() != null) sb.Append(" <SCROLL>");
                sb.AppendLine();

                for (int i = 0; i < t.childCount; i++) WalkUI(t.GetChild(i), depth + 1, sb);
            }
            catch { }
        }
        // Diagnostic: dump every Image component under a GameObject with its sprite name, so we can
        // see exactly which Image is actually rendered by the robot slot (icon-not-showing bug).
        private static void DumpImages(GameObject go, string tag)        {
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
