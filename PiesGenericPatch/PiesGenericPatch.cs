using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using SOD.Common.BepInEx;
using Pies_Generic_Patch.BugFixes;
using Cpp2IL.Core.Api;

namespace Pies_Generic_Patch
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Pies_Generic_PatchPlugin : PluginController<Pies_Generic_PatchPlugin>
    {
        private static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> DebugLogging;
     
        private static ConfigEntry<bool> PatchEnabled_AddRetirees;
        private static ConfigEntry<bool> PatchEnabled_FixGroups;

        public override void Load()
        {
            // Plugin startup logic

            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required)");
            DebugLogging = Config.Bind("Debugging", "Logging", false, "Is debug logging enabled?");

            // PatchEnabled_AddRetirees = Config.Bind("Fixes", "Add Retirees", true);
            // PatchEnabled_FixGroups = Config.Bind("Fixes", "Fix Groups", true);
            
            if (Enabled.Value)
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");

                harmony.PatchAll();

                /*
                if(PatchEnabled_AddRetirees.Value)
                    AddRetirees.DoPatch(harmony);

                if(PatchEnabled_FixGroups.Value)
                    FixGroups.DoPatch(harmony);
                */

                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
            }
        }
    }
}
