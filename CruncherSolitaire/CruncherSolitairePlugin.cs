using System.Collections.Generic;
using System.Linq;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
#endif

using UnityEngine;
using HarmonyLib;
using CruncherSolitaire;
using AssetBundleLoader;

namespace CruncherSolitaire
{
#if MONO
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class CruncherSolitairePlugin : BaseUnityPlugin
#elif IL2CPP
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class CruncherSolitairePlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        public static ConfigEntry<SessionData.TimeSpeed> TimeScaleWhilePlaying;
        public static ConfigEntry<bool> OneCardDraw;


#if MONO
        public void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            if (!Config.Bind("General", "Enabled", true).Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is disabled.");
                return;
            }

            TimeScaleWhilePlaying = Config.Bind("General", "Time speed in app", SessionData.TimeSpeed.normal);
            OneCardDraw = Config.Bind("General", "Draw one card at a time?", false);


            // Plugin startup logic
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

#if IL2CPP
            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_Card>();
            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_CardHolder>();
            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_Game>();
            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_CardDeck>();

            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_Loader>();
            ClassInjector.RegisterTypeInIl2Cpp<SolitaireCruncherAppPrefab>();
#endif


            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} has added custom types!");
        }

        public static void SetInAppTimeScale(bool inApp)
        {
            if (inApp && TimeScaleWhilePlaying.Value != SessionData.TimeSpeed.normal)
            {
                if (SessionData.Instance.currentTimeSpeed != TimeScaleWhilePlaying.Value)
                {
                    PluginLogger.LogInfo($"Cruncher Solitare: Changing time speed to {System.Enum.GetName(typeof(SessionData.TimeSpeed), TimeScaleWhilePlaying.Value)}");
                    SessionData.Instance.SetTimeSpeed(TimeScaleWhilePlaying.Value);
                }
            }
            else
            {
                if (SessionData.Instance.currentTimeSpeed != SessionData.TimeSpeed.normal)
                {
                    PluginLogger.LogInfo($"Cruncher Solitare: Changing time speed to normal");
                    SessionData.Instance.SetTimeSpeed(SessionData.TimeSpeed.normal);
                }
            }
        }
    }

    class CruncherSolitaireHooks
    {
        [HarmonyPatch(typeof(MainMenuController), "Start")]
        public class MainMenuController_Start
        {
            static bool hasInit = false;

            public static void Prefix()
            {
                if (hasInit) return;
                hasInit = true;

                var moddedAssetBundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "crunchersolitaire"), true, true);

                // This is literally the scriptableobject the game stores these in, so that works
                var newCruncherApp = moddedAssetBundle.LoadAsset<CruncherAppPreset>("Solitaire");
                // newCruncherApp.appContent[0].AddComponent<RealEstateCruncherAppContent>();

                newCruncherApp.appContent[0].AddComponent<SolitaireCruncherAppPrefab>();
                newCruncherApp.appContent[0].AddComponent<PieSolitaire_Loader>();

                foreach (var cruncher in UnityEngine.Resources.FindObjectsOfTypeAll<InteractablePreset>().Where(preset => preset.name.Contains("Cruncher")))
                {
                    cruncher.additionalApps.Insert(cruncher.additionalApps.Count - 2, newCruncherApp);
                }

                CruncherSolitairePlugin.PluginLogger.LogInfo("Loading custom asset bundle complete");
            }
        }
    }

    [HarmonyPatch(typeof(ComputerController), "OnAppLoaded")]
    public class ComputerController_OnAppLoaded
    {
        public static void Postfix(ComputerController __instance)
        {
            if (__instance.playerControlled)
            {
                CruncherSolitairePlugin.SetInAppTimeScale(__instance.currentApp.name == "Solitaire");
            }
        }
    }

    [HarmonyPatch(typeof(ComputerController), "OnAppExit")]
    public class ComputerController_OnAppExit
    {
        public static void Prefix(ComputerController __instance)
        {
            if (__instance.playerControlled)
            {
                CruncherSolitairePlugin.SetInAppTimeScale(false);
            }
        }
    }

    [HarmonyPatch(typeof(ComputerController), "OnPlayerControlChange")]
    public class ComputerController_OnPlayerControlChange
    {
        static bool wasPlayerControlled = false;

        public static void Prefix(ComputerController __instance)
        {
            wasPlayerControlled = __instance.playerControlled;
        }

        public static void Postfix(ComputerController __instance)
        {
            if(wasPlayerControlled)
            {
                CruncherSolitairePlugin.SetInAppTimeScale(false);
                wasPlayerControlled = false;
            }
            else if (__instance.playerControlled)
            {
                CruncherSolitairePlugin.SetInAppTimeScale(__instance.currentApp.name == "Solitaire");
            }
        }
    }
}

public class PieSolitaire_Loader : MonoBehaviour
{
    public static GameObject CardPrefab;
    public static Dictionary<string, Sprite> CardSprites;
    public static Sprite CardSpriteBack;

    void Awake()
    {
        var bundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "crunchersolitaire"), true, stable: true);

        CardPrefab = bundle.LoadAsset<GameObject>("Card");
        CardSprites = new Dictionary<string, Sprite>();

        Dictionary<int, string> numToName = new Dictionary<int, string>()
        {
            { 0, "ace" },
            { 10, "jack" },
            { 11, "queen" },
            { 12, "king" }
        };

        Dictionary<int, string> numToSuit = new Dictionary<int, string>()
        {
            { 0, "clubs" },
            { 1, "diamonds" },
            { 2, "hearts" },
            { 3, "spades" }
        };

        for (int i = 0; i < 52; i++)
        {
            string cardName = numToName.TryGetValue(i % 13, out var name) ? name : ((i % 13) + 1).ToString();
            string cardSuit = numToSuit[i / 13];
            CardSprites[$"{cardName}_of_{cardSuit}"] = bundle.LoadAsset<Sprite>($"{cardName}_of_{cardSuit}");
        }
        CardSpriteBack = bundle.LoadAsset<Sprite>($"card_back");
    }
}