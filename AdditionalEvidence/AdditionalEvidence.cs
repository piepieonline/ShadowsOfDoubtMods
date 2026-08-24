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

        public static ConfigEntry<bool> GroupFlyers_Enabled;
        public static ConfigEntry<float> GroupFlyers_ChancePerNoticeBoard;
        public static ConfigEntry<float> GroupFlyers_ChancePerSurface;
        public static ConfigEntry<int> GroupFlyers_MaxExtraFlyersPerGroup;
        public static ConfigEntry<bool> GroupFlyers_DebugLogging;

        public static ConfigEntry<bool> BinPasscodes_Enabled;
        public static ConfigEntry<float> BinPasscodes_ChancePerCitizen;
        public static ConfigEntry<float> BinPasscodes_CompanyChancePerOffice;
        public static ConfigEntry<float> BinPasscodes_DistractorChancePerBin;
        public static ConfigEntry<bool> BinPasscodes_DebugLogging;

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

            GroupFlyers_Enabled = Config.Bind("GroupFlyers", "Enabled", true, "Post club meet-up flyers on the noticeboards of public eateries beyond the club's own meeting place (Game restart required)");
            GroupFlyers_ChancePerNoticeBoard = Config.Bind("GroupFlyers", "ChancePerNoticeBoard", 0.35f, "Chance that any given eatery noticeboard gets a flyer (0-1)");
            GroupFlyers_ChancePerSurface = Config.Bind("GroupFlyers", "ChancePerSurface", 0.2f, "Chance that any given public venue gets a flyer left out on a table or counter (0-1)");
            GroupFlyers_MaxExtraFlyersPerGroup = Config.Bind("GroupFlyers", "MaxExtraFlyersPerGroup", 2, "How many of these extra flyers a single club can have across the city");
            GroupFlyers_DebugLogging = Config.Bind("GroupFlyers", "Debug", false, "Print debug information");

            BinPasscodes_Enabled = Config.Bind("BinPasscodes", "Enabled", true, "Citizens who are careless with their passcode throw the reminder note in the bin, where searching it reveals their door code (Game restart required)");
            BinPasscodes_ChancePerCitizen = Config.Bind("BinPasscodes", "ChancePerCitizen", 0.35f, "Chance that such a citizen actually leaves the note in a bin at their home or workplace (0-1)");
            BinPasscodes_CompanyChancePerOffice = Config.Bind("BinPasscodes", "CompanyChancePerOffice", 0.45f, "Chance that a company with a door code has an employee's discarded memo of it in a bin on the premises (0-1)");
            BinPasscodes_DebugLogging = Config.Bind("BinPasscodes", "Debug", false, "Print debug information");
            BinPasscodes_DistractorChancePerBin = Config.Bind("BinPasscodes", "DistractorChancePerBin", 0.8f, "Chance that any given bin in the city gets junk (0-1)");
            
            if (Enabled.Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

                GunSerialNumbers.Init();

                SOD.Common.Lib.SaveGame.OnAfterNewGame += BinDistractors.OnAfterNewGame;
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
