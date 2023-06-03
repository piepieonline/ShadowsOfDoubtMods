using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace RealEstateListingCruncherApp
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class RealEstateListingCruncherPlugin : BasePlugin
    {
        public static ManualLogSource Logger;

        public override void Load()
        {
            if (!Config.Bind("General", "Enabled", true).Value)
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is disabled.");
                return;
            }

            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

            ClassInjector.RegisterTypeInIl2Cpp<RealEstateCruncherAppContent>();
            ClassInjector.RegisterTypeInIl2Cpp<RealEstateCruncherAppContent.CruncherForSaleContent>();

            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} has added custom types!");
        }
    }
}
