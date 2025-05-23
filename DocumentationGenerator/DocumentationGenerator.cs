using System;
using System.Linq;
using System.Runtime.Loader;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

using HarmonyLib;
using UniverseLib;
using static AssetBundleLoader.JsonLoader;

namespace DocumentationGenerator
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class DocumentationGenerator : BasePlugin
    {
        public static ManualLogSource Logger;
        public static bool IsFullExportEnabled = false;

        private static string DDS_GAME_PATH = @"E:\Program Files (x86)\Steam\steamapps\common\Shadows of Doubt\Shadows of Doubt_Data\StreamingAssets\DDS\";
        private static string MONO_ASSEMBLY_PATH = @"F:\SteamLibrary\steamapps\common\shadows of doubt mono\Shadows of Doubt_Data\Managed\Assembly-CSharp.dll";
        private static string DOC_EXPORT_PATH = @"D:\Game Modding\ShadowsOfDoubt\Documentation\";
        private static string MEDIAWIKI_DOC_EXPORT_PATH = @"D:\Game Modding\ShadowsOfDoubt\Documentation\MediaWikiExports\";
        private static string SO_EXPORT_PATH = @"D:\Game Modding\ShadowsOfDoubt\Documentation\ExportedSOs\";
        private static string TEXTASSET_EXPORT_PATH = @"D:\Game Modding\ShadowsOfDoubt\Documentation\ExportedTextAssets\";
        private static string MURDER_BUILDER_DOC_EXPORT_PATH = @"D:\Game Modding\ShadowsOfDoubt\PieMurderBuilder\scripts\ref\";
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
                var codegenSOMap = new SOMapping();
                var codegenIDMap = new Dictionary<string, IEnumerable<string>>();
                var codegenTemplates = new Dictionary<string, dynamic>();
                var codegenSOTypeMapping = new Dictionary<string, Dictionary<string, (string Class, bool Array, string Tooltip)>>();

                if (DocumentationGenerator.IsFullExportEnabled)
                {
                    System.IO.Directory.Delete(SO_EXPORT_PATH, true);
                    System.IO.Directory.CreateDirectory(SO_EXPORT_PATH);
                }

                var typesToForceInclude = new string[] { "UnityEngine.Sprite", "UnityEngine.GameObject", "UnityEngine.TextAsset" };
                foreach (var uo in RuntimeHelper.FindObjectsOfTypeAll(typeof(UnityEngine.Object)))
                {
                    UnityEngine.Object so = ((dynamic)uo).Cast<UnityEngine.Object>();
                    var soType = so.GetActualType();

                    // Namespace null for things defined in Assembly-CSharp, but add exclusions for things we care about outside of that
                    if (soType.Namespace != null && !typesToForceInclude.Contains(soType.FullName)) continue;

                    // Logger.LogWarning($"{so.name} is actual type {soType.Name} (Assignable: {soType.IsAssignableTo(typeof(ScriptableObject))})");

                    var soTypeName = soType.Name;
                    var soName = so.name;

                    var tempListForSerialisation = new Il2CppSystem.Collections.Generic.List<UnityEngine.Object>();
                    tempListForSerialisation.Add(so);
                    var id = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(JsonUtilityArrays.ToJson(tempListForSerialisation.ToArray())).SelectToken("Items[0].m_FileID").ToString();

                    // Skip custom objects that have been loaded, they get IDs below 0
                    if (int.Parse(id) < 0) continue;

                    if(soType.IsAssignableTo(typeof(ScriptableObject)))
                    {
                        // Type map
                        if (!codegenSOMap.ScriptableObject.ContainsKey(soTypeName))
                        {
                            codegenSOMap.ScriptableObject[soTypeName] = new SortedSet<string>();
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
                            if (!codegenSOMap.ScriptableObject[soTypeName].Contains(soName)) ((SortedSet<string>)codegenSOMap.ScriptableObject[soTypeName]).Add(soName);
                        }
                    }

                    if(soType.IsAssignableTo(typeof(ScriptableObjectIDSystem)))
                    {
                        // Type map
                        if (!codegenSOMap.ScriptableObjectID.ContainsKey(soTypeName))
                        {
                            codegenSOMap.ScriptableObjectID[soTypeName] = new SortedDictionary<string, string>();
                        }

                        if (soName != "")
                        {
                            ScriptableObjectIDSystem scriptableObjectID = so.Cast<ScriptableObjectIDSystem>();
                            if (!codegenSOMap.ScriptableObjectID[soTypeName].ContainsKey(scriptableObjectID.id)) codegenSOMap.ScriptableObjectID[soTypeName].Add(scriptableObjectID.id, soName);
                        }
                    }

                    if (soName != "")
                    {
                        if(!codegenIDMap.ContainsKey(id)) codegenIDMap[id] = new List<string>();
                        codegenIDMap[id].Add(soTypeName + "|" + soName);
                    }
                }

                // Export trait list to the wiki table format
                Generator_Traits.GenerateTraitsWiki(MEDIAWIKI_DOC_EXPORT_PATH, codegenSOMap.ScriptableObject["CharacterTrait"]);

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
                        codegenSOMap.Enum[type.Name] = new List<string>();
                        foreach (var enumValue in type.GetEnumValues())
                        {
                            ((List<string>)codegenSOMap.Enum[type.Name]).Add(enumValue.ToString());
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
                                codegenSOMap.Enum[property.Name] = new SortedSet<string>();
                                foreach (var enumValue in property.PropertyType.GetEnumValues())
                                {
                                    ((SortedSet<string>)codegenSOMap.Enum[property.Name]).Add(enumValue.ToString());
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
                                codegenTemplates[type.Name] = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(serializedObject);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Type failed to map: {type.Name}");
                        }
                    }
                }

                // Map DDS content (So we tree each ID is in)
                var ddsMap = Generator_DDS.GenerateLookup(DDS_GAME_PATH);
                Generator_DDS.GenerateScopes(DOC_EXPORT_PATH, MEDIAWIKI_DOC_EXPORT_PATH);

                // Export all text assets if we are doing a full export
                if (IsFullExportEnabled)
                {
                    foreach(var textAsset in RuntimeHelper.FindObjectsOfTypeAll<TextAsset>())
                    {
                        if(textAsset == null || textAsset.dataSize == 0) continue;

                        System.IO.File.WriteAllText(System.IO.Path.Combine(TEXTASSET_EXPORT_PATH, textAsset.name + ".txt"), textAsset.text);
                    }
                }

                // Export the DDS map to both case editor and the DDS editor
                System.IO.File.WriteAllText(System.IO.Path.Join(MURDER_BUILDER_DOC_EXPORT_PATH, "ddsMap.json"), NewtonsoftExtensions.NewtonsoftJson.JObject_FromObject(ddsMap).ToString());
                System.IO.File.WriteAllText(System.IO.Path.Join(DDS_EDITOR_DOC_EXPORT_PATH, "ddsMap.json"), NewtonsoftExtensions.NewtonsoftJson.JObject_FromObject(ddsMap).ToString());

                System.IO.File.WriteAllText(System.IO.Path.Join(MURDER_BUILDER_DOC_EXPORT_PATH, "soMap.json"), NewtonsoftExtensions.NewtonsoftJson.JObject_FromObject(codegenSOMap).ToString());
                System.IO.File.WriteAllText(System.IO.Path.Join(MURDER_BUILDER_DOC_EXPORT_PATH, "soIdMap.json"), NewtonsoftExtensions.NewtonsoftJson.JObject_FromObject(codegenIDMap).ToString());
                System.IO.File.WriteAllText(System.IO.Path.Join(MURDER_BUILDER_DOC_EXPORT_PATH, "templates.json"), NewtonsoftExtensions.NewtonsoftJson.JObject_FromObject(codegenTemplates).ToString());
                System.IO.File.WriteAllText(System.IO.Path.Join(MURDER_BUILDER_DOC_EXPORT_PATH, "soChildTypes.json"), NewtonsoftExtensions.NewtonsoftJson.JObject_FromObject(codegenSOTypeMapping).ToString());

                Logger.LogWarning($"Documentation Regenerated. Turn off documentation generation and restart the game!");
            }
        }

        class SOMapping
        {
            public Dictionary<string, IEnumerable<string>> ScriptableObject = new Dictionary<string, IEnumerable<string>>();
            public Dictionary<string, SortedDictionary<string, string>> ScriptableObjectID = new Dictionary<string, SortedDictionary<string, string>>();
            public Dictionary<string, IEnumerable<string>> Enum = new Dictionary<string, IEnumerable<string>>();
        }
    }
}
