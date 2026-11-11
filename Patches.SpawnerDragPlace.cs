using System;
using System.Collections.Generic;
using System.Reflection;
using Eremite;
using Eremite.Buildings;
using Eremite.Controller;
using Eremite.Model;
using Eremite.Services;
using Eremite.View.HUD;
using Eremite.View.HUD.Construction;
using HarmonyLib;
using UnityEngine;

namespace ForestCreator
{
    public partial class Plugin
    {
        // ============ FEATURE: drag-a-rectangle placement for the fertile-soil spawner ============
        //
        // WHY the vanilla farm field ("田地") can be dragged into a whole patch at once, while every
        // other building is placed one by one — verified from the game IL:
        //
        //   BuildingsPanel.CreateRequest(BuildingModel) dispatches on the model type:
        //       RoadModel     -> new RoadRequest(roadPlanner, PlacingFinished)
        //       FarmfieldModel-> new FieldRequest(fieldsPlanner, PlacingFinished)   <== the drag box
        //       anything else -> new BuildingRequest(...)                          <== single tile
        //
        //   GroupRequest (abstract base, implements IBuildingRequest):
        //       planner (IConstructionPlanner) + onFinished (Action)
        //       Update():  mouse DOWN -> planner.SetUp()
        //                  mouse UP   -> planner.GetPositions() -> Construct(list) -> Spawn(list) -> onFinished()
        //
        //   FieldRequest : GroupRequest  — Spawn(pos) instantiates model.prefab, sets it up with a fresh
        //       state, then Farmfield.Place(pos.ToWorld()).
        //
        //   FieldsPlanner (Eremite.Controller) is the actual rubber-band selector:
        //       SetUp()            records initialPos from the pointer
        //       UpdateSelection()  FromDragPoints(initial, current, 1, 1) -> RectInt, draws the frame
        //       UpdateHighlight()  spawns TilePlan previews inside the rect
        //       GetPositions()     every available tile inside lastFrameRect
        //       IsAvaiable(x,y)    field.Type == 40 (Grass) && ConstructionService.IsGridEmpty(x,y)
        //
        // So the ONLY thing that makes a building draggable is which request object the panel builds.
        // This feature supplies a SpawnerGroupRequest for the fertile-soil spawner (internal name
        // "Biome Grass Spawner", shown in game as 沃土), reusing the vanilla FieldsPlanner for the
        // rubber band and only relaxing its grass-type test so the spawner can target any free tile.
        //
        // Nothing here touches saves: it is pure construction-input plumbing. Buildings are created
        // through the same Instantiate/SetUp/Place path the vanilla field uses.

        /// <summary>Internal building names that should get the drag-a-rectangle behaviour.</summary>
        private static readonly string[] DragPlacingSpawnerNames = new string[]
        {
            "Biome Grass Spawner",   // 沃土 / fertile soil
        };

        /// <summary>Localised display-name keywords used as a fallback matcher.</summary>
        private static readonly string[] DragPlacingSpawnerDisplayKeywords = new string[]
        {
            "沃土", "肥沃", "Fertile Soil", "FertileSoil", "Fertile",
        };

        /// <summary>Non-null while a drag placement of a spawner is active (read by the planner patch).</summary>
        private static SpawnerModel ActiveDragSpawner;

        /// <summary>Safety valve: never spawn more than this many tiles in a single drag.</summary>
        private const int DragPlacingMaxTilesPerDrag = 400;

        /// <summary>Diagnostics switch: log every soil/spawner-like building the player clicks.</summary>
        private const bool VerboseSpawnerDragDiagnostics = true;

