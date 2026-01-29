using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace DialogUIRework
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class DialogUIReworkPlugin : BasePlugin
    {
        public static ManualLogSource PluginLogger;
        
        internal static TabbedDialogUI TabbedDialogUI;
        
        private static ConfigEntry<bool> _enabled;

        public override void Load()
        {
            PluginLogger = Log;
            _enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required)");

            if (_enabled.Value)
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();

                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
            }
        }
    }
}