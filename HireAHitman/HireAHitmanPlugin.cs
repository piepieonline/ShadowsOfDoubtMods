using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using HarmonyLib;
using BepInEx.Configuration;
using DDSScriptExtensions;
using SOD.Common;
using System.Text.Json;
using Cpp2IL.Core.Api;





#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace HireAHitman
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class HireAHitmanPlugin : BaseUnityPlugin
#elif IL2CPP
    public class HireAHitmanPlugin : BasePlugin
#endif
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<int> CostToHire;
        public static ConfigEntry<bool> PublicPhoneNumber;
        public static ConfigEntry<bool> EnableHitmanInNormalGameplay;
        public static ConfigEntry<bool> UseEasyCaseType;
        public static ConfigEntry<bool> DebugMode;

        private static HireAHitmanPlugin instance;

        public static ManualLogSource PluginLogger;

        private static HireAHitmanCustomDDSData hireAHitmanCustomDDSData;
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

            instance = this;

            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required)");
            CostToHire = Config.Bind("General", "CostToHire", 1000, "The cost to hire a hitman");
            PublicPhoneNumber = Config.Bind("General", "PublicPhoneNumber", false, "Show phone number in the phone known numbers section? (Once added, it can't be removed from that save)");
            EnableHitmanInNormalGameplay = Config.Bind("General", "EnableHitmanInNormalGameplay", false, "Should the Hitman case be used in the normal rotation? (Game restart required)");
            UseEasyCaseType = Config.Bind("General", "UseEasyCase", false, "Should the easy version of the case be used for a quick solve?");
            DebugMode = Config.Bind("Debug", "Enabled", false, "Is debug logging enabled?");

            if (Enabled.Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

                // Setup custom data for DDSScriptExtensions
                hireAHitmanCustomDDSData = new HireAHitmanCustomDDSData();
                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["HireAHitmanData"] = MoonSharp.Interpreter.UserData.Create(HireAHitmanCustomDDSData.Instance);

                // Saving and loading
                SOD.Common.Lib.SaveGame.OnBeforeSave += SaveGame_OnBeforeSave;
                SOD.Common.Lib.SaveGame.OnBeforeLoad += SaveGame_OnBeforeLoad;
                SOD.Common.Lib.SaveGame.OnAfterLoad += SaveGame_OnAfterLoad;
                SOD.Common.Lib.Gameplay.OnVictimKilled += HireAHitmanHooks.VMailBuildingSecurity;
            }
        }

        private void SaveGame_OnBeforeLoad(object sender, SOD.Common.Helpers.SaveGameArgs e)
        {
            try
            {
                var savePath = GetSavePath();
                if (System.IO.File.Exists(savePath))
                {
                    var jtokenTargetIds = AssetBundleLoader.JsonLoader.NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(System.IO.File.ReadAllText(savePath));
                    HireAHitmanHooks.TargetHumanIds = jtokenTargetIds.ToObject<System.Collections.Generic.List<int>>();
                }
            }
            catch (System.Exception exception)
            {
                PluginLogger.LogError($"Failed to save hired hitman targets: {exception.Message}");
            }
        }

        private void SaveGame_OnAfterLoad(object sender, SOD.Common.Helpers.SaveGameArgs e)
        {
            // For some reason, loading a city fires this too early. Try again after loading
            if (PublicPhoneNumber.Value)
            {
                GameplayController.Instance.AddOrMergePhoneNumberData(HireAHitmanHooks.FakePhoneNumber, false, null, "hireahitman", false);
            }
        }

        private void SaveGame_OnBeforeSave(object sender, SOD.Common.Helpers.SaveGameArgs e)
        {
            try
            {
                var savePath = GetSavePath();
                JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };
                System.IO.File.WriteAllText(savePath, JsonSerializer.Serialize(HireAHitmanHooks.TargetHumanIds, options));
                PluginLogger.LogInfo($"Successfully saved hired hitman targets at {savePath}!");
            }
            catch (System.Exception exception)
            {
                PluginLogger.LogError($"Failed to save hired hitman targets: {exception.Message}");
            }
        }

        private string GetSavePath()
        {
            var savecode = Lib.SaveGame.GetUniqueString("HitmanTargetIds");
            var fileName = $"HitmanTargetList_{savecode}.json";
            return Lib.SaveGame.GetSavestoreDirectoryPath(System.Reflection.Assembly.GetExecutingAssembly(), fileName);
        }

        public static void DebugLogIfEnabled(LogLevel logLevel, string value)
        {
            if (DebugMode.Value)
            {
                switch(logLevel)
                {
                    case LogLevel.Info:
                        PluginLogger.LogInfo(value);
                        break;
                    case LogLevel.Warning:
                        PluginLogger.LogWarning(value);
                        break;
                    case LogLevel.Error:
                        PluginLogger.LogError(value);
                        break;
                }
            }
        }
    }

    public class HireAHitmanCustomDDSData
    {
        public static HireAHitmanCustomDDSData Instance;

        public Human Target;

        public HireAHitmanCustomDDSData()
        {
            Instance = this;
        }

        public string GetCostToHire()
        {
            return HireAHitmanPlugin.CostToHire.Value.ToString();
        }
    }
}
