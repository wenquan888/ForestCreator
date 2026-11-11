using System;
using System.Collections.Generic;
using System.IO;
using ATS_API.Effects;
using Eremite.Characters.Villagers;
using Eremite.Controller;
using Eremite.Model;
using Eremite.Model.Effects;
using Eremite.Model.Sound;
using Eremite.Services;
using HarmonyLib;
using UnityEngine;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: two custom cornerstones, auto-applied at game start ============
        //
        // The cornerstones themselves are registered through ATS_API (GUID "API") so they become
        // real game content with their own name, description, rarity and icon. Nothing is written
        // to MetaSave: exactly like the vanilla starting cornerstones, they are applied to the
        // current settlement when the game starts, so existing settlements keep loading.
        //
        //   森林造物主的狂热崇拜 -> settlers never want to leave   (blocks VillagerLossType.Leave)
        //   森林造物主的神圣庇佑 -> settlers never die abnormally  (blocks VillagerLossType.Death)
        //
        // Both effects hang off ONE choke point verified from IL:
        //   Villager.Die(VillagerLossType, string, bool, float, SoundModel)  -> DieAsync
        // Every way a villager is lost funnels through here: VillagersService.KillVillagers(),
        // starvation, disease, events and assaults all end up calling Die with their loss type.
        // Exile is deliberately NOT blocked, because that is the player's own choice.
        //
        // NOTE ON NAMING: ATS_API renames every registered effect to "<mod guid>_<name>"
        // (EffectManager.AddEffect does name = guid + "_" + name). We therefore never look the
        // models up by name in Settings: the builder hands us the final EffectModel instance and
        // we keep a reference to it, which is exactly what ATS_API's own example mod does.
        // The names below are only builder-side names. Renaming them changes the effect name in
        // existing settlements, so treat them as permanent.
        internal const string FC_WorshipBuilderName = "Forest Creator Fervent Worship";
        internal const string FC_ProtectionBuilderName = "Forest Creator Divine Protection";

        private static string _fcWorshipEffectName;
        private static string _fcProtectionEffectName;
        private static HookedEffectModel _fcWorshipModel;
        private static HookedEffectModel _fcProtectionModel;

        private static bool _fcCornerstonesRegistered;
        private static bool _fcCornerstonesFailed;

        // Whether the matching cornerstone is actually present in this settlement. Set when the
        // starting-cornerstone injector runs, so the behaviour only kicks in when the player can
        // also see the cornerstone in the HUD.
        private static bool _fcWorshipActive;
        private static bool _fcProtectionActive;

        /// <summary>
        /// Registers both cornerstones with ATS_API. Must run early (plugin Awake), before the API
        /// merges modded content into Settings.
        /// </summary>
        internal static void CreateCustomCornerstones()
        {
            if (_fcCornerstonesRegistered || _fcCornerstonesFailed) return;

            try
            {
                var worship = CreateFerventWorship();
                var protection = CreateDivineProtection();

                // ATS_API renamed them to "<guid>_<name>"; keep both the final name and the model.
                _fcWorshipModel = worship.EffectModel;
                _fcProtectionModel = protection.EffectModel;
                _fcWorshipEffectName = _fcWorshipModel != null ? _fcWorshipModel.name : null;
                _fcProtectionEffectName = _fcProtectionModel != null ? _fcProtectionModel.name : null;

                _fcCornerstonesRegistered = true;
                Instance.Logger.LogInfo("FC CORNERSTONE registered: " + _fcWorshipEffectName + " / " + _fcProtectionEffectName);
            }
            catch (Exception ex)
            {
                // Without a working registration the injector would log "not found" every game.
                _fcCornerstonesFailed = true;
                Instance.Logger.LogError("FC ERR CreateCustomCornerstones: " + ex);
            }
        }

        private static HookedEffectBuilder CreateFerventWorship()
        {
            var builder = new HookedEffectBuilder(
                PluginInfo.PLUGIN_GUID, FC_WorshipBuilderName, FC_IconPath("fc_blue_dragon.png"));

            builder.SetPositive(true);
            builder.SetRarity(EffectRarity.Legendary);
            builder.SetObtainedAsCornerstone();
            builder.SetAvailableInAllBiomesAndSeasons();
            builder.SetDrawLimit(1);
            builder.SetLabel("ForestCreator", SystemLanguage.English);
        builder.SetLabel("森林造物主", SystemLanguage.ChineseSimplified);

            builder.SetDisplayName("Forest Creator's Fervent Worship");
            builder.SetDescription(
                "The settlers of this town worship the Forest Creator with such fervor that they no longer wish to leave. " +
                "Villagers never leave the settlement.");
            builder.SetDisplayName("森林造物主的狂热崇拜", SystemLanguage.ChineseSimplified);
            builder.SetDescription(
                "定居点的居民对森林造物主狂热崇拜，因此不会想要离开。",
                SystemLanguage.ChineseSimplified);

            return builder;
        }

        private static HookedEffectBuilder CreateDivineProtection()
        {
            var builder = new HookedEffectBuilder(
                PluginInfo.PLUGIN_GUID, FC_ProtectionBuilderName, FC_IconPath("fc_gold_dragon.png"));

            builder.SetPositive(true);
            builder.SetRarity(EffectRarity.Legendary);
            builder.SetObtainedAsCornerstone();
            builder.SetAvailableInAllBiomesAndSeasons();
            builder.SetDrawLimit(1);
            builder.SetLabel("ForestCreator", SystemLanguage.English);
        builder.SetLabel("森林造物主", SystemLanguage.ChineseSimplified);

            builder.SetDisplayName("Forest Creator's Divine Protection");
            builder.SetDescription(
                "The Forest Creator watches over this town, so its settlers cannot die abnormally. " +
                "Villagers never die from starvation, disease, events or assaults.");
            builder.SetDisplayName("森林造物主的神圣庇佑", SystemLanguage.ChineseSimplified);
            builder.SetDescription(
                "森林造物主庇护着定居点的居民，因此定居点的居民不会异常死去。",
                SystemLanguage.ChineseSimplified);

            return builder;
        }

        // Icons are read as absolute paths from the folder next to our dll (Assets/fc_*.png).
        // If that file is missing we hand back the bare file name: ATS_API then falls back to a
        // recursive search of the plugins folder instead of failing on a non-existent path.
        private static string FC_IconPath(string fileName)
        {
            try
            {
                string dir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                if (!string.IsNullOrEmpty(dir))
                {
                    string local = Path.Combine(Path.Combine(dir, "Assets"), fileName);
                    if (File.Exists(local)) return local;
                }
            }
            catch { }
            Instance.Logger.LogWarning("FC CORNERSTONE icon not found next to plugin, falling back to plugins-wide search: " + fileName);
            return fileName;
        }

        /// <summary>
        /// Applies both custom cornerstones to the current settlement. Called from the
        /// starting-cornerstone injector right after the vanilla ones.
        ///
        /// The models are applied through the reference captured at registration time instead of
        /// Settings.GetEffect(name): ATS_API renames effects to "&lt;guid&gt;_&lt;name&gt;", and the
        /// merged Settings collection may not contain them yet when StartGame fires. Both cases
        /// would silently produce "cornerstone not found" and no effect at all.
        /// </summary>
        internal static void FC_InjectCustomCornerstones()
        {
            InjectCustomCornerstone(_fcWorshipModel, "worship");
            InjectCustomCornerstone(_fcProtectionModel, "protection");
        }

        private static void InjectCustomCornerstone(HookedEffectModel model, string tag)
        {
            if (model == null)
            {
                LogDebug($"FC CORNERSTONE skip: {tag} model unavailable");
                return;
            }

            // Already in this settlement (new game applies, loaded save is skipped).
            if (FC_IsAlreadyApplied(model.name))
            {
                LogDebug($"FC CORNERSTONE already present: {model.name}");
                return;
            }

            try
            {
                // EffectModel.Apply -> AddAsPerk (puts it in the HUD cornerstone list)
                //                  -> HookedEffectModel.OnApply (creates the hook state)
                model.Apply(EffectContextType.None, null, 0);
                LogDebug($"FC CORNERSTONE injected: {model.name} ({tag})");
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError($"FC ERR inject cornerstone '{model.name}': {ex}");
            }
        }

        /// <summary>
        /// Re-reads whether both custom cornerstones are really active in this settlement.
        /// Called right after the starting-cornerstone injection.
        /// </summary>
        internal static void SyncCustomCornerstoneFlags()
        {
            _fcWorshipActive = _fcWorshipModel != null && FC_IsAlreadyApplied(_fcWorshipModel.name);
            _fcProtectionActive = _fcProtectionModel != null && FC_IsAlreadyApplied(_fcProtectionModel.name);
            LogDebug($"FC CORNERSTONE active: worship={_fcWorshipActive} protection={_fcProtectionActive}");
        }

        /// <summary>
        /// True when the effect is already active in this settlement, either as a plain perk
        /// (EffectsState.perks) or as a hooked effect (StateService.HookedEffects). Used to avoid
        /// applying the same cornerstone twice when a saved settlement is loaded.
        /// </summary>
        internal static bool FC_IsAlreadyApplied(string effectName)
        {
            if (string.IsNullOrEmpty(effectName)) return false;
            try
            {
                var state = Serviceable.StateService;
                if (state == null) return false;

                var perks = state.Effects != null ? state.Effects.perks : null;
                if (perks != null && perks.ContainsKey(effectName)) return true;

                var hooked = state.HookedEffects;
                if (hooked != null)
                {
                    if (ContainsHooked(hooked.activeEffects, effectName)) return true;
                    if (ContainsHooked(hooked.toAdd, effectName)) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool ContainsHooked(List<HookedEffectState> states, string effectName)
        {
            if (states == null) return false;
            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (s != null && s.model == effectName) return true;
            }
            return false;
        }

        // ---- behaviour ----------------------------------------------------------------

        // The single choke point every villager loss goes through. Returning false skips the
        // whole death routine, so the villager simply stays alive and keeps working.
        [HarmonyPatch(typeof(Villager), "Die", new[]
        {
            typeof(VillagerLossType), typeof(string), typeof(bool), typeof(float), typeof(SoundModel)
        })]
        [HarmonyPrefix]
        private static bool FC_BlockVillagerLoss(VillagerLossType __0)
        {
            return FC_ShouldAllowLoss(__0);
        }

        // Direct callers of the async path would otherwise bypass the patch above.
        [HarmonyPatch(typeof(Villager), "DieAsync", new[]
        {
            typeof(VillagerLossType), typeof(string), typeof(bool), typeof(float), typeof(SoundModel)
        })]
        [HarmonyPrefix]
        private static bool FC_BlockVillagerLossAsync(VillagerLossType __0)
        {
            return FC_ShouldAllowLoss(__0);
        }

        private static bool FC_ShouldAllowLoss(VillagerLossType lossType)
        {
            try
            {
                // 神圣庇佑: no villager ever dies abnormally
                if (lossType == VillagerLossType.Death && _fcProtectionActive)
                {
                    LogDebug("FC CORNERSTONE blocked villager death");
                    return false;
                }

                // 狂热崇拜: no villager ever wants to leave
                if (lossType == VillagerLossType.Leave && _fcWorshipActive)
                {
                    LogDebug("FC CORNERSTONE blocked villager leaving");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Instance.Logger.LogError("FC ERR FC_ShouldAllowLoss: " + ex.Message);
            }

            // Exile and every other loss type are left untouched.
            return true;
        }
    }
}
