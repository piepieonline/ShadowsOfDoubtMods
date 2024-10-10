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
    // ToString will just say Il2CppObject if we don't specifically call the type's ToString method
    public static class ToTypedStringExtension
    {
        public static string ToTypedString(this object toPrint)
        {
            var messageType = toPrint.GetActualType();
            var castedToPrint = toPrint.TryCast(messageType);
            return (string)messageType.GetMethod("ToString", System.Type.EmptyTypes).Invoke(castedToPrint, null);
        }
    }

    internal class Hooks_LoadContent
    {
        // Before any other postfixes run, we need to make sure we have cached all ScriptableObjects
        static bool haveLoadedCustomPresets = false;
        private static void LoadCustomPresets()
        {
            // Just in case, ensure we don't load twice
            if(!haveLoadedCustomPresets)
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

                haveLoadedCustomPresets = true;
            }
        }

        // We need to run after all ScriptableObjects have been loaded (so we can cache them), but before the game has setup it's own caches
        // We used to patch AssetLoader.GetAllPresets, but this broke in 39.10 without a mono branch to find an alternative
        // So hook the debug logging that has been stable since day 1 and pray...
        [HarmonyPatch]
        public class Debug_Log
        {
            [HarmonyTargetMethods]
            internal static IEnumerable<System.Reflection.MethodBase> CalculateMethods()
            {
                var mi = typeof(UnityEngine.Debug).GetMethods().Where(mi => mi.Name == "Log");
                return mi;
            }

            public static void Prefix(object message)
            {
                // Wait for the end of loading so everything else already exists
                if (message.ToTypedString().Contains("Finished load of group: data"))
                {
                    BundleLoader.PluginLogger.LogInfo($"Loading custom data");
                    LoadCustomPresets();
                    BundleLoader.PluginLogger.LogInfo($"Done loading custom data");
                }
            }
        }

        /*
        [HarmonyPriority(Priority.VeryHigh)]
        [HarmonyPatch(typeof(AssetLoader), nameof(AssetLoader.GetAllPresets))]
        public class AssetLoader_GetAllPresets
        {
            private static bool hasLoaded = false;

            public static void Postfix()
            {
                LoadCustomPresets();
            }
        }
        */
    }
}
