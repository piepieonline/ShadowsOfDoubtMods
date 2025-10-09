using HarmonyLib;
using Il2CppInterop.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UniverseLib;

namespace BuildingLoader
{
    internal class FloorDataOverrides
    {
        [HarmonyPatch(typeof(CityData), nameof(CityData.ParseFloorData))]
        public class CityData_ParseFloorData
        {
            public static void Postfix()
            {
                BuildingLoaderPlugin.PluginLogger.LogError($"Parsing FloorData");
                

                TextAsset customParkGround = new TextAsset(System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "plugins", "CustomPark", "CustomParkGroundFloor.json")));
                customParkGround.name = "CustomParkGroundFloor";
                CityData.Instance.floorData.Add(customParkGround.name, JsonUtility.FromJson<FloorSaveData>(customParkGround.text));
                TextAsset customParkRoof = new TextAsset(System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "plugins", "CustomPark", "CustomParkRoof.json")));
                customParkRoof.name = "CustomParkRoof";
                CityData.Instance.floorData.Add(customParkRoof.name, JsonUtility.FromJson<FloorSaveData>(customParkRoof.text));
                
                foreach (var buildingPreset in AssetLoader.Instance.allBuildingData)
                {
                    if (buildingPreset.name == "CustomParkBuildingPreset")
                    {
                        buildingPreset.floorLayouts[0].blueprints.Add(customParkGround);
                        buildingPreset.floorLayouts[1].blueprints.Add(customParkRoof);
                        BuildingLoaderPlugin.PluginLogger.LogWarning($"Found CustomPark and added blueprint");
                    }
                }

                SOD.Common.Lib.DdsStrings["names.rooms", "customparkbuildingpreset"] = "Custom Park";
            }
        }

        [HarmonyPatch(typeof(AssetLoader), nameof(AssetLoader.GetAllFloorData))]
        public class AssetLoader_GetAllFloorData
        {
            public static void Postfix(ref Il2CppSystem.Collections.Generic.List<TextAsset> __result)
            {
                TextAsset newAsset;
                BuildingLoaderPlugin.PluginLogger.LogError($"Loaded {__result.Count} FloorDataSave TextAssets");

                var floorToSwap = "Park_GroundFloor01";
                // var floorToSwap = "CityHall_GroundFloor";

                // Disabled
                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    if (false && __result[i].name == floorToSwap) // Disabled floor swapping, working on new buildings for now
                    {
                        __result.RemoveAt(i);
                        newAsset = new TextAsset(System.IO.File.ReadAllText(@"E:\UnityDev\SodBuildingVisualiser\Assets\FloorSaves\testNewBuilding.json"));
                        newAsset.name = floorToSwap;
                        __result.Add(newAsset);

                        BuildingLoaderPlugin.PluginLogger.LogWarning($"Replaced {floorToSwap}");
                    }
                }
                // End disabled

                TextAsset customParkGround = new TextAsset(System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "plugins", "CustomPark", "CustomParkGroundFloor.csv")));
                customParkGround.name = "CustomParkGroundFloor";
                TextAsset customParkRoof = new TextAsset(System.IO.File.ReadAllText(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "plugins", "CustomPark", "CustomParkRoof.csv")));
                customParkRoof.name = "CustomParkRoof";
                
                foreach (var buildingPreset in AssetLoader.Instance.allBuildingData)
                {
                    if (buildingPreset.name == "CustomParkBuildingPreset")
                    {
                        buildingPreset.floorLayouts[0].blueprints.Add(customParkGround);
                        buildingPreset.floorLayouts[1].blueprints.Add(customParkRoof);
                        BuildingLoaderPlugin.PluginLogger.LogWarning($"Found CustomPark and added blueprint");
                    }
                }

                SOD.Common.Lib.DdsStrings["names.rooms", "customparkbuildingpreset"] = "Custom Park";
                
                __result.Add(customParkGround);
                __result.Add(customParkRoof);
            }
        }

        [HarmonyPatch(typeof(AssetLoader), nameof(AssetLoader.GetAllBuildingPresets))]
        public class AssetLoader_GetAllBuildingPresets
        {
            public static void Postfix(ref Il2CppSystem.Collections.Generic.List<BuildingPreset> __result)
            {
                // __result.Add(Toolbox.Instance.resourcesCache[Il2CppType.Of<BuildingPreset>()]["CustomPark"].TryCast<BuildingPreset>());
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Start))]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                if(AssetLoader.instance.allBuildingData != null)
                {
                    BuildingLoaderPlugin.PluginLogger.LogWarning($"Toolbox ready, all building data loaded");
                }
                else
                {
                    BuildingLoaderPlugin.PluginLogger.LogError($"Toolbox ready, all building data NOT loaded");
                }
            }
        }
    }
}