        /// <summary>
        /// Emit one diagnostic line for every model that looks like a soil/spawner candidate, so the
        /// log tells us exactly what the game calls the thing the player clicks as "沃土".
        /// </summary>
        private static void LogSpawnerCandidate(BuildingModel model)
        {
            if (!VerboseSpawnerDragDiagnostics || model == null)
                return;

            string name = model.Name ?? string.Empty;
            string typeName = model.GetType().Name;
            bool looksInteresting = model is SpawnerModel
                || name.IndexOf("Spawner", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Biome", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Soil", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Fertile", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!looksInteresting)
                return;

            string display = string.Empty;
            try { display = model.displayName != null ? model.displayName.Text : string.Empty; } catch { }
            FileLog("FC SPAWNER-DRAG: clicked name=" + name + " | display=" + display + " | type=" + typeName
                + " | isInShop=" + model.isInShop + " | matched=" + IsDragPlacingSpawner(model));
        }

        private static bool IsDragPlacingSpawner(BuildingModel model)
        {
            if (model == null || !(model is SpawnerModel))
                return false;

            string name = model.Name ?? string.Empty;
            foreach (string candidate in DragPlacingSpawnerNames)
            {
                if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            try
            {
                string display = model.displayName != null ? model.displayName.Text : null;
                if (!string.IsNullOrEmpty(display))
                {
                    foreach (string keyword in DragPlacingSpawnerDisplayKeywords)
                    {
                        if (display.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        /// <summary>Group request that recreates the vanilla field drag behaviour for a spawner model.</summary>
        internal sealed class SpawnerGroupRequest : GroupRequest
        {
            private readonly SpawnerModel _model;

            public SpawnerGroupRequest(SpawnerModel model, IConstructionPlanner planner, Action onFinished)
                : base(planner, onFinished)
            {
                _model = model;
            }

            public override BuildingModel Model
            {
                get { return _model; }
            }

            public override void Spawn(List<Vector2Int> positions)
            {
                int placed = 0;
                if (positions != null)
                {
                    foreach (Vector2Int pos in positions)
                    {
                        if (placed >= DragPlacingMaxTilesPerDrag)
                        {
                            FileLog("FC SPAWNER-DRAG: hit the per-drag cap of " + DragPlacingMaxTilesPerDrag + " tiles, stopping");
                            break;
                        }

                        Spawner spawner = null;
                        try
                        {
                            if (!GameMB.MapService.IsFieldEmpty(pos))
                                continue;

                            spawner = Instantiate(_model.prefab);
                            if (spawner == null) continue;

                            spawner.SetUp(_model, new SpawnerState(
                                _model,
                                GameMB.StateService.GetUniqueId(),
                                0,
                                GameMB.ConstructionService.GetConstructionCostFor(_model)));
                            spawner.SetPosition(pos.ToWorld());
                            spawner.ManualPlacingFinished();
                            placed++;
                        }
                        catch (Exception ex)
                        {
                            FileLog("ERR SPAWNER-DRAG spawn " + pos + ": " + ex.GetType().Name + ": " + ex.Message);
                            // do not leave a half-built shell behind if placement blew up mid-way
                            try
                            {
                                if (spawner != null && spawner.gameObject != null)
                                    UnityEngine.Object.Destroy(spawner.gameObject);
                            }
                            catch { }
                        }
                    }
                }

                FileLog("FC SPAWNER-DRAG: spawned " + placed + " of " + (positions == null ? 0 : positions.Count) + " tiles for " + _model.Name);

                if (placed > 0)
                {
                    try { SoundsManager.PlaySoundEffect(Sounds.BuildingPlaced); } catch { }
                }
            }
        }

        /// <summary>
        /// Replace the single-building request with our group request for the fertile-soil spawner.
        /// Everything else falls through to the vanilla dispatch untouched, so other mods that patch
        /// this method keep working for every building they care about.
        /// </summary>
        [HarmonyPatch(typeof(BuildingsPanel), "CreateRequest")]
        [HarmonyPrefix]
        private static bool CreateRequest_DragPlacingSpawner(BuildingModel __0, BuildingsPanel __instance, ref IBuildingRequest __result)
        {
            try
            {
                // every new request (road / field / building) resets the flag first, so a cancelled
                // drag can never leave the relaxed tile test active for the next building
                ActiveDragSpawner = null;

                LogSpawnerCandidate(__0);

                if (!IsDragPlacingSpawner(__0))
                    return true;

                IConstructionPlanner planner = null;
                FieldInfo plannerField = AccessTools.Field(typeof(BuildingsPanel), "fieldsPlanner");
                if (plannerField != null)
                    planner = plannerField.GetValue(__instance) as IConstructionPlanner;

                if (planner == null)
                {
                    FileLog("FC SPAWNER-DRAG: fieldsPlanner missing, fallback to vanilla request");
                    return true;
                }

                MethodInfo finished = AccessTools.Method(typeof(BuildingsPanel), "PlacingFinished");
                if (finished == null)
                {
                    FileLog("FC SPAWNER-DRAG: PlacingFinished missing, fallback to vanilla request");
                    return true;
                }

                Action onFinished = (Action)Delegate.CreateDelegate(typeof(Action), __instance, finished);
                __result = new SpawnerGroupRequest((SpawnerModel)__0, planner, onFinished);
                ActiveDragSpawner = (SpawnerModel)__0;
                FileLog("FC SPAWNER-DRAG: group request created for " + __0.Name);
                return false;
            }
            catch (Exception ex)
            {
                FileLog("ERR SPAWNER-DRAG createRequest: " + ex.GetType().Name + ": " + ex.Message);
                return true;
            }
        }

        /// <summary>Clear the drag flag as soon as the group request is torn down.</summary>
        [HarmonyPatch(typeof(GroupRequest), "Dispose")]
        [HarmonyPostfix]
        private static void Dispose_ClearDragSpawner(object __instance)
        {
            if (__instance is SpawnerGroupRequest)
                ActiveDragSpawner = null;
        }

        /// <summary>
        /// The vanilla selector only accepts grass tiles (FieldType 40). A spawner item places soil
        /// itself, so while our request is active every free tile is acceptable.
        /// </summary>
        [HarmonyPatch(typeof(FieldsPlanner), "IsAvaiable")]
        [HarmonyPrefix]
        private static bool IsAvaiable_AllowAnyFreeTile(int __0, int __1, ref bool __result)
        {
            if (ActiveDragSpawner == null)
                return true;

            try
            {
                __result = GameMB.MapService.IsFieldEmpty(new Vector2Int(__0, __1));
            }
            catch (Exception ex)
            {
                FileLog("ERR SPAWNER-DRAG isAvaiable: " + ex.Message);
                __result = false;
            }
            return false;
        }
    }
}
