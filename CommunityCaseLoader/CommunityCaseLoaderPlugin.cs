﻿using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UniverseLib;


#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace CommunityCaseLoader
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class CommunityCaseLoaderPlugin : BaseUnityPlugin
#elif IL2CPP
    public class CommunityCaseLoaderPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        public static bool OverrideMurderDifficulty;
        public static bool OverrideJobDifficulty;

        public static string DEBUG_LoadSpecificMurder;

        public static bool DEBUG_ShowMurderDebugMessages;
        public static bool DEBUG_EnableMODebugging;

        public static string DEBUG_LoadSpecificSideJob;
        public static bool DEBUG_ShowSideJobDebugMessages;
        public static bool DEBUG_ShowSideJobSpawnLocation;

        public static bool DEBUG_ListAllLoadedObjects;

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif
            OverrideMurderDifficulty = Config.Bind("Case Generation", "Override max murder difficulty (Ensures all cases can spawn on a fresh game)", false).Value;
            OverrideJobDifficulty = Config.Bind("Case Generation", "Override max job difficulty (Ensures all jobs can spawn on a fresh game)", false).Value;

            DEBUG_ListAllLoadedObjects = Config.Bind("Debug", "List all loaded objects", false).Value;
            DEBUG_LoadSpecificMurder = Config.Bind("Debug", "Force specific MurderMO", "").Value;
            DEBUG_ShowMurderDebugMessages = Config.Bind("Debug", "Show murder debug messages", false).Value;
            DEBUG_EnableMODebugging = Config.Bind("MurderMO Debugging", "List spawned murder objects and highlight them", false).Value;

            DEBUG_LoadSpecificSideJob = Config.Bind("SideJob Debugging", "Force specific JobPreset", "", "Only one will spawn at a time in this mode for debugging purposes").Value;
            DEBUG_ShowSideJobDebugMessages = Config.Bind("SideJob Debugging", "Show SideJob debug messages", false).Value;
            DEBUG_ShowSideJobSpawnLocation = Config.Bind("SideJob Debugging", "Log where the SideJob spawned", false, "Gives an easy way to find the forced SideJob without spoiling it").Value;

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

            AssetBundleLoader.BundleLoader.loadObjectDelegates.Add(LoadObjectsCallback);
        }

        public List<ScriptableObject> LoadObjectsCallback(Il2CppSystem.Collections.Generic.List<ScriptableObject> loadedScriptableObjects)
        {
            var objectsToLoad = new List<ScriptableObject>();
            var loadedManifests = new List<dynamic>();

            // Search the plugins directory to find any and all murdermanifests, so that people can upload thunderstore mods that purely contain json without code
            var modsToLoadFrom = Directory.GetDirectories(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), ".."), "*", SearchOption.AllDirectories)
                .Select(dirPath => new DirectoryInfo(dirPath))
                .Where(dir => File.Exists(Path.Combine(dir.FullName, "murdermanifest.sodso.json")))
                .ToList();

            foreach (var mod in modsToLoadFrom)
            {
                var manifest = AssetBundleLoader.JsonLoader.NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(mod.FullName, "murdermanifest.sodso.json")));
                manifest["moName"] = mod.Name;
                manifest["folderPath"] = mod.FullName;

                var loadBefore = manifest.Value<string>("loadBefore");
                if (loadBefore == null && loadBefore == "")
                {
                    loadedManifests.Add(manifest);
                }
                else
                {
                    var previousManifest = loadedManifests.Where(previousManifest => previousManifest.Value<string>("moName") == loadBefore).FirstOrDefault();
                    if (previousManifest != null)
                    {
                        loadedManifests.Insert(loadedManifests.IndexOf(previousManifest), manifest);
                    }
                    else
                    {
                        loadedManifests.Add(manifest);
                    }
                }
            }

            foreach (var manifest in loadedManifests)
            {
                LoadManifest(manifest, ref objectsToLoad);
            }

            return objectsToLoad;
        }

        private static void LoadManifest(dynamic manifest, ref List<ScriptableObject> objectsToLoad)
        {
            var moName = manifest.Value<string>("moName");
            var folderPath = manifest.Value<string>("folderPath");

            if (manifest.Value<bool>("enabled"))
            {
                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Loading manifest: {moName}");
            }
            else
            {
                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Not loading manifest: {moName} (Disabled)");
                return;
            }

            List<string> fileContents = new List<string>();

            foreach (var file in manifest["fileOrder"])
            {
                string filePath = Path.Combine(folderPath, file.ToString().Replace("REF:", "") + ".sodso.json");
                // \\?\ is used to bypass MAX_PATH
                if (File.Exists("\\\\?\\" + filePath))
                {
                    var fileContent = File.ReadAllText("\\\\?\\" + filePath);
                    fileContents.Add(fileContent);
                }
                else
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogError($"Failed to load file: {file} (File not found)");
                }
            }

            foreach (var fileContent in fileContents)
            {
                var outputFile = AssetBundleLoader.JsonLoader.LoadFileToGame(fileContent);
                objectsToLoad.Add(outputFile);

                // If it's a sidejob with 
                if (outputFile.GetActualType() == typeof(JobPreset))
                {
                    var preset = UniverseLib.ReflectionExtensions.TryCast<JobPreset>(outputFile);
                    foreach (var spawnItem in preset.spawnItems)
                    {
                        if (spawnItem.vmailThread != "")
                        {
                            // Note that the same thread can't be used across multiple SideJobs types
                            Toolbox_Start.vmailTreeMap[spawnItem.vmailThread] = new JobPresetAndTag() { JobPreset = preset, JobTag = spawnItem.itemTag };
                        }
                    }
                }

                if (CommunityCaseLoaderPlugin.DEBUG_ListAllLoadedObjects)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Loading Object: {outputFile.name}");
                }

            }
        }
    }

   [HarmonyPatch(typeof(Toolbox), "Start")]
    public class Toolbox_Start
    {
        // TODO: Side job mapping only works if the purp is the sender!
        // Save safe, vmailTreeMap builds on the loader, sideJobThreadIDMap builds on vmail send or rebuilds after loading 
        public static Dictionary<string, JobPresetAndTag> vmailTreeMap = new Dictionary<string, JobPresetAndTag>(); // TODO: Murders, too?
        public static Dictionary<int, int> sideJobThreadIDMap = new Dictionary<int, int>();

        public static void Postfix()
        {
            // Force single type for testing
            if (CommunityCaseLoaderPlugin.DEBUG_LoadSpecificMurder != "")
            {
                bool foundSpecificMurder = false;
                CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"Forcing MurderMO: {CommunityCaseLoaderPlugin.DEBUG_LoadSpecificMurder}");
                for (int i = Toolbox.Instance.allMurderMOs.Count - 1; i >= 0; i--)
                {
                    if (Toolbox.Instance.allMurderMOs[i].name != CommunityCaseLoaderPlugin.DEBUG_LoadSpecificMurder)
                        Toolbox.Instance.allMurderMOs[i].disabled = true; // TODO: Cache the current state to allow changing at runtime
                    else
                        foundSpecificMurder = true;
                }

                if (!foundSpecificMurder)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogError($"MurderMO not found: {CommunityCaseLoaderPlugin.DEBUG_LoadSpecificMurder}");
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Loaded MurderMOs:");
                    for (int i = Toolbox.Instance.allMurderMOs.Count - 1; i >= 0; i--)
                    {
                        CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"\t{Toolbox.Instance.allMurderMOs[i].name}");
                    }
                }
            }

            if (CommunityCaseLoaderPlugin.DEBUG_LoadSpecificSideJob != "")
            {
                CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"Forcing SideJob: {CommunityCaseLoaderPlugin.DEBUG_LoadSpecificSideJob}");
                for (int i = SideJobController.Instance.jobTracking.Count - 1; i >= 0; i--)
                {
                    if (SideJobController.Instance.jobTracking[i].name != CommunityCaseLoaderPlugin.DEBUG_LoadSpecificSideJob)
                        SideJobController.Instance.jobTracking.RemoveAt(i);
                    else
                    {
                        SideJobController.Instance.jobTracking[i].preset.disabled = false;
                        SideJobController.Instance.jobTracking[i].preset.maxJobs = 1;
                        SideJobController.Instance.jobTracking[i].preset.immediatePostCountThreshold = 100;
                    }
                }
            }
            else
            {
                // For now, turn off the other debugging options if there is nothing specified
                CommunityCaseLoaderPlugin.DEBUG_ShowSideJobDebugMessages = false;
                CommunityCaseLoaderPlugin.DEBUG_ShowSideJobSpawnLocation = false;
            }
        }
    }

    [HarmonyPatch]
    public class Toolbox_NewVmailThread
    {
        [HarmonyTargetMethod]
        internal static System.Reflection.MethodBase CalculateMethod()
        {
            var mi = typeof(Toolbox).GetMethods().Where(mi => mi.Name == "NewVmailThread" && mi.GetParameters().Length == 10).First();
            return mi;
        }

        public static void Postfix(StateSaveData.MessageThreadSave __result, Human from, ref Human to1, ref Human to2, ref Human to3, ref List<Human> cc, string treeID, float timeStamp, int progress = 999)
        {
            if (Toolbox_Start.vmailTreeMap.ContainsKey(treeID))
            {
                foreach (var job in SideJobController.Instance.allJobsDictionary)
                {
                    if (job.Value.purpID == from.humanID && job.Value.presetStr == Toolbox_Start.vmailTreeMap[treeID].JobPreset.presetName)
                    {
                        Toolbox_Start.sideJobThreadIDMap[job.value.jobID] = __result.threadID;
                        if (CommunityCaseLoaderPlugin.DEBUG_ShowSideJobDebugMessages)
                            CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Mapped jobID {job.value.jobID} to threadID {__result.threadID} on creation");
                        break;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(SaveStateController), nameof(SaveStateController.LoadSaveState))]
    public class SaveStateController_LoadSaveState
    {
        public static void Postfix(SaveStateController __instance)
        {
            foreach (var messageThreadPair in GameplayController.Instance.messageThreads)
            {
                if (Toolbox_Start.vmailTreeMap.ContainsKey(messageThreadPair.Value.treeID))
                {
                    var humanId = messageThreadPair.Value.senders[0];

                    foreach (var job in SideJobController.Instance.allJobsDictionary.Values)
                    {
                        if (Toolbox_Start.vmailTreeMap[messageThreadPair.Value.treeID].JobPreset.presetName == job.preset.presetName && job.purpID == humanId)
                        {
                            Toolbox_Start.sideJobThreadIDMap[job.jobID] = messageThreadPair.Value.threadID;
                            if (CommunityCaseLoaderPlugin.DEBUG_ShowSideJobDebugMessages)
                                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Mapped jobID {job.jobID} to threadID {messageThreadPair.Value.threadID} on load");
                        }
                    }

                    break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Case.ResolveQuestion), nameof(Case.ResolveQuestion.UpdateCorrect))]
    public class CaseResolveQuestion_UpdateCorrect
    {
        public static bool Prefix(Case.ResolveQuestion __instance, Case forCase, ref bool __result)
        {
            // Is this a job we are tracking?
            if (forCase?.job != null && Toolbox_Start.sideJobThreadIDMap.ContainsKey(forCase.job.jobID))
            {
                var evidenceId = __instance.inputtedEvidence;
                foreach (var caseEle in forCase.caseElements)
                {
                    // Find the case element that matches the submitted evidence
                    if (caseEle.id == evidenceId)
                    {
                        // Check it's a vmail
                        var vmailEvi = caseEle.pinnedController.evidence.TryCast<EvidencePrintedVmail>();
                        if (vmailEvi != null)
                        {
                            // Check the thread is one we know, and the tag matches the tag associated with this spawnItem
                            if (Toolbox_Start.vmailTreeMap.ContainsKey(vmailEvi.thread.treeID) && __instance.tag == Toolbox_Start.vmailTreeMap[vmailEvi.thread.treeID].JobTag)
                            {
                                __instance.isCorrect = vmailEvi.thread.threadID == Toolbox_Start.sideJobThreadIDMap[forCase.job.jobID];
                                __result = __instance.isCorrect;
                                return false;
                            }
                        }

                        // caseEle.pinnedController.evidence.evID
                        // Check for specific evidence that we wanted
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CityConstructor), nameof(CityConstructor.StartGame))]
    public class CityConstructor_StartGame
    {
        public static void Prefix()
        {
            if(CommunityCaseLoaderPlugin.OverrideMurderDifficulty)
            {
                MurderController.Instance.maxDifficultyLevel = 10;
            }

            if (CommunityCaseLoaderPlugin.OverrideJobDifficulty)
            {
                GameplayController.Instance.SetJobDifficultyLevel(5);
            }
        }
    }

    public class JobPresetAndTag
    {
        public JobPreset JobPreset;
        public JobPreset.JobTag JobTag;
    }

    /*
    // Failing because of ref params
    [HarmonyPatch]
    public class MurderController_SpawnItemIsValid
    {
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

    #region Debug helper hooks

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

    #region Murder Spawned Items

    [HarmonyPatch(typeof(MurderController), nameof(MurderController.SpawnItem))]
    public class MurderController_SpawnItem
    {
        public static List<Interactable> murderInteractables = new List<Interactable>();

        public static void Postfix(MurderController __instance, Interactable __result)
        {
            if (CommunityCaseLoaderPlugin.DEBUG_EnableMODebugging && __result.preset != null)
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

    [HarmonyPatch(typeof(Interactable), nameof(Interactable.LoadInteractableToWorld))]
    public class Interactable_LoadInteractableToWorld
    {
        private const int HIGHLIGHTED_LAYER = 30;
        
        public static void Prefix(Interactable __instance, ref bool forceSpawnImmediate)
        {
            if (MurderController_SpawnItem.murderInteractables.Contains(__instance))
            {
                forceSpawnImmediate = true;
            }
        }

        public static void Postfix(Interactable __instance)
        {
            if (MurderController_SpawnItem.murderInteractables.Contains(__instance))
            {
                __instance.spawnedObject.layer = HIGHLIGHTED_LAYER;
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
                string othersList = "None";

                if (otherParticipiants != null && otherParticipiants.Count > 0)
                {
                    othersList = "";
                    foreach (var other in otherParticipiants)
                    {
                        othersList += other == null ? "null," : (other.citizenName + ",");
                    }
                }

                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"MurderMO ({CurrentMurderMO?.presetName}) Sending VMail: '{treeID}', from {from.citizenName}. Other participants: {othersList}");
            }
        }
    }
    #endregion

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
}