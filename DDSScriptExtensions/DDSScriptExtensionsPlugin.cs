using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using BepInEx.Configuration;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace DDSScriptExtensions
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class DDSScriptExtensionsPlugin : BaseUnityPlugin
#elif IL2CPP
    public class DDSScriptExtensionsPlugin : BasePlugin
#endif
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> DebugEnabled;

        public static ManualLogSource PluginLogger;

        public static Script LuaScriptEnvironment;
        public static Dictionary<string, Dictionary<string, Dictionary<string, DDSScript>>> LoadedExtensions = new Dictionary<string, Dictionary<string, Dictionary<string, DDSScript>>>();
#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif
            // Plugin startup logic
            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Restart required)");
            DebugEnabled = Config.Bind("General", "Debug Enabled", false, "If enabled, information about DDS Script execution is logged");

            if(Enabled.Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

                LuaScriptEnvironment = new Script();
                UserData.RegistrationPolicy = InteropRegistrationPolicy.Automatic;
            }
        }

        public static void ReloadScriptList()
        {
            LoadedExtensions.Clear();
            LoadedExtensions["scopes"] = new Dictionary<string, Dictionary<string, DDSScript>>();
            LoadedExtensions["values"] = new Dictionary<string, Dictionary<string, DDSScript>>();

            // Search for, and load, all `ddsscripts.sod.json` files found in the Bepinex plugins directory
            var modsToLoadFrom = Directory.GetDirectories(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), ".."), "*", SearchOption.AllDirectories)
                .Select(dirPath => new DirectoryInfo(dirPath))
                .Where(dir => File.Exists(Path.Combine(dir.FullName, "ddsscripts.sod.json")))
                .ToList();

            foreach (var mod in modsToLoadFrom)
            {
                var fileContent = AssetBundleLoader.JsonLoader.NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(mod.FullName, "ddsscripts.sod.json")));
                var modFolderName = mod.Parent.Parent.Name;

                LoadSubsection(fileContent, mod.Parent.Parent.Name, "scopes");
                LoadSubsection(fileContent, mod.Parent.Parent.Name, "values");

                DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"Loaded DDS Scripts for: {modFolderName}");
            }
        }

        private static void LoadSubsection(dynamic fileContent, string modFolderName, string subsection)
        {
            if(fileContent.ContainsKey(subsection))
            {
                foreach (var group in fileContent[subsection].Children())
                {
                    if (!LoadedExtensions[subsection].ContainsKey(group.Name))
                        LoadedExtensions[subsection][group.Name] = new Dictionary<string, DDSScript>();

                    foreach (var childValue in group.Value.Children())
                    {
                        var prefixedValueName = (subsection == "values" ? "custom_" : "customscope_") + childValue.Name;

                        if (LoadedExtensions[subsection][group.Name].ContainsKey(prefixedValueName))
                        {
                            DDSScriptExtensionsPlugin.PluginLogger.LogWarning($"Duplicate DDS script: {group.Name}.{childValue.Name}. Skipping script from {modFolderName}");
                        }
                        else
                        {
                            LoadedExtensions[subsection][group.Name][prefixedValueName] = new DDSScript()
                            {
                                script = childValue.Value["script"].Value.ToString()
                            };

                            if (childValue.Value["seed"] != null && childValue.Value["seed"].Value != "")
                            {
                                LoadedExtensions[subsection][group.Name][prefixedValueName].seedStatement = childValue.Value["seed"].Value;
                            }
                            
                            if (childValue.Value["scope"] != null && childValue.Value["scope"].Value != "")
                            {
                                LoadedExtensions[subsection][group.Name][prefixedValueName].scope = childValue.Value["scope"].Value;
                            }
                        }
                    }
                }
            }
        }
    }

    public class DDSScript
    {
        public string script = "";
        public string scope = "";
        public string seedStatement = "os.time()";
    }
}
