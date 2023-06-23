using AssetBundleLoader;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using CruncherSolitaire;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CruncherSolitaire
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class CruncherSolitairePlugin : BasePlugin
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

            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_Card>();
            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_CardHolder>();
            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_Game>();
            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_CardDeck>();

            ClassInjector.RegisterTypeInIl2Cpp<PieSolitaire_Loader>();
            ClassInjector.RegisterTypeInIl2Cpp<SolitaireCruncherAppPrefab>();

            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} has added custom types!");
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

                // var moddedAssetBundle = UniverseLib.AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "realestatelistingcruncherappbundle"));
                var moddedAssetBundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "crunchersolitaire"), true);

                // This is literally the scriptableobject the game stores these in, so that works
                var newCruncherApp = moddedAssetBundle.LoadAsset<CruncherAppPreset>("Solitaire");
                // newCruncherApp.appContent[0].AddComponent<RealEstateCruncherAppContent>();

                newCruncherApp.appContent[0].AddComponent<SolitaireCruncherAppPrefab>();
                newCruncherApp.appContent[0].AddComponent<PieSolitaire_Loader>();

                foreach (var cruncher in UnityEngine.Resources.FindObjectsOfTypeAll<InteractablePreset>().Where(preset => preset.name.Contains("Cruncher")))
                {
                    cruncher.additionalApps.Insert(cruncher.additionalApps.Count - 2, newCruncherApp);
                }

                CruncherSolitairePlugin.Logger.LogInfo("Loading custom asset bundle complete");
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
        var bundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "crunchersolitaire"), true);

        CardPrefab = bundle.LoadAsset<GameObject>("Card");

        CruncherSolitairePlugin.Logger.LogInfo($"Loaded card: {CardPrefab.name}"); 

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