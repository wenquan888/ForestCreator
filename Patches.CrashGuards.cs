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
        // ============ CRASH GUARD #1: ActorBrain empty-stack safety ============
        // A robot (automaton) in a worker slot can be queried by the building panel while its
        // ActorBrain task stack is still empty (SpawnNewAutomaton adds the automaton to the
        // building BEFORE SetUpAutomatonBrain runs, and ChangeBehaviour leaves a brief empty
        // window). ActorBrain.GetActiveTask() is stack.Peek(), which throws
        // InvalidOperationException on an empty stack and crashes the whole building panel
        // (WorkerStatusSlot.SetUpActor -> Actor.GetTaskIcon -> ActorBrain.GetCurrentIcon).
        // Short-circuit the four public readers that call it to return safe defaults when the
        // stack is empty. This is the definitive fix for the "panel won't open" crash.
        private static FieldInfo _brainStackField;

        private static bool ActorBrain_StackEmpty(ActorBrain brain)
        {
            try
            {
                if (_brainStackField == null)
                    _brainStackField = AccessTools.Field(typeof(ActorBrain), "stack");
                if (_brainStackField == null) return false;
                var s = _brainStackField.GetValue(brain) as Stack<ActorTask>;
                return s == null || s.Count == 0;
            }
            catch { return false; }
        }

        [HarmonyPatch(typeof(ActorBrain), "GetCurrentIcon")]
        [HarmonyPrefix]
        private static bool ActorBrain_GetCurrentIcon_Prefix(ActorBrain __instance, ref Sprite __result)
        {
            if (ActorBrain_StackEmpty(__instance)) { __result = null; return false; }
            return true;
        }

        [HarmonyPatch(typeof(ActorBrain), "GetCurrentDescription")]
        [HarmonyPrefix]
        private static bool ActorBrain_GetCurrentDescription_Prefix(ActorBrain __instance, ref string __result)
        {
            if (ActorBrain_StackEmpty(__instance)) { __result = ""; return false; }
            return true;
        }

        [HarmonyPatch(typeof(ActorBrain), "GetCurrentProgress")]
        [HarmonyPrefix]
        private static bool ActorBrain_GetCurrentProgress_Prefix(ActorBrain __instance, ref float __result)
        {
            if (ActorBrain_StackEmpty(__instance)) { __result = 0f; return false; }
            return true;
        }

        [HarmonyPatch(typeof(ActorBrain), "GetCurrentGood")]
        [HarmonyPrefix]
        private static bool ActorBrain_GetCurrentGood_Prefix(ActorBrain __instance, ref Good __result)
        {
            if (ActorBrain_StackEmpty(__instance)) { __result = Good.None; return false; }
            return true;
        }

        // ============ CRASH GUARD #2: ActorTasksList.OnStart fallback ============
        // When a behaviour (e.g. Craftsman) starts but NONE of its sub-tasks can start yet,
        // the original OnStart pushes nothing and base.OnStart() (BaseInstantTask) immediately
        // marks the behaviour completed. ActorBrain.CheckForCompletedTask then pops it, the
        // stack empties, RunBehaviour() re-pushes the behaviour, and the loop runs >1000 times
        // -> "brain has looped itself!". Because IdleWait.RequiresFinishedWorkplace is false it
        // can ALWAYS start, so if no sub-task was pushed we push an IdleWait as a long-lived
        // fallback to keep the stack non-empty (the normal scheduler still re-runs later).
        [HarmonyPatch(typeof(ActorTasksList), "OnStart")]
        [HarmonyPostfix]
        private static void ActorTasksList_OnStart_Postfix(ActorTasksList __instance)
        {
            try
            {
                var brain = AccessTools.Field(typeof(ActorTask), "brain")?.GetValue(__instance) as ActorBrain;
                var actor = AccessTools.Field(typeof(ActorTask), "actor")?.GetValue(__instance) as Actor;
                if (brain == null || actor == null) return;
                var stackField = AccessTools.Field(typeof(ActorBrain), "stack");
                if (stackField == null) return;
                var stack = stackField.GetValue(brain) as Stack<ActorTask>;
                // A real sub-task was pushed on top of the behaviour -> the normal scheduler owns it.
                if (stack != null && stack.Count > 0 && stack.Peek() != __instance) return;
                // Nothing pushable: push a long-lived IdleWait so the behaviour does not complete
                // instantly and empty the stack (which would make ActorBrain loop forever).
                var idle = new IdleWait();
                idle.SetUp(actor, brain, new ActorTaskState(ActorTaskType.IdleWait));
                brain.PushTask(idle);
            }
            catch { /* never let a guard break the game */ }
        }

        // ============ CRASH GUARD #3: FulfillNeeds must never run for automatons ============
        // A robot re-pointed to a production behaviour (e.g. Craftsman) still inherits the common
        // high-base sub-tasks of the behaviour, including FulfillNeeds (rest / eat / hearth).
        // FulfillNeeds.OnSetUp does `villager = actor as Villager`; an automaton has no Villager
        // component, so villager is null, and FulfillNeeds.CanStart() -> FeelsAnyNeed() then
        // NullRefs on `villager.state`. That is why a workshop robot "spawned" but never produced:
        // the behaviour died in FulfillNeeds while evaluating its sub-tasks. Villagers are
        // unaffected (they ARE a Villager). We short-circuit CanStart to false for any non-villager
        // actor so the robot skips rest/eat and proceeds straight to the real production sub-tasks.
        [HarmonyPatch(typeof(FulfillNeeds), "CanStart")]
        [HarmonyPrefix]
        private static bool FulfillNeeds_CanStart_Prefix(FulfillNeeds __instance, ref bool __result)
        {
            var villager = AccessTools.Field(typeof(FulfillNeeds), "villager")?.GetValue(__instance);
            if (villager == null) { __result = false; return false; }
            return true;
        }

        // ============ CRASH FIX: BaseInstantTask (automaton in worker slot) ============
        //
        // Root cause of the building-panel crash: when an automaton (e.g. Pump Automaton =
        // CollectorWorkerAutomaton) occupies a worker slot, its behaviour executes BaseInstantTask
        // sub-tasks (Think/Collect/IdleInWorkplace/...). The game's own BaseInstantTask.GetIcon()
        // and GetDescription() throw `NotImplementedException($"{Type} is instant task!")`, which
        // bubbles up through Actor.GetTaskIcon -> WorkerStatusSlot.SetUpActor and crashes the whole
        // building panel whenever that building is opened.
        //
        // Fix: short-circuit those two methods to return safe defaults instead of throwing.
        //   * GetIcon()       -> null   (WorkerStatusSlot.SetUpIcon hides the status icon when null)
        //   * GetDescription() -> ""     (tooltip shows empty text, no crash)
        // This lets ANY automaton (pump/hauler/blight fighter/...) render and produce in a worker
        // slot without crashing the panel. Production is unaffected (the tasks themselves run).

        [HarmonyPatch(typeof(BaseInstantTask), "GetIcon")]
        [HarmonyPrefix]
        private static bool BaseInstantTask_GetIcon_Prefix(ref Sprite __result)
        {
            __result = null;
            return false; // skip original (which throws)
        }

        [HarmonyPatch(typeof(BaseInstantTask), "GetDescription")]
        [HarmonyPrefix]
        private static bool BaseInstantTask_GetDescription_Prefix(ref string __result)
        {
            __result = "";
            return false; // skip original (which throws)
        }
    }
}
