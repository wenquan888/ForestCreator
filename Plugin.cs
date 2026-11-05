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

[assembly: IgnoresAccessChecksTo("Assembly-CSharp")]

namespace System.Runtime.CompilerServices
{
    // Allows this mod to access internal members of the game assembly at runtime (BepInEx honors this attribute).
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName) { }
    }
}

namespace ForestCreator
{
    /// <summary>
    /// Plugin entry point. All gameplay patches live in their own feature files
    /// (Patches.*.cs / RobotMenuSlot.cs / Diagnostics.cs) as members of this partial class.
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;

        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            harmony = Harmony.CreateAndPatchAll(typeof(Plugin), (string)null);
            Instance.Logger.LogInfo("Plugin ForestCreator is loaded!");
        }

        // Debug-only diagnostic log. Calls are completely stripped from Release builds by
        // [Conditional("DEBUG")], so no diagnostic noise ships in the packaged mod.
        [Conditional("DEBUG")]
        internal static void LogDebug(string msg)
        {
            Instance.Logger.LogInfo(msg);
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "ForestCreator";
        public const string PLUGIN_NAME = "ForestCreator";
        public const string PLUGIN_VERSION = "1.0.0";
    }
}
