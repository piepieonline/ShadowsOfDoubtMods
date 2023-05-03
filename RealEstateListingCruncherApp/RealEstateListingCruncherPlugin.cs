using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using System.Linq;
using UnhollowerRuntimeLib;

namespace RealEstateListingCruncherApp
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class RealEstateListingCruncherPlugin : BasePlugin
    {
        public static ManualLogSource Logger;

        public override void Load()
        {
            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{PluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is patched!");

            ClassInjector.RegisterTypeInIl2Cpp<RealEstateCruncherAppContent>();
            ClassInjector.RegisterTypeInIl2Cpp<RealEstateCruncherAppContent.CruncherForSaleContent>();

            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} has added custom types!");
        }
    }
}
