using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;

using System.Text.RegularExpressions;
using DDSScriptExtensions;
using SOD.Common.Helpers;
using SOD.Common;
using System.Text.Json;






#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace AdditionalEvidence
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class AdditionalEvidencePlugin : BaseUnityPlugin
#elif IL2CPP
    public class AdditionalEvidencePlugin : BasePlugin
#endif
    {
        public static ConfigEntry<bool> Enabled;

        public static ConfigEntry<bool> GunForensics_DebugLogging;
        public static ConfigEntry<bool> GunForensics_ScanGunForHeadPrint;
        public static ConfigEntry<float> GunForensics_ChanceToDropGunsAtScene;

        public static ManualLogSource PluginLogger;

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

            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required)");

            GunForensics_ScanGunForHeadPrint = Config.Bind("GunForensics", "ScanGunForHeadPrint", false, "Should scanning the gun reveal the head print? (Otherwise a lab is required)");
            GunForensics_ChanceToDropGunsAtScene = Config.Bind("GunForensics", "GunDropChance", 0.25f, "What is the chances of the purp dropping the gun at or nearby the scene? (0-1, 0 is vanilla)"); // TODO: Default value?
            GunForensics_DebugLogging = Config.Bind("GunForensics", "Debug", false, "Print debug information");
            
            if (Enabled.Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

                GunSerialNumbers.Init();

                SOD.Common.Lib.SaveGame.OnAfterLoad += SaveGame_OnAfterLoad;
                SOD.Common.Lib.SaveGame.OnBeforeSave += SaveGame_OnBeforeSave;
            }
        }

        private void SaveGame_OnAfterLoad(object sender, SOD.Common.Helpers.SaveGameArgs e)
        {
            try
            {
                var savePath = GetSavePath(e.FilePath);
                if (System.IO.File.Exists(savePath))
                {
                    var jtokenTargetIds = AssetBundleLoader.JsonLoader.NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(System.IO.File.ReadAllText(savePath));
                    
                    GunSerialNumbers.Load(jtokenTargetIds["GunSerialNumbers"]);

                    PluginLogger.LogInfo($"Loaded AdditionalEvidence data from file");
                }
            }
            catch (System.Exception exception)
            {
                PluginLogger.LogError($"Failed to load AdditionalEvidence data: {exception.Message}");
            }
        }

        private void SaveGame_OnBeforeSave(object sender, SOD.Common.Helpers.SaveGameArgs e)
        {
            try
            {
                var savePath = GetSavePath(e.FilePath);
                
                var saveData = new Dictionary<string, object>();
                saveData["GunSerialNumbers"] = GunSerialNumbers.Save();

                var jsonSaveData = AssetBundleLoader.JsonLoader.NewtonsoftExtensions.NewtonsoftJson.JObject_FromObject(saveData);

                System.IO.File.WriteAllText(savePath, jsonSaveData.ToString());
                PluginLogger.LogInfo($"Saved AdditionalEvidence data to file");
            }
            catch (System.Exception exception)
            {
                PluginLogger.LogError($"Failed to save AdditionalEvidence data: {exception.Message}");
            }
        }

        private string GetSavePath(string filePath)
        {
            var savecode = Lib.SaveGame.GetUniqueString(filePath);
            var fileName = $"AdditionalEvidence_{savecode}.json";
            return Lib.SaveGame.GetSavestoreDirectoryPath(System.Reflection.Assembly.GetExecutingAssembly(), fileName);
        }
    }
}
