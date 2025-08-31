using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Assertions.Must;

namespace CommunityCaseLoader
{
    internal class DebuggingHooks
    {
        [HarmonyPatch(typeof(MurderController), nameof(MurderController.PickNewVictim))]
        public class MurderController_PickNewVictim
        {
            public static void Postfix()
            {
                if (CommunityCaseLoaderPlugin.DEBUG_ShowMurderDebugMessages)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"MurderMO: {MurderController.Instance.chosenMO.presetName}");
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Murderer: {MurderController.Instance.currentMurderer?.name} (Works: {MurderController.Instance.currentMurderer.job?.employer?.name}, Lives: {MurderController.Instance.currentMurderer?.home.name})");
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Victim: {MurderController.Instance.currentVictim?.name} (Works: {MurderController.Instance.currentVictim?.job?.employer?.name}, Lives: {MurderController.Instance.currentVictim?.home.name}))");
                }
            }
        }

        [HarmonyPatch(typeof(Interactable), nameof(Interactable.LoadInteractableToWorld))]
        public class Interactable_LoadInteractableToWorld
        {
            private const int HIGHLIGHTED_LAYER = 30;

            public static void Prefix(Interactable __instance, ref bool forceSpawnImmediate)
            {
                if (
                    MurderController_SpawnItem.murderInteractables.Contains(__instance) ||
                    CommunityCaseLoaderPlugin.DEBUG_DebugSpecificInteractable.Value == __instance.preset.name
                    )
                {
                    forceSpawnImmediate = true;
                }
            }

            public static void Postfix(Interactable __instance)
            {
                if (
                    MurderController_SpawnItem.murderInteractables.Contains(__instance) ||
                    CommunityCaseLoaderPlugin.DEBUG_DebugSpecificInteractable.Value == __instance.preset.name
                    )
                {
                    __instance.spawnedObject.layer = HIGHLIGHTED_LAYER;
                }
            }
        }

        #region Murder Spawned Items

        [HarmonyPatch(typeof(MurderController), nameof(MurderController.SpawnItem))]
        public class MurderController_SpawnItem
        {
            public static List<Interactable> murderInteractables = new List<Interactable>();

            public static void Postfix(MurderController __instance, Interactable __result)
            {
                if (CommunityCaseLoaderPlugin.DEBUG_EnableMODebugging && __result?.preset != null)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"MurderMO ({__instance.chosenMO.name}) Spawning Item: {__result.preset.name}");
                    murderInteractables.Add(__result);
                }
            }
        }

        [HarmonyPatch(typeof(MurderController.Murder), nameof(MurderController.Murder.PlaceCallingCard))]
        public class MurderController_PlaceCallingCard
        {
            private const int HIGHLIGHTED_LAYER = 30;
            public static void Postfix(MurderController.Murder __instance)
            {
                if (CommunityCaseLoaderPlugin.DEBUG_EnableMODebugging && __instance.callingCard != null)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"MurderMO ({__instance.preset.name}) Spawning Calling Card: {__instance.callingCard.preset.name}");
                    MurderController_SpawnItem.murderInteractables.Add(__instance.callingCard);
                    __instance.callingCard.spawnedObject.layer = HIGHLIGHTED_LAYER;
                }
            }
        }

        // Only enable vmail checks during the SpawnItemsCheck function
        [HarmonyPatch(typeof(MurderController), nameof(MurderController.SpawnItemsCheck))]
        public class MurderController_SpawnItemsCheck
        {
            internal static void Prefix(MurderController __instance)
            {
                Toolbox_NewVmailThread_Debug.LoggingEnabled = true;
                Toolbox_NewVmailThread_Debug.CurrentMurderMO = __instance.chosenMO;
            }

            internal static void Postfix()
            {
                Toolbox_NewVmailThread_Debug.LoggingEnabled = false;
                Toolbox_NewVmailThread_Debug.CurrentMurderMO = null;
            }
        }

        [HarmonyPatch]
        public class Toolbox_NewVmailThread_Debug
        {
            public static bool LoggingEnabled = false;
            public static MurderMO CurrentMurderMO = null;

            [HarmonyTargetMethod]
            internal static System.Reflection.MethodBase CalculateMethod()
            {
                var mi = typeof(Toolbox).GetMethods().Where(mi => mi.Name == "NewVmailThread" && mi.GetParameters().Length == 7).First();
                return mi;
            }

            public static void Postfix(Human from, Il2CppSystem.Collections.Generic.List<Human> otherParticipiants, string treeID, float timeStamp, int progress, StateSaveData.CustomDataSource overrideDataSource, int newDataSourceID)
            {
                if (LoggingEnabled && CommunityCaseLoaderPlugin.DEBUG_EnableMODebugging)
                {
                    string fromName = from != null ? from.citizenName : "None";
                    string othersList = "None";

                    if (otherParticipiants != null && otherParticipiants.Count > 0)
                    {
                        othersList = "";
                        foreach (var other in otherParticipiants)
                        {
                            othersList += other == null ? "null," : (other.citizenName + ",");
                        }
                    }

                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"MurderMO ({CurrentMurderMO?.presetName}) Sending VMail: '{treeID}', from {fromName}. Other participants: {othersList}");
                }
            }
        }

        /*
        // Failing because of ref params
        [HarmonyPatch(typeof(MurderController), nameof(MurderController.SpawnItemIsValid))]
        public class MurderController_SpawnItemIsValid
        {
            /*
            public static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method(
                    typeof(MurderController),
                    nameof(MurderController.SpawnItemIsValid),
                    new System.Type[]
                    {
                        typeof(JobPreset.StartingSpawnItem),
                        typeof(Il2CppSystem.Collections.Generic.List<JobPreset.StartingSpawnItem>).MakeByRefType(),
                        typeof(bool)
                    });
            }
            *

            public static void Postfix(bool __result, MurderPreset.MurderLeadItem spawn)
            {
                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Ref type");
                if (CommunityCaseLoaderPlugin.DEBUG_ShowMurderDebugMessages)
                {
                    if (!__result)
                    {
                        CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Not spawning murder lead item: {spawn.name}");
                    }
                }
            }
        }
        */
        #endregion

        #region SideJob Spawned Items

        // Logging for SideJob spawning
        [HarmonyPatch(typeof(SideJob), nameof(SideJob.PostJob))]
        public class SideJob_PostJob
        {
            public static void Postfix(SideJob __instance)
            {
                if (CommunityCaseLoaderPlugin.DEBUG_ShowSideJobDebugMessages || CommunityCaseLoaderPlugin.DEBUG_ShowSideJobSpawnLocation)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"SideJob: Posted at {__instance?.post?.node?.gameLocation?.name}");
                }

                if (CommunityCaseLoaderPlugin.DEBUG_ShowSideJobDebugMessages)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"SideJob: Posted about {__instance?.purp?.name} (Works at {__instance?.purp?.job?.employer?.name})");
                }
            }
        }

        // Doesn't work due to ref parameters
        /*
        [HarmonyPatch]
        public class SideJob_SpawnItemIsValid
        {
            public static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method(
                    typeof(SideJob),
                    nameof(SideJob.SpawnItemIsValid),
                    new System.Type[]
                    {
                        typeof(JobPreset.StartingSpawnItem),
                        typeof(Il2CppSystem.Collections.Generic.List<>).MakeByRefType(),
                        typeof(bool)
                    });
            }

            public static void Postfix(SideJob __instance, bool __result, JobPreset.StartingSpawnItem spawn, ref Il2CppSystem.Collections.Generic.List<JobPreset.StartingSpawnItem> successsfullySpawned, bool useChance)
            {
                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"postfix");
            }
        }
        */

        #endregion

        #region Generic Debugging Helpers

        [HarmonyPatch(typeof(Interactable), nameof(Interactable.OnCreate))]
        public class Interactable_OnCreate
        {
            public static void Postfix(Interactable __instance)
            {
                if(CommunityCaseLoaderPlugin.DEBUG_DebugSpecificInteractable.Value == __instance.preset.name)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Spawned {__instance.preset.name} at {__instance?.node?.gameLocation?.thisAsAddress?.name} (Belongs to {__instance.belongsTo?.citizenName ?? "Nobody"})");

                    if(!__instance?.node?.gameLocation?.thisAsAddress)
                    {
                        CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Unknown address, trying gameLocation: {__instance?.node?.gameLocation?.name}");
                    }
                }
            }
        }

        #endregion
    }
}
