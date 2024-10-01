using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using HarmonyLib;
using BepInEx.Configuration;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace NoHandcuffEscapes
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class NoHandcuffEscapesPlugin : BaseUnityPlugin
#elif IL2CPP
    public class NoHandcuffEscapesPlugin : BasePlugin
#endif
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> HoursToLockFor;
        public static ConfigEntry<bool> NeverRelease;

        private static NoHandcuffEscapesPlugin instance;

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

            instance = this;

            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Restart required)");
            HoursToLockFor = Config.Bind("General", "Handcuff Lock Time", 24f, "How many hours should handcuffs stay locked for? (Restart required)");
            NeverRelease = Config.Bind("General", "Handcuffs Never Unlock", false, "Handcuffs never release? (Overrides the number of hours)");

            if(Enabled.Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
            }
        }
    }

    [HarmonyPatch(typeof(GameplayControls), "Awake")]
    public class GameplayControls_Awake
    {
        public static void Postfix()
        {
            GameplayControls.Instance.restrainedTimer = NoHandcuffEscapesPlugin.HoursToLockFor.Value > 0 ? NoHandcuffEscapesPlugin.HoursToLockFor.Value : 2;
        }
    }

    [HarmonyPatch(typeof(NewAIController), "FrequentUpdate")]
    public class NewAIController_FrequentUpdate
    {
        public static void Prefix(NewAIController __instance)
        {
            if(NoHandcuffEscapesPlugin.NeverRelease.Value)
            {
                __instance.restrainTime = SessionData.Instance.gameTime + 1;
            }
        }
    }
}
