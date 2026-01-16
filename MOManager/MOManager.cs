using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using Il2CppInterop.Runtime;

using System.Text.RegularExpressions;
using LibCpp2IL.BinaryStructures;
using SOD.Common.Extensions;
using Il2CppType = Il2CppInterop.Runtime.Il2CppType;


#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace MOManager
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class MOManagerPlugin : BaseUnityPlugin
#elif IL2CPP
    public class MOManagerPlugin : BasePlugin
#endif
    {
        public static ConfigEntry<bool> Enabled;

        public static ManualLogSource PluginLogger;

        public static ConfigFile ConfigFile;

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

            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required after changing any settings at all)");

            if (Enabled.Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

                ConfigFile = Config;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Start))]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                foreach(var preset in Toolbox.Instance.GetFromResourceCache<MurderPreset>())
                {
                    // Disabled for hitman, the tutorial preset
                    if (string.IsNullOrEmpty(preset.presetName) || preset.presetName == "Hitman")
                        continue;

                    var presetConfigValue = ConfigFile.Bind("MurderPreset", $"Frequency {preset.presetName.Trim()}.", preset.frequency, $"The frequency for this Preset. This preset is added to the pool this many times, and then a preset is selected from the pool at random. Default is {preset.frequency}, -1 to disable outright.").Value;

                    if (presetConfigValue <= 0)
                    {
                        preset.disabled = true;
                    }
                    else
                    {
                        preset.frequency = presetConfigValue;
                    }
                }

                foreach(var mo in Toolbox.Instance.GetFromResourceCache<MurderMO>())
                {
                    // Disabled for hitman, the tutorial mo
                    if (string.IsNullOrEmpty(mo.presetName) || mo.presetName == "Hitman")
                        continue;

                    var moConfigValue = ConfigFile.Bind("MurderMO", $"Maximum base score for {mo.presetName.Trim()}.", mo.pickRandomScoreRange.y, $"The maximum base score for this MO. The higher, the more likely this MO is to be picked. Default is {mo.pickRandomScoreRange.y}, -1 to disable outright.").Value;

                    if(moConfigValue <= 0)
                    {
                        mo.disabled = true;
                    }
                    else
                    {
                        mo.pickRandomScoreRange = new Vector2(mo.pickRandomScoreRange.x, moConfigValue);
                    }
                }
            }
        }
    }
}
