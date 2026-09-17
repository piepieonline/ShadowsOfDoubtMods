using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2CppInterop.Runtime;

using System.Text.RegularExpressions;
using LibCpp2IL.BinaryStructures;
using SOD.Common.Extensions;
using UniverseLib;
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

            ConfigFile = Config;

            // Any save before the preset entries are bound writes them without their default value comments, which the pre-bind needs
            Config.SaveOnConfigSet = false;
            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required after changing any settings at all)");
            PreBindSavedEntries();
            Config.SaveOnConfigSet = true;
            Config.Save();

            if (Enabled.Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
            }
        }

        static string PresetDescription(int defaultFrequency) => $"The frequency for this Preset. This preset is added to the pool this many times, and then a preset is selected from the pool at random. Default is {defaultFrequency}, -1 to disable outright.";
        static string MODescription(float defaultScore) => $"The maximum base score for this MO. The higher, the more likely this MO is to be picked. Default is {defaultScore}, -1 to disable outright.";

        // Preset entries can only be bound once Toolbox has loaded, which is after ConfigManager has cached this plugin's entries.
        // Rebinding the ones already saved on disk now means they exist when ConfigManager scans, and Toolbox_Start's Bind returns them without firing SettingChanged.
        static void PreBindSavedEntries()
        {
            if (!File.Exists(ConfigFile.ConfigFilePath))
                return;

            string section = null;
            string defaultValue = null;

            foreach (var line in File.ReadLines(ConfigFile.ConfigFilePath))
            {
                if (line.StartsWith("["))
                {
                    section = line.Trim('[', ']');
                    continue;
                }

                if (line.StartsWith("# Default value: "))
                {
                    defaultValue = line.Substring("# Default value: ".Length);
                    continue;
                }

                if (line.StartsWith("#") || !line.Contains('='))
                    continue;

                var key = line.Substring(0, line.IndexOf('=')).Trim();

                if (section == "MurderPreset" && int.TryParse(defaultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frequency))
                    ConfigFile.Bind(section, key, frequency, PresetDescription(frequency));
                else if (section == "MurderMO" && float.TryParse(defaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
                    ConfigFile.Bind(section, key, score, MODescription(score));

                defaultValue = null;
            }
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Start))]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                foreach(var so in Toolbox.Instance.resourcesCache[Il2CppType.Of<MurderPreset>()].Values)
                {
                    var preset = so.TryCast<MurderPreset>();
                    // Disabled for hitman, the tutorial preset
                    if (string.IsNullOrEmpty(preset.presetName) || preset.presetName == "Hitman")
                        continue;

                    var presetConfigValue = ConfigFile.Bind("MurderPreset", $"Frequency {preset.presetName.Trim()}.", preset.frequency, PresetDescription(preset.frequency)).Value;

                    if (presetConfigValue <= 0)
                    {
                        preset.disabled = true;
                    }
                    else
                    {
                        preset.frequency = presetConfigValue;
                    }
                }

                foreach(var so in Toolbox.Instance.resourcesCache[Il2CppType.Of<MurderMO>()].Values)
                {
                    var mo = so.TryCast<MurderMO>();
                    // Disabled for hitman, the tutorial mo
                    if (string.IsNullOrEmpty(mo.presetName) || mo.presetName == "Hitman")
                        continue;

                    var moConfigValue = ConfigFile.Bind("MurderMO", $"Maximum base score for {mo.presetName.Trim()}.", mo.pickRandomScoreRange.y, MODescription(mo.pickRandomScoreRange.y)).Value;

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
