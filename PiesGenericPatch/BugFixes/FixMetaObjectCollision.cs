using HarmonyLib;
using System;

namespace Pies_Generic_Patch.BugFixes
{
    /// <summary>
    /// Stops object IDs (mainly credit cards and blood donor cards) being handed out twice when starting a new game on an existing city.
    ///
    /// StartLoading resets
    /// Interactable.worldAssignID to 10000000, prepareCitizens (16) runs before loadObjects (17), and
    /// nothing reserves the IDs the city file already uses, so the MetaObjects minted during citizen
    /// prep take IDs belonging to the city's own metas.
    /// </summary>
    public class FixMetaObjectCollision
    {
        public static void DoPatch(Harmony harmony)
        {
            harmony.PatchAll(typeof(FixMetaObjectCollision.CityConstructor_GatherData));
        }

        // Runs before generateEvidence, generateInteriors and prepareCitizens, and after currentData is parsed
        [HarmonyPatch(typeof(CityConstructor), nameof(CityConstructor.GatherData))]
        public class CityConstructor_GatherData
        {
            public static void Postfix()
            {
                try
                {
                    ReserveLoadedIds();
                }
                catch (Exception e)
                {
                    Pies_Generic_PatchPlugin.Log.LogError($"Failed to reserve loaded meta object IDs: {e}");
                }
            }
        }

        private static void ReserveLoadedIds()
        {
            var constructor = CityConstructor.Instance;

            // On a fresh generation currentData is not populated until savingData, and the dictionary
            // fills as the city is built, so there is nothing to reserve
            if (constructor == null || constructor.generateNew || constructor.currentData == null)
                return;

            var highestId = Interactable.worldAssignID - 1;
            var registered = 0;

            var metas = constructor.currentData.metas;

            if (metas != null)
            {
                var liveMetas = CityData.Instance.metaObjectDictionary;

                foreach (var meta in metas)
                {
                    if (meta == null)
                        continue;

                    if (meta.id > highestId)
                        highestId = meta.id;

                    if (liveMetas.ContainsKey(meta.id))
                        continue;

                    liveMetas.Add(meta.id, meta);
                    registered++;
                }
            }

            var interactables = constructor.currentData.interactables;

            if (interactables != null)
            {
                foreach (var interactable in interactables)
                {
                    if (interactable != null && interactable.id > highestId)
                        highestId = interactable.id;
                }
            }

            var previousAssignId = Interactable.worldAssignID;
            Interactable.worldAssignID = highestId + 1;

            if (Pies_Generic_PatchPlugin.DebugLogging.Value)
                Pies_Generic_PatchPlugin.Log.LogInfo($"Meta objects: registered {registered} city meta objects early, moved worldAssignID from {previousAssignId} to {Interactable.worldAssignID}");
        }
    }
}
