using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using UnityEngine;
using UniverseLib;
using static AssetBundleLoader.JsonLoader;
using static AssetBundleLoader.JsonLoader.NewtonsoftExtensions;

namespace DocumentationGenerator
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class DocumentationGenerator : BasePlugin
    {
        public static ManualLogSource Logger;
        public static bool IsFullExportEnabled = false;

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
                // Codegen specific stuff
                var codegenSOMap = new Dictionary<string, Dictionary<string, IEnumerable<string>>>();
                codegenSOMap["ScriptableObject"] = new Dictionary<string, IEnumerable<string>>();
                codegenSOMap["Enum"] = new Dictionary<string, IEnumerable<string>>();
                var codegenTemplates = new Dictionary<string, dynamic>();
                var codegenSOTypeMapping = new Dictionary<string, Dictionary<string, (string Class, bool Array, string Tooltip)>>();

                var soExportBasePath = "D:\\Game Modding\\ShadowsOfDoubt\\Documentation\\ExportedSOs\\";
                if (DocumentationGenerator.IsFullExportEnabled)
                {
                    System.IO.Directory.Delete(soExportBasePath, true);
                    System.IO.Directory.CreateDirectory(soExportBasePath);
                }

                foreach (var uo in RuntimeHelper.FindObjectsOfTypeAll(typeof(ScriptableObject)))
                {
                    ScriptableObject so = ((dynamic)uo).Cast<ScriptableObject>();
                    var soType = so.GetActualType();

                    if (soType.Namespace != null) continue;

                    var soTypeName = soType.Name;
                    var soName = so.name;

                    // Type map
                    if (!codegenSOMap["ScriptableObject"].ContainsKey(soTypeName)) codegenSOMap["ScriptableObject"][soTypeName] = new SortedSet<string>();

                    if (soName != "" && !codegenSOMap["ScriptableObject"][soTypeName].Contains(soName))
                        ((SortedSet<string>)codegenSOMap["ScriptableObject"][soTypeName]).Add(soName);

                    if(DocumentationGenerator.IsFullExportEnabled && soName != "")
                    {
                        if(!System.IO.Directory.Exists(System.IO.Path.Combine(soExportBasePath, soTypeName)))
                        {
                            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(soExportBasePath, soTypeName));
                        }
                        System.IO.File.WriteAllText(System.IO.Path.Combine(soExportBasePath, soTypeName, soName + ".json"), RestoredJsonUtility.ToJsonInternal(so, true));
                    }
                }

                // Load mono for tooltip info
                var assemblyLoadContext = new AssemblyLoadContext("MonoAssembly");
                assemblyLoadContext.LoadFromAssemblyPath(@"E:\SteamLibrary\steamapps\common\Shadows of Doubt mono\Shadows of Doubt_Data\Managed\Assembly-CSharp.dll");
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
                                codegenTemplates[type.Name] = NewtonsoftJson.JToken_Parse(serializedObject);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"Type failed to map: {type.Name}");
                        }
                    }
                }

                System.IO.File.WriteAllText("D:\\Game Modding\\ShadowsOfDoubt\\PieMurderBuilder\\scripts\\ref\\soMap.json", NewtonsoftJson.JObject_FromObject(codegenSOMap).ToString());
                System.IO.File.WriteAllText("D:\\Game Modding\\ShadowsOfDoubt\\PieMurderBuilder\\scripts\\ref\\templates.json", NewtonsoftJson.JObject_FromObject(codegenTemplates).ToString());
                System.IO.File.WriteAllText("D:\\Game Modding\\ShadowsOfDoubt\\PieMurderBuilder\\scripts\\ref\\soChildTypes.json", NewtonsoftJson.JObject_FromObject(codegenSOTypeMapping).ToString());

                Logger.LogWarning($"Documentation Regenerated");
            }
        }
    }
}
