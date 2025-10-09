using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SOD.Common.Extensions;
using AssetBundleLoader;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace BuildingLoader
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class BuildingLoaderPlugin : BaseUnityPlugin
#elif IL2CPP
    public class BuildingLoaderPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        public static bool DEBUG_ListAllLoadedObjects;

        static List<dynamic> loadedManifests = new List<dynamic>();

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            DEBUG_ListAllLoadedObjects = Config.Bind("Debug", "List all loaded objects", false).Value;

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

            // AssetBundleLoader.BundleLoader.loadObjectDelegates.Add(LoadObjectsCallback);
        }

        public List<ScriptableObject> LoadObjectsCallback(Il2CppSystem.Collections.Generic.List<ScriptableObject> loadedScriptableObjects)
        {
            var objectsToLoad = new List<ScriptableObject>();
            var modsToLoadFrom = new List<System.IO.DirectoryInfo> { new System.IO.DirectoryInfo(@"D:\Game Modding\ShadowsOfDoubt\_Mods\ShadowsOfDoubtMods\BuildingLoader\ModFolderContent\plugins\GamingArcade\") };

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

            /*
            if (manifest.Value<bool>("enabled"))
            {
                BuildingLoaderPlugin.PluginLogger.LogInfo($"Loading Building Manifest: {moName}");
            }
            else
            {
                BuildingLoaderPlugin.PluginLogger.LogInfo($"Not Loading Building Manifest: {moName} (Disabled)");
                return;
            }
            */

            List<(string fileName, string content)> files = new List<(string, string)>();

            foreach (var file in manifest["fileOrder"])
            {
                string filePath = Path.Combine(folderPath, file.ToString().Replace("REF:", "") + ".sodso.json");
                if (File.Exists(filePath))
                {
                    var fileContent = File.ReadAllText(filePath);
                    files.Add((file, fileContent));
                }
                else
                {
                    BuildingLoaderPlugin.PluginLogger.LogError($"Failed to load file: {file} (File not found)");
                }
            }

            foreach (var file in files)
            {
                if (BuildingLoaderPlugin.DEBUG_ListAllLoadedObjects)
                {
                    BuildingLoaderPlugin.PluginLogger.LogInfo($"Loading Object: {file.fileName}");
                }

                var outputFile = AssetBundleLoader.JsonLoader.LoadFileToGame(file.content);

                objectsToLoad.Add(outputFile);

                if (outputFile.name == "ArcadeMachinePreset")
                {
                    BuildingLoaderPlugin.PluginLogger.LogInfo($"Starting ArcadeMachine");

                    BuildingLoaderPlugin.PluginLogger.LogInfo($"Found row");
                    var moddedAssetBundle = BundleLoader.LoadBundle(Path.Combine(folderPath, "arcadebusiness"), true, true);
                    GameObject arcadeMachineGO = moddedAssetBundle.LoadAsset<GameObject>("arcadeMachinePrefab");

                    DeepUpdateMaterialsInTransform(arcadeMachineGO.transform);

                    ((dynamic)outputFile).Cast<FurniturePreset>().prefab = arcadeMachineGO;

                    BuildingLoaderPlugin.PluginLogger.LogInfo($"Loaded assets:");
                }
            }
        }

        /// <summary>
        /// Materials built in Unity aren't linked to the normal shader, so swap them
        /// </summary>
        /// <param name="transform">The transform to find all MeshRenderers on</param>
        private static void DeepUpdateMaterialsInTransform(Transform transform)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                DeepUpdateMaterialsInTransform(transform.GetChild(i));
            }

            var renderer = transform.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // If it was built using the standard sharder in Unity, swap it to the version in the game
                if(renderer.sharedMaterial.shader.name == "HDRP/Lit")
                {
                    renderer.sharedMaterial.shader = Shader.Find("HDRP/Lit");
                    renderer.sharedMaterial.renderQueue = 2225;
                }
            }
        }
    }

    #region Debugging

    [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Start))]
    public class Toolbox_Start
    {
        public static void Postfix()
        {
            foreach (var addr in Toolbox.Instance.allAddressPresets)
            {
                if (addr.presetName == "Launderette")
                {
                    addr.debug = true;
                    break;
                }
            }

            foreach (var cluster in Toolbox.Instance.allFurnitureClusters)
            {
                cluster.enableDebug = false;
                if (cluster.name == "WashingMachinesBackToBackX5")
                {
                    cluster.disable = true;
                    break;
                }
            }

            foreach(var buildingPreset in AssetLoader.Instance.GetAllBuildingPresets())
            {
                if(buildingPreset.name == "CityHall")
                {
                    buildingPreset.controlRoomRange = new Vector2(1, 1);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GenerationController), nameof(GenerationController.GetBestFurnitureClusterLocation))]
    public class GenerationController_GetBestFurnitureClusterLocation
    {
        public static void Prefix(NewRoom room, ref FurnitureCluster cluster, ref bool enableDebug, ref bool ignoreLimitations)
        {
            // if(cluster.presetName.Contains("Wash") || cluster.presetName.Contains("ATM"))
            // if (room.name.Contains("Launderette"))
            if(room.gameLocation.name == "Chapman Street Suds")
            {
                enableDebug = true;
                Game.Instance.collectDebugData = true;
                Game.Instance.printDebug = true;


                if(cluster.presetName == "ArcadeMachineCluster")
                {
                    cluster.enableDebug = true;
                    ignoreLimitations = true;
                }

                if(cluster.presetName == "WashingMachinesAgainstWallX5")
                {
                    cluster.enableDebug = true;
                }

                BuildingLoaderPlugin.PluginLogger.LogInfo($"Washing Machines at: {room.name} of type {room.preset.name} ({room.roomID}) Cluster: {cluster.presetName}");
            }
        }

        public static void Postfix(bool enableDebug, NewRoom room, ref FurnitureCluster cluster, FurnitureClusterLocation __result)
        {
            if (enableDebug)
            {
                BuildingLoaderPlugin.PluginLogger.LogInfo($"Cluster debug result for {room.name} ({room.roomID}): {__result?.cluster.name} {__result?.ranking}");
                cluster.enableDebug = false;
            }
        }
    }

    [HarmonyPatch(typeof(GenerationController), nameof(GenerationController.FurnishRoom))]
    public class FurnishRoom
    {
        public static void Prefix(NewRoom room, ref bool __state)
        {
            __state = false;
            // if(cluster.presetName.Contains("Wash") || cluster.presetName.Contains("ATM"))
            // if (room.name.Contains("Launderette"))
            if (room.gameLocation.name == "Chapman Street Suds")
            {
                __state = true;

                // Game.Instance.collectDebugData = true;
                // Game.Instance.printDebug = true;


                foreach(var cluster in Toolbox.Instance.allFurnitureClusters.Where(cluster => new[] { "ArcadeMachineCluster", "WashingMachinesAgainstWallX5" }.Contains(cluster.presetName)))
                {
                    cluster.enableDebug = true;
                }

                BuildingLoaderPlugin.PluginLogger.LogInfo($"FurnishRoom Start {room.name} of type {room.preset.name} ({room.roomID})");
            }
        }

        public static void Postfix(NewRoom room, bool __state)
        {
            if (__state)
            {
                Game.Instance.collectDebugData = false;
                Game.Instance.printDebug = false;

                foreach (var cluster in Toolbox.Instance.allFurnitureClusters.Where(cluster => new[] { "ArcadeMachineCluster", "WashingMachinesAgainstWallX5" }.Contains(cluster.presetName)))
                {
                    cluster.enableDebug = false;
                }

                BuildingLoaderPlugin.PluginLogger.LogInfo($"FurnishRoom Complete {room.name} ({room.roomID})");
            }
        }
    }
    
    // [HarmonyPatch(typeof(GenerationController), nameof(GenerationController.PickFurniture))]
    public class PickFurniture
    {
        public static void Prefix(GenerationController __instance, FurnitureClass furnClass, NewRoom room, ref bool debug, bool ignoreLimitations, DesignStylePreset styleOverride)
        {
            if (room.name == "Chapman Street Suds Launderette" && room.preset.name == "Launderette")
            {
                debug = true;
                GetValidFurniture(furnClass, room, returnList: false, out _, debug: true, ignoreLimitations, styleOverride);
            }
        }

        public static void Postfix(FurniturePreset __result, FurnitureClass furnClass, NewRoom room, bool debug)
        {
            if(debug)
            {
                BuildingLoaderPlugin.PluginLogger.LogInfo($"PickFurniture Result for {room.name} ({room.roomID}): {furnClass.presetName} gave {__result?.name}");
            }
        }

        public static bool GetValidFurniture(FurnitureClass furnClass, NewRoom room, bool returnList, out List<FurniturePreset> possibleFurniture, bool debug = true, bool ignoreLimitations = false, DesignStylePreset _designStyleOverride = null)
        {
            float normalizedLandValue = Toolbox.Instance.GetNormalizedLandValue(room.gameLocation);
            possibleFurniture = null;

            var designStyleOverride = _designStyleOverride;

            if (designStyleOverride == null)
            {
                designStyleOverride = room.gameLocation.designStyle;
                Game.Log("Using room design style");
            }
            Game.Log($"Using design style: {designStyleOverride.name}");

            foreach (FurniturePreset item in Toolbox.Instance.furnitureDesignStyleRef[designStyleOverride])
            {
                if (item == null)
                {
                    continue;
                }
                if (debug)
                {
                    Game.Log("---" + item.name + "---");
                }
                if (!ignoreLimitations && normalizedLandValue < item.minimumWealth)
                {
                    if (debug)
                    {
                        Game.Log("... Below minimum wealth of " + item.minimumWealth);
                    }
                }
                else if (!ignoreLimitations && room.nodes.Count < item.minimumRoomSize)
                {
                    if (debug)
                    {
                        Game.Log("... Below minimum room size of " + item.minimumRoomSize);
                    }
                }
                else
                {
                    if (!ignoreLimitations && item.allowedInOpenPlan == FurnitureCluster.AllowedOpenPlan.no && room.openPlanElements.Count > 0)
                    {
                        continue;
                    }
                    if (!item.classes.Contains(furnClass))
                    {
                        if (debug)
                        {
                            Game.Log("... Not in class " + furnClass.name);
                        }
                        continue;
                    }
                    if (item.furnitureGroup != 0 && room.furnitureGroups.ContainsKey(item.furnitureGroup) && item.groupID != room.furnitureGroups[item.furnitureGroup])
                    {
                        if (debug)
                        {
                            Game.Log("... Furniture group doesn't match: " + room.furnitureGroups[item.furnitureGroup]);
                        }
                        continue;
                    }
                    if (item.isSecurityCamera && room.preset.limitSecurityCameras && room.individualFurniture.ToList().FindAll((FurnitureLocation item) => item.furniture.isSecurityCamera).Count >= room.preset.securityCameraLimit)
                    {
                        if (debug)
                        {
                            Game.Log("... Security camera limit...");
                        }
                        continue;
                    }
                    if (debug)
                    {
                        Game.Log("... Checking room compatibility...");
                    }
                    if ((!SessionData.Instance.isFloorEdit && ((item.OnlyAllowInBuildings && !ignoreLimitations && (room.gameLocation.building == null || !item.allowedInBuildings.Contains(room.gameLocation.building.preset))) || (item.banFromBuildings && !ignoreLimitations && room.gameLocation.building != null && item.notAllowedInBuildings.Contains(room.gameLocation.building.preset)))) || (!SessionData.Instance.isFloorEdit && ((item.OnlyAllowInDistricts && !ignoreLimitations && !item.allowedInDistricts.Contains(room.gameLocation.district.preset)) || (item.banFromDistricts && !ignoreLimitations && item.notAllowedInDistricts.Contains(room.gameLocation.district.preset)))))
                    {
                        continue;
                    }
                    if (item.requiresGenderedInhabitants && !ignoreLimitations)
                    {
                        if (!(room.gameLocation.thisAsAddress != null))
                        {
                            continue;
                        }
                        bool flag = true;
                        foreach (Human inhabitant in room.gameLocation.thisAsAddress.inhabitants)
                        {
                            if (!item.enableIfGenderPresent.Contains(inhabitant.gender))
                            {
                                flag = false;
                            }
                        }
                        if (!flag)
                        {
                            continue;
                        }
                    }
                    if (item.onlyAllowInFollowing && !ignoreLimitations)
                    {
                        if (!(room.gameLocation.thisAsAddress != null) || !(room.gameLocation.thisAsAddress.addressPreset != null))
                        {
                            if (debug)
                            {
                                Game.Log("... Not assigned an address preset");
                            }
                            continue;
                        }
                        if (!item.allowedInAddressesOfType.Contains(room.gameLocation.thisAsAddress.addressPreset))
                        {
                            if (debug)
                            {
                                Game.Log("... Not allowed in address " + room.gameLocation.thisAsAddress.addressPreset.name);
                            }
                            continue;
                        }
                    }
                    if (item.banInFollowing && !ignoreLimitations && room.gameLocation.thisAsAddress != null && room.gameLocation.thisAsAddress.addressPreset != null && item.bannedInAddressesOfType.Contains(room.gameLocation.thisAsAddress.addressPreset))
                    {
                        if (debug)
                        {
                            Game.Log("... Banned in address " + room.gameLocation.thisAsAddress.addressPreset.name);
                        }
                        continue;
                    }
                    bool flag2 = false;
                    if (ignoreLimitations)
                    {
                        flag2 = true;
                    }
                    else
                    {
                        foreach (RoomTypeFilter allowedRoomFilter in item.allowedRoomFilters)
                        {
                            if (allowedRoomFilter == null)
                            {
                                Game.LogError("Null filter found in " + item.name);
                                continue;
                            }
                            Game.Log($"... Allowed in open area {item.allowedInOpenPlan.ToString()}...");

                            if (item.allowedInOpenPlan != FurnitureCluster.AllowedOpenPlan.openPlanOnly)
                            {
                                Il2CppSystem.Collections.Generic.HashSet<FurniturePreset> value = null;
                                if (Toolbox.Instance.furnitureRoomTypeRef.TryGetValue(room.preset.roomClass, out value) && value.Contains(item))
                                {
                                    Game.Log($"...Passed room check (not restricted to open plan) ({room.preset.name})...");
                                    flag2 = true;
                                    break;
                                }
                            }
                            foreach (RoomConfiguration openPlanElement in room.openPlanElements)
                            {
                                if (item.allowedInOpenPlan != FurnitureCluster.AllowedOpenPlan.no)
                                {
                                    Il2CppSystem.Collections.Generic.HashSet<FurniturePreset> value2 = null;
                                    if (Toolbox.Instance.furnitureRoomTypeRef.TryGetValue(openPlanElement.roomClass, out value2) && value2.Contains(item))
                                    {
                                        Game.Log($"...Pass RoomConfig check ({openPlanElement.name})...");
                                        flag2 = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (!flag2)
                    {
                        if (debug)
                        {
                            Game.Log("... Open plan/filter check failed.");
                        }
                        continue;
                    }
                    if (!returnList)
                    {
                        return true;
                    }
                    if (possibleFurniture == null)
                    {
                        possibleFurniture = new List<FurniturePreset>();
                    }
                    possibleFurniture.Add(item);
                }
            }
            if (possibleFurniture == null || possibleFurniture.Count <= 0)
            {
                return false;
            }
            return true;
        }
    }

    /*
    [HarmonyPatch(typeof(GenerationController), nameof(GenerationController.GetValidFurniture))]
    public class GetValidFurniture
    {
        public static void Prefix(FurnitureClass furnClass, NewRoom room, ref bool debug)
        {
            if (room.name == "Chapman Street Suds Launderette" && room.preset.name == "Launderette")
            {
                debug = true;
            }
        }
    }
    */

    #endregion
}