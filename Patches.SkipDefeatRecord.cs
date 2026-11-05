using System;
using HarmonyLib;
using Eremite.Services;
using Eremite.WorldMap;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: debug mode - do not record defeats / abandons ============
        //
        // Verified chain (Cecil):
        //   GameMenuPopup.Abandon()        -> ReputationService.Abandon() -> GameLost()
        //   ReputationService.CheckForLoose() -> GameLost()   (defeat when penalty overflows)
        //   GameLost():
        //     - StatsService.GameFinished(false)        -> MetaStatsGameUpdater.GameFinished(false)
        //     - gameResultSubject.OnNext(false)         -> MetaStateGameUpdater.GameFinished(false)
        //                                                  -> SaveInHistory(false)
        //                                                  -> GameHistoryCreator.SaveHistory(false)
        //                                                     (writes the GamesHistory failure entry)
        //
        // We keep the defeat flow itself intact (UI shows defeated, town is discarded, etc.),
        // but skip the two places that RECORD the loss, so you can freely abandon/restart while
        // debugging without accumulating failure history / stats. Wins are untouched.
        //
        // Params by position (__0) instead of by name: the parameter names are not visible in the
        // IL dump, and a wrong name makes the whole PatchAll abort (see the worker/actor incident).
        // SaveHistory(Boolean) and GameFinished(Boolean) both take hasWon as their first argument.

        [HarmonyPatch(typeof(GameHistoryCreator), "SaveHistory")]
        [HarmonyPrefix]
        private static bool SkipDefeatHistory(bool __0)
        {
            try
            {
                if (!__0) { LogDebug("FC: skipped defeat game-history record"); return false; }
            }
            catch (Exception ex) { Instance.Logger.LogError("FC ERR SkipDefeatHistory: " + ex.Message); }
            return true;
        }

        [HarmonyPatch(typeof(StatsService), "GameFinished")]
        [HarmonyPrefix]
        private static bool SkipDefeatStats(bool __0)
        {
            try
            {
                if (!__0) { LogDebug("FC: skipped defeat meta-stats record"); return false; }
            }
            catch (Exception ex) { Instance.Logger.LogError("FC ERR SkipDefeatStats: " + ex.Message); }
            return true;
        }

        // ---- failed towns shown at the bottom of the world map (skull icons) ----
        // Verified: CycleStateGameUpdater.AddFailedCity(bool hasWon) does
        //   if (hasWon) return;  WorldState.failedCities.Add(CreateFailedCityState()); // wasWon=false
        // WorldCalendarDisplay reads WorldState.failedCities to render the bottom town row with skulls.
        // So skipping AddFailedCity(false) stops new defeat towns from being recorded.
        [HarmonyPatch(typeof(CycleStateGameUpdater), "AddFailedCity")]
        [HarmonyPrefix]
        private static bool SkipFailedCity(bool __0)
        {
            try
            {
                if (!__0) { LogDebug("FC: skipped failed-town record (world map bottom)"); return false; }
            }
            catch (Exception ex) { Instance.Logger.LogError("FC ERR SkipFailedCity: " + ex.Message); }
            return true;
        }

        // Clear the already-accumulated failedCities when the world state is loaded, so the old
        // skull towns disappear too. FixState() runs right after the world save is loaded.
        [HarmonyPatch(typeof(WorldStateService), "FixState")]
        [HarmonyPostfix]
        private static void ClearFailedCities(WorldState __result)
        {
            try
            {
                if (__result != null && __result.failedCities != null && __result.failedCities.Count > 0)
                {
                    int n = __result.failedCities.Count;
                    __result.failedCities.Clear();
                    LogDebug($"FC: cleared {n} existing failed-town record(s) from world map");
                }
            }
            catch (Exception ex) { Instance.Logger.LogError("FC ERR ClearFailedCities: " + ex.Message); }
        }

        // ---- cycle progress bar at the bottom of the world map ----
        // CycleStateGameUpdater.GameFinished(hasWon) starts with an UNCONDITIONAL AddYears(), so
        // every town end (win / lose / abandon) pushes the cycle year forward -> the purple progress
        // bar grows even when you abandon or lose. For free debugging we skip the whole GameFinished
        // when hasWon == false, so the cycle year does not advance on defeats/abandons. Wins untouched.
        [HarmonyPatch(typeof(CycleStateGameUpdater), "GameFinished")]
        [HarmonyPrefix]
        private static bool SkipDefeatCycleProgress(bool __0)
        {
            try
            {
                if (!__0) { LogDebug("FC: skipped cycle progress on defeat/abandon (bar does not grow)"); return false; }
            }
            catch (Exception ex) { Instance.Logger.LogError("FC ERR SkipDefeatCycleProgress: " + ex.Message); }
            return true;
        }
    }
}
