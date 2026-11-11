using System;
using System.Reflection;
using HarmonyLib;

namespace ForestCreator
{
    /// <summary>
    /// Compatibility shim for ATS_API (API.dll).
    ///
    /// ATS_API announces a "modded save corruption" dialog through its own
    /// ATS_API.SaveLoading.GenericPopup (object name "API Generic Popup", title "Save File Corruption").
    /// That dialog is not routed through Eremite's PopupsService, it blocks the menu behind a pending
    /// UniTask, and on some machines it fires every startup even with a clean save folder - the player
    /// then cannot get past it ("settlement cannot be loaded", new-settlement button spins forever).
    ///
    /// We therefore auto-answer the decision: record what API wanted to report, then resolve it as
    /// "delete the unusable modded save data" and let loading continue. Everything else in API keeps
    /// working untouched. Signatures are patched by reflection on purpose, so ForestCreator still loads
    /// (and never hard-fails) when API.dll is not installed.
    /// </summary>
    internal static class ApiSaveCompat
    {
        private const string SaveManagerType = "ATS_API.SaveLoading.ModdedSaveManagerService";
        private const string CorruptionTaskType = "Eremite.Model.SaveSupport.SaveCorruptionTask";

        private static FieldInfo _decisionField;

        internal static void Install(Harmony harmony)
        {
            try
            {
                var manager = AccessTools.TypeByName(SaveManagerType);
                if (manager == null)
                {
                    Plugin.FileLog("FC API-COMPAT: ATS_API not installed, nothing to do");
                    return;
                }

                var createTask = AccessTools.Method(manager, "CreateSaveCorruptionTask");
                if (createTask != null)
                    harmony.Patch(createTask, prefix: new HarmonyMethod(AccessTools.Method(typeof(ApiSaveCompat), nameof(LogCorruptionTask))));

                var waitForDecision = AccessTools.Method(manager, "WaitForDecision");
                if (waitForDecision != null)
                    harmony.Patch(waitForDecision, prefix: new HarmonyMethod(AccessTools.Method(typeof(ApiSaveCompat), nameof(SkipDecisionPopup))));

                Plugin.FileLog($"FC API-COMPAT: installed (createTask={createTask != null} waitForDecision={waitForDecision != null})");
            }
            catch (Exception ex) { Plugin.FileLog("ERR API-COMPAT: " + ex); }
        }

        // Records which modded save file API considers broken, so the user can still see it in the log.
        private static void LogCorruptionTask(string __0, string __1)
        {
            try { Plugin.FileLog($"FC API-COMPAT: corruption reported | path={__1} | file={__0}"); }
            catch { }
        }

        // Skips the blocking dialog so the menu does not freeze behind a pending UniTask.
        // decision = 0 -> LoadBackup（回滚到备份），1 -> Delete（直接删存档）。
        // v1.0.5 起固定用 0：这里绝不能静默删玩家存档——一旦是我们自己的补丁写坏了数据，
        // 选 Delete 就等于把证据和存档一起抹掉。
        private static bool SkipDecisionPopup(object task)
        {
            try
            {
                if (task != null)
                {
                    if (_decisionField == null)
                    {
                        var taskType = AccessTools.TypeByName(CorruptionTaskType) ?? task.GetType();
                        _decisionField = AccessTools.Field(taskType, "decision");
                    }

                    if (_decisionField != null)
                    {
                        _decisionField.SetValue(task, Enum.ToObject(_decisionField.FieldType, 0));
                        Plugin.FileLog("FC API-COMPAT: corruption dialog suppressed, decision=LoadBackup");
                    }
                    else
                    {
                        Plugin.FileLog("FC API-COMPAT: decision field not found, dialog not suppressed");
                        return true;
                    }
                }
                else
                {
                    Plugin.FileLog("FC API-COMPAT: null corruption task, dialog skipped");
                }
            }
            catch (Exception ex) { Plugin.FileLog("ERR API-COMPAT SkipDecisionPopup: " + ex); return true; }

            // Return false => original WaitForDecision is not executed; its UniTask result stays
            // default(UniTask), which is already completed, so the caller resumes immediately.
            return false;
        }
    }
}
