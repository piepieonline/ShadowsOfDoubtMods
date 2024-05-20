using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using HarmonyLib;
using System.Linq;
using System.IO;
using System.Collections.Generic;


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

        public static string DEBUG_LoadSpecificMurder;

        public static bool DEBUG_ShowDebugMessages;

        public static bool CreateMapping = false;

        public static CharacterTrait homelessTrait;

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            DEBUG_LoadSpecificMurder = Config.Bind("Debug", "Force specific MurderMO", "").Value;
            DEBUG_ShowDebugMessages = Config.Bind("Debug", "Show murder debug messages", false).Value;

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }
    }

    [HarmonyPatch(typeof(Toolbox), "Start")]
    public class Toolbox_Start
    {
        static bool hasToolboxInit = false;

        public static void Postfix()
        {
            if (!hasToolboxInit)
            {
                // Search the plugins directory to find any and all murdermanifests, so that people can upload thunderstore mods that purely contain json without code
                var modsToLoadFrom = Directory.GetDirectories(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), ".."), "*", SearchOption.AllDirectories)
                    .Select(dirPath => new DirectoryInfo(dirPath))
                    .Where(dir => File.Exists(Path.Combine(dir.FullName, "murdermanifest.sodso.json")))
                    .ToList();

                foreach (var mod in modsToLoadFrom)
                {
                    LoadManifest(mod.Name, mod.FullName);
                }

                // Force single type for testing
                if (CommunityCaseLoaderPlugin.DEBUG_LoadSpecificMurder != "")
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Forcing MurderMO: {CommunityCaseLoaderPlugin.DEBUG_LoadSpecificMurder}");
                    for (int i = Toolbox.Instance.allMurderMOs.Count - 1; i >= 0; i--)
                    {
                        if (Toolbox.Instance.allMurderMOs[i].name != CommunityCaseLoaderPlugin.DEBUG_LoadSpecificMurder)
                            Toolbox.Instance.allMurderMOs[i].disabled = true; // TODO: Cache the current state to allow changing at runtime
                    }
                }

                // TODO: Incomplete, not added to citizens atm
                CommunityCaseLoaderPlugin.homelessTrait = ScriptableObject.CreateInstance<CharacterTrait>();
                CommunityCaseLoaderPlugin.homelessTrait.name = "Quirk-Homeless";
                CommunityCaseLoaderPlugin.homelessTrait.isTrait = true;

                Toolbox.Instance.allCharacterTraits.Add(CommunityCaseLoaderPlugin.homelessTrait);

                hasToolboxInit = true;
            }
        }

        private static void LoadManifest(string moName, string folderPath)
        {
            var manifest = AssetBundleLoader.JsonLoader.NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(folderPath, "murdermanifest.sodso.json")));

            if (manifest.Value<bool>("enabled"))
            {
                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Loading MurderMO: {moName}");
            }
            else
            {
                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Not Loading MurderMO: {moName} (Disabled)");
                return;
            }

            foreach(var file in manifest["fileOrder"])
            {
                var filePath = Path.Combine(folderPath, file.ToString().Replace("REF:", "") + ".sodso.json");
                if(File.Exists(filePath))
                {
                    AssetBundleLoader.JsonLoader.LoadFileToGame(File.ReadAllText(filePath));
                }
                else
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogError($"Failed to load file: {file} (File not found)");
                }
            }
        }
    }



    /*
    [HarmonyPatch(typeof(MurderController), "SpawnItemIsValid")]
    public class MurderController_SpawnItemIsValid
    {
        public static void Postfix(bool __result, MurderPreset.MurderLeadItem spawn)
        {
            if(CommunityCaseLoaderPlugin.DEBUG_ShowDebugMessages)
            {
                if (!__result)
                {
                    CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Not spawning murder lead item: {spawn.name}");
                }
            }
        }
    }
    */
    
    [HarmonyPatch(typeof(MurderController), "PickNewVictim")]
    public class MurderController_PickNewVictim
    {
        public static void Postfix()
        {
            if(CommunityCaseLoaderPlugin.DEBUG_ShowDebugMessages)
            {
                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Murderer: {MurderController.Instance.currentMurderer.name} (Works: {MurderController.Instance.currentMurderer.job.employer.name})");
                CommunityCaseLoaderPlugin.PluginLogger.LogInfo($"Victim: {MurderController.Instance.currentVictim.name} (Works: {MurderController.Instance.currentVictim.job.employer.name})");
            }
        }
    }
}