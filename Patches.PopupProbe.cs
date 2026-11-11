using System;
using System.Reflection;
using System.Text;
using Eremite.Services;
using Eremite.View.Popups;
using Eremite.View.SaveSupport;
using HarmonyLib;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ TEMPORARY DIAGNOSTIC: world-save compatibility ============
        // Reproduced: the boot popup is Eremite.View.SaveSupport.WorldSaveSupportPopup, raised from
        // SavesSupportService.ValidateWorld() because "World version incompatible!".
        //
        // ValidateWorld only skips the popup when IsWorldValid() or NoWorldSaveFile() is true, and
        // IsWorldValid() is exactly: State.worldSupportCode == Config.currentWorldCode
        // So the whole question is "which two strings disagree". Log both, verbatim.
        //
        // Debug builds only; delete this file once the mismatch is explained.

        [HarmonyPatch(typeof(SavesSupportService), "IsWorldValid")]
        [HarmonyPrefix]
        private static void Diag_IsWorldValid(SavesSupportService __instance)
        {
            try
            {
                object state = GetMemberValue(__instance, "State");
                object config = GetMemberValue(__instance, "Config");
                string saveCode = GetFieldText(state, "worldSupportCode");
                string currentCode = GetFieldText(config, "currentWorldCode");
                FileLog($"FC WORLDCODE: saveCode=\"{saveCode}\" currentCode=\"{currentCode}\" match={(saveCode == currentCode)} appVersion={UnityEngine.Application.version}");
            }
            catch (Exception ex) { FileLog("ERR WORLDCODE: " + ex.Message); }
        }

        [HarmonyPatch(typeof(WorldSaveSupportPopup), "Show")]
        [HarmonyPrefix]
        private static void Diag_WorldSavePopupShow()
        {
            try { FileLog("FC WORLDCODE: WorldSaveSupportPopup.Show"); }
            catch (Exception ex) { FileLog("ERR WORLDCODE: " + ex.Message); }
        }

        // ---------- generic popup probe ----------
        // Anything the game raises goes through PopupsService.PopupShown(Popup). Log the concrete
        // type plus every string-ish field it carries, so the popup on screen can be identified
        // from the log instead of from a screenshot.

        [HarmonyPatch(typeof(PopupsService), "PopupShown")]
        [HarmonyPostfix]
        private static void Diag_AnyPopupShown(Popup __0)
        {
            try
            {
                if (__0 == null) { FileLog("FC POPUP: <null>"); return; }

                var sb = new StringBuilder("FC POPUP: ").Append(__0.GetType().FullName);
                try { sb.Append(" active=").Append(__0.gameObject.activeSelf); } catch { }

                Type t = __0.GetType();
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try
                    {
                        if (f.FieldType == typeof(string))
                        {
                            string v = (string)f.GetValue(__0);
                            if (!string.IsNullOrEmpty(v)) sb.Append(' ').Append(f.Name).Append('=').Append(v);
                        }
                        else if (f.FieldType == typeof(bool))
                        {
                            object v = f.GetValue(__0);
                            if (v is bool flag && flag) sb.Append(' ').Append(f.Name).Append("=true");
                        }
                        else if (f.FieldType.Name.IndexOf("Localized", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            object v = f.GetValue(__0);
                            if (v != null) sb.Append(' ').Append(f.Name).Append('=').Append(v);
                        }
                    }
                    catch { }
                }

                FileLog(sb.ToString());
            }
            catch (Exception ex) { FileLog("ERR POPUP: " + ex.Message); }
        }

        private static object GetMemberValue(object target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return prop.GetValue(target);
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(target);
        }

        private static string GetFieldText(object target, string name)
        {
            if (target == null) return "<null target>";
            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return $"<no field {name} in {type.Name}>";
            var value = field.GetValue(target);
            return value?.ToString() ?? "<null>";
        }

        private void StartPopupProbe()
        {
            FileLog("FC POPUP-DIAG: probe armed (worldcode mode)");
        }
    }
}
