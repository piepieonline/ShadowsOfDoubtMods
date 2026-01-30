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
        internal static ConfigEntry<bool> _enableWASDNavigation;
        internal static ConfigEntry<bool> _enableAllTab;
        internal static ConfigEntry<bool> _enableControlGlyphs;
        
        private static ConfigEntry<bool> _enabled;
        

        public override void Load()
        {
            PluginLogger = Log;
            _enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required)");
            _enableWASDNavigation = Config.Bind("General", "Enable WASD dialog navigation", true, "Enable WASD navigation (W and S to select dialog, A and D to change tab)");
            _enableAllTab = Config.Bind("General", "Enable All tab", true, "");
            _enableControlGlyphs = Config.Bind("General", "Enable control glyphs", true, "");

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