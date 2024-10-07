using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UniverseLib;
using static AssetBundleLoader.JsonLoader;
using static AssetBundleLoader.JsonLoader.NewtonsoftExtensions;
using static lzma;

namespace DocumentationGenerator
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class DocumentationGenerator : BasePlugin
    {
        public static ManualLogSource Logger;
        public static bool IsFullExportEnabled = false;

        private static string DDS_GAME_PATH = @"E:\SteamLibrary\steamapps\common\Shadows of Doubt base\Shadows of Doubt_Data\StreamingAssets\DDS\";
        private static string MONO_ASSEMBLY_PATH = @"E:\SteamLibrary\steamapps\common\Shadows of Doubt mono\Shadows of Doubt_Data\Managed\Assembly-CSharp.dll";
        private static string SO_EXPORT_PATH = @"D:\Game Modding\ShadowsOfDoubt\Documentation\ExportedSOs\";
        private static string DOC_EXPORT_PATH = @"D:\Game Modding\ShadowsOfDoubt\PieMurderBuilder\scripts\ref\";
        private static string DDS_EDITOR_DOC_EXPORT_PATH = @"D:\Game Modding\ShadowsOfDoubt\SOD_DDS_Editor_Pie\scripts\ref\"; 

        public override void Load()
        {
            if (Config.Bind("General", "Enabled", false).Value)
            {
                IsFullExportEnabled = Config.Bind("General", "Full Export", false).Value;
                Logger = Log;

                // Plugin startup logic
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded.");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched.");
            }
        }

        [HarmonyPriority(Priority.VeryHigh)]
        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                Logger.LogWarning($"Documentation Regeneration Started");

                // Codegen specific stuff
                var codegenSOMap = new Dictionary<string, Dictionary<string, IEnumerable<string>>>();
                codegenSOMap["ScriptableObject"] = new Dictionary<string, IEnumerable<string>>();
                codegenSOMap["Enum"] = new Dictionary<string, IEnumerable<string>>();
                codegenSOMap["IDMap"] = new Dictionary<string, IEnumerable<string>>();
                var codegenTemplates = new Dictionary<string, dynamic>();
                var codegenSOTypeMapping = new Dictionary<string, Dictionary<string, (string Class, bool Array, string Tooltip)>>();

                if (DocumentationGenerator.IsFullExportEnabled)
                {
                    System.IO.Directory.Delete(SO_EXPORT_PATH, true);
                    System.IO.Directory.CreateDirectory(SO_EXPORT_PATH);
                }

                var typesToForceInclude = new string[] { "UnityEngine.Sprite", "UnityEngine.GameObject" };
                foreach (var uo in RuntimeHelper.FindObjectsOfTypeAll(typeof(UnityEngine.Object)))
                {
                    UnityEngine.Object so = ((dynamic)uo).Cast<UnityEngine.Object>();
                    var soType = so.GetActualType();

                    // Namespace null for things defined in Assembly-CSharp, but add exclusions for things we care about outside of that
                    if (soType.Namespace != null && !typesToForceInclude.Contains(soType.FullName)) continue;

                    Logger.LogWarning($"{so.name} is actual type {soType.Name} (Assignable: {soType.IsAssignableTo(typeof(ScriptableObject))})");

                    var soTypeName = soType.Name;
                    var soName = so.name;

                    var tempListForSerialisation = new Il2CppSystem.Collections.Generic.List<UnityEngine.Object>();
                    tempListForSerialisation.Add(so);
                    var id = NewtonsoftJson.JToken_Parse(JsonUtilityArrays.ToJson(tempListForSerialisation.ToArray())).SelectToken("Items[0].m_FileID").ToString();

                    // Skip custom objects that have been loaded, they get IDs below 0
                    if (int.Parse(id) < 0) continue;

                    if(soType.IsAssignableTo(typeof(ScriptableObject)))
                    {
                        // Type map
                        if (!codegenSOMap["ScriptableObject"].ContainsKey(soTypeName))
                        {
                            codegenSOMap["ScriptableObject"][soTypeName] = new SortedSet<string>();
                        }

                        if (DocumentationGenerator.IsFullExportEnabled && soName != "")
                        {
                            if (!System.IO.Directory.Exists(System.IO.Path.Combine(SO_EXPORT_PATH, soTypeName)))
                            {
                                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(SO_EXPORT_PATH, soTypeName));
                            }
                            System.IO.File.WriteAllText(System.IO.Path.Combine(SO_EXPORT_PATH, soTypeName, soName + ".json"), RestoredJsonUtility.ToJsonInternal(so, true));
                        }

                        if (soName != "")
                        {
                            // There are a massive number of duplicates - maybe due to addressables?
                            // Only add one copy to the SO list, but record both IDs (treating them as the same for now)
                            if (!codegenSOMap["ScriptableObject"][soTypeName].Contains(soName)) ((SortedSet<string>)codegenSOMap["ScriptableObject"][soTypeName]).Add(soName);
                        }
                    }

                    if (soName != "")
                    {
                        if(!codegenSOMap["IDMap"].ContainsKey(id)) codegenSOMap["IDMap"][id] = new List<string>();
                        codegenSOMap["IDMap"][id].Add(soTypeName + "|" + soName);
                    }
                }

                /*
                foreach(var rq in GameplayControls.Instance.murderResolveQuestions)
                {
                    char[] invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();

                    // Builds a string out of valid chars and an _ for invalid ones
                    var validFilename = new string(rq.name.Select(ch => invalidFileNameChars.Contains(ch) ? '_' : ch).ToArray());

                    System.IO.File.WriteAllText(System.IO.Path.Combine("D:\\temp\\sod\\", validFilename + ".json"), RestoredJsonUtility.ToJsonInternal(rq, true));
                }
                */

                // Load mono for tooltip info
                var assemblyLoadContext = new AssemblyLoadContext("MonoAssembly");
                assemblyLoadContext.LoadFromAssemblyPath(MONO_ASSEMBLY_PATH);
                var monoAssembly = assemblyLoadContext.Assemblies.Where(a => a.GetName().Name == "Assembly-CSharp").First();

                foreach (var type in typeof(MurderMO).Assembly.DefinedTypes)
                {
                    if (type.IsEnum)
                    {
                        codegenSOMap["Enum"][type.Name] = new List<string>();
                        foreach (var enumValue in type.GetEnumValues())
                        {
                            ((List<string>)codegenSOMap["Enum"][type.Name]).Add(enumValue.ToString());
                        }
                    }

                    if (type.IsSerializable || type.IsSubclassOf(typeof(ScriptableObject)))
                    {
                        codegenSOTypeMapping[type.Name] = new Dictionary<string, (string Class, bool Array, string Tooltip)>();

                        Type monoType = null;
                        try
                        {
                            monoType = monoAssembly.GetType(type.FullName);
                        }
                        catch { }

                        foreach (var property in type.DeclaredProperties)
                        {
                            string tooltipText = "";

                            var monoProperty = monoType?.GetField(property.Name);
                            if(monoProperty != null)
                            {
                                foreach (var attr in monoProperty.GetCustomAttributes(true))
                                {
                                    if(attr.GetType().Name == "TooltipAttribute")
                                    {
                                        tooltipText = attr.GetType().GetProperty("tooltip").GetValue(attr).ToString();
                                    }
                                }
                            }

                            if (property.PropertyType.IsGenericType)
                            {
                                codegenSOTypeMapping[type.Name][property.Name] = (property.PropertyType.GetGenericArguments()[0].Name, true, tooltipText);
                            }
                            else
                            {
                                codegenSOTypeMapping[type.Name][property.Name] = (property.PropertyType.Name, false, tooltipText);
                            }

                            if (property.PropertyType.IsEnum)
                            {
                                codegenSOMap["Enum"][property.Name] = new SortedSet<string>();
                                foreach (var enumValue in property.PropertyType.GetEnumValues())
                                {
                                    ((SortedSet<string>)codegenSOMap["Enum"][property.Name]).Add(enumValue.ToString());
                                }
                            }
                        }

                        try
                        {
                            string serializedObject;

                            if (type.IsSubclassOf(typeof(ScriptableObject)))
                            {
                                serializedObject = RestoredJsonUtility.ToJsonInternal(ScriptableObject.CreateInstance(type.Name), false);
                                serializedObject = Regex.Replace(serializedObject, """{"m_FileID":\s?(\d+),\s?"m_PathID":\s?(\d+)}""", m => "null");
                                codegenTemplates[type.Name] = NewtonsoftJson.JToken_Parse(serializedObject);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Type failed to map: {type.Name}");
                        }
                    }
                }

                // Map DDS content (So we tree each ID is in)
                var ddsMap = new Dictionary<string, dynamic>();
                ddsMap["ReverseIdMap"] = new Dictionary<string, List<string>>();
                ddsMap["IdNameMap"] = new Dictionary<string, string>();
                var reverseIdMap = ddsMap["ReverseIdMap"];
                var idNameMap = ddsMap["IdNameMap"];
                foreach (var directoryName in new string[] { "Trees", "Messages", "Blocks" })
                {
                    string folderPath = System.IO.Path.Combine(DDS_GAME_PATH, directoryName);
                    ddsMap[directoryName] = new List<string>();

                    foreach(var filePath in System.IO.Directory.EnumerateFiles(folderPath))
                    {
                        var id = filePath.Substring(filePath.LastIndexOf(System.IO.Path.DirectorySeparatorChar) + 1).Split(".")[0];
                        ddsMap[directoryName].Add(id);

                        var json = NewtonsoftJson.JToken_Parse(System.IO.File.ReadAllText(filePath));

                        if (json["name"] != null)
                        {
                            idNameMap[id] = json.Value<string>("name");
                        }

                        switch(directoryName)
                        {
                            case "Trees":
                                foreach(var message in json["messages"])
                                {
                                    if (message["msgID"] == null) continue;
                                    string msgId = message.Value<string>("msgID");
                                    if (msgId == "") continue;
                                    if (!reverseIdMap.ContainsKey(msgId)) reverseIdMap[msgId] = new List<string>();
                                    reverseIdMap[msgId].Add(id);
                                }
                                break;
                            case "Messages":
                                foreach (var message in json["blocks"])
                                {
                                    if (message["blockID"] == null) continue;
                                    string blockID = message.Value<string>("blockID");
                                    if (blockID == "") continue;
                                    if (!reverseIdMap.ContainsKey(blockID)) reverseIdMap[blockID] = new List<string>();
                                    reverseIdMap[blockID].Add(id);
                                }
                                break;
                            case "Blocks":
                                foreach (var message in json["replacements"])
                                {
                                    if (message["replaceWithID"] == null) continue;
                                    string replacementID = message.Value<string>("replaceWithID");
                                    if (replacementID == "") continue;
                                    if (!reverseIdMap.ContainsKey(replacementID)) reverseIdMap[replacementID] = new List<string>();
                                    reverseIdMap[replacementID].Add(id);
                                }
                                break;
                        }
                    }
                }

                // Export the DDS map to both case editor and the DDS editor
                System.IO.File.WriteAllText(System.IO.Path.Join(DOC_EXPORT_PATH, "ddsMap.json"), NewtonsoftJson.JObject_FromObject(ddsMap).ToString());
                System.IO.File.WriteAllText(System.IO.Path.Join(DDS_EDITOR_DOC_EXPORT_PATH, "ddsMap.json"), NewtonsoftJson.JObject_FromObject(ddsMap).ToString());

                System.IO.File.WriteAllText(System.IO.Path.Join(DOC_EXPORT_PATH, "soMap.json"), NewtonsoftJson.JObject_FromObject(codegenSOMap).ToString());
                System.IO.File.WriteAllText(System.IO.Path.Join(DOC_EXPORT_PATH, "templates.json"), NewtonsoftJson.JObject_FromObject(codegenTemplates).ToString());
                System.IO.File.WriteAllText(System.IO.Path.Join(DOC_EXPORT_PATH, "soChildTypes.json"), NewtonsoftJson.JObject_FromObject(codegenSOTypeMapping).ToString());

                Logger.LogWarning($"Documentation Regenerated. Turn off documentation generation and restart the game!");
            }
        }
    }
}
