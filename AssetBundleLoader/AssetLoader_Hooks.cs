using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UniverseLib;

namespace AssetBundleLoader
{
    internal class AssetLoader_Hooks
    {
        // Before any other postfixes run, we need to make sure we have cached all scriptable objects
        [HarmonyPriority(Priority.VeryHigh)]
        [HarmonyPatch(typeof(AssetLoader), nameof(AssetLoader.GetAllPresets))]
        public class AssetLoader_GetAllPresets
        {
            private static bool hasLoaded = false;

            public static void Postfix()
            {
                // If it's the first load, do the mapping and load all mod items
                if (!hasLoaded)
                {
                    // We need to create a list of all ScriptableObject in the game, and the fileID associated
                    // We use this to rewrite the "REF:*" text references before Unity deserialises for us
                    var typeNameMapping = new List<(ScriptableObject so, string type, string name)>();
                    foreach (var uo in AssetLoader.Instance.allPresets)
                    {
                        ScriptableObject so = ((dynamic)uo).Cast<ScriptableObject>();
                        var soType = so.GetActualType().Name;
                        var soName = so.name;
                        typeNameMapping.Add(((ScriptableObject)so, soType, soName));
                    }

                    dynamic jobject = AssetBundleLoader.JsonLoader.NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(JsonUtilityArrays.ToJson(AssetLoader.Instance.allPresets.ToArray(), false));

                    var soIndex = 0;
                    foreach (var child in jobject["Items"])
                    {
                        child["name"] = typeNameMapping[soIndex].name;
                        child["type"] = typeNameMapping[soIndex].type;

                        string key = child["type"].ToString() + "|" + child["name"].ToString();

                        ScriptableObject value1 = typeNameMapping[soIndex].so;
                        string value2 = "{\"m_FileID\":" + child["m_FileID"].ToString() + ",\"m_PathID\":" + child["m_PathID"].ToString() + "}";

                        JsonLoader.ScriptableObjectIDMap[key] = (
                            value1,
                            value2
                        );

                        soIndex++;
                    }

                    // Some types need internal regeneration 

                    // Send a request to all mods to load their content
                    foreach (var dele in BundleLoader.loadObjectDelegates)
                    {
                        // Try catch to protect the game, otherwise loading broken content will break the entire game by leaving things unloaded
                        try
                        {
                            foreach (var so in dele(AssetLoader.Instance.allPresets))
                            {
                                AssetLoader.Instance.allPresets.Add(so);
                                // This handles most sublists, but not all?
                                AssetLoader.Instance.SortScriptableObject(so);
                            }
                        }
                        catch (Exception ex)
                        {
                            BundleLoader.PluginLogger.LogError($"Content loader threw an exception: {dele.Target.GetActualType().Name}");
                            BundleLoader.PluginLogger.LogError(ex);
                        }
                    }

                    // The side job controller caches a list, rebuild it
                    SideJobController.Instance.jobTracking.Clear();
                    SideJobController.Instance.Start();

                    BundleLoader.PluginLogger.LogInfo("Sidejobs recalculated");

                    hasLoaded = true;
                }
            }
        }
    }
}
