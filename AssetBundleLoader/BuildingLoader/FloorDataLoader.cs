using System.Collections.Generic;
using System.IO;

using HarmonyLib;
using UnityEngine;

namespace AssetBundleLoader
{
    public static class FloorDataLoader
    {
        static Dictionary<string, TextAsset> floorplans = new Dictionary<string, TextAsset>();
        static Dictionary<string, FloorSaveData> floorData = new Dictionary<string, FloorSaveData>();
        static Dictionary<string, string> floorplanSources = new Dictionary<string, string>();

        public static TextAsset LoadFloorplan(string modFolderPath, string relativePath)
        {
            string floorName = Path.GetFileNameWithoutExtension(relativePath);
            string filePath = Path.Combine(modFolderPath, relativePath + ".json");

            if (floorplans.ContainsKey(floorName))
            {
                if (floorplanSources[floorName] != filePath)
                {
                    BundleLoader.PluginLogger.LogError($"Floorplan '{floorName}' is already loaded from {floorplanSources[floorName]}, ignoring {filePath}. Floorplan names are shared by every mod, so give yours a unique prefix.");
                }

                return floorplans[floorName];
            }

            string contents = ReadFile(filePath);
            if (contents == null)
            {
                BundleLoader.PluginLogger.LogError($"Floorplan '{floorName}' failed to load, {filePath} doesn't exist!");
                return null;
            }

            var floorplan = new TextAsset(contents);
            floorplan.name = floorName;
            floorplan.hideFlags = HideFlags.DontUnloadUnusedAsset;

            floorplans[floorName] = floorplan;
            floorData[floorName] = JsonUtility.FromJson<FloorSaveData>(contents);
            floorplanSources[floorName] = filePath;

            return floorplan;
        }

        // The game runs under Wine on Linux, so mod paths are Windows paths either way, and \\?\ bypasses MAX_PATH
        static string ReadFile(string filePath)
        {
            if (File.Exists("\\\\?\\" + filePath)) return File.ReadAllText("\\\\?\\" + filePath);
            if (File.Exists(filePath)) return File.ReadAllText(filePath);
            return null;
        }

        // A stair flight is only ever joined to the room it sits in from the adjoining node's side, and only if that
        // tile's stairwellLowerLink is already set - which happens the first time one of the tile's own nodes comes up
        // in the same loop. A blueprint that lists the node you walk in from before the stairwell tile's nodes loses
        // that link for good, leaving a flight no NPC can path into. Wiring the room's stairwells up front makes the
        // order the nodes were saved in irrelevant.
        // Bugfix for a vanilla issue
        [HarmonyPatch(typeof(NewRoom), nameof(NewRoom.ConnectNodes))]
        public class NewRoom_ConnectNodes
        {
            public static void Prefix(NewRoom __instance)
            {
                // Floor edit wipes every node's access as ConnectNodes starts, so anything wired here would be thrown away
                if (SessionData.Instance.isFloorEdit) return;

                foreach (var node in __instance.nodes)
                {
                    if (node.tile.isStairwell && !node.isConnected)
                    {
                        node.tile.ConnectStairwell();
                    }
                }
            }
        }

        // The game builds this dictionary from its own floor_data asset group, and rebuilds it whenever floors are edited
        [HarmonyPatch(typeof(CityData), nameof(CityData.ParseFloorData))]
        public class CityData_ParseFloorData
        {
            public static void Postfix()
            {
                foreach (var floor in floorData)
                {
                    CityData.Instance.floorData[floor.Key] = floor.Value;
                }

                if (floorData.Count > 0)
                {
                    BundleLoader.PluginLogger.LogInfo($"Added {floorData.Count} custom floorplan(s) to the city's floor data");
                }
            }
        }
    }
}
