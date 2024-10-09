using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

#if MONO
using BepInEx.Unity.Mono;
using UnityEngine;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
using UniverseLib;
#endif

namespace AssetBundleLoader
{
#if MONO
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class BundleLoader : BaseUnityPlugin
#elif IL2CPP
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class BundleLoader : BasePlugin
#endif
    {
        public static bool EnableLogging = false;

        public static ManualLogSource PluginLogger;

        static Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

        public delegate List<UnityEngine.ScriptableObject> LoadObjects(Il2CppSystem.Collections.Generic.List<UnityEngine.ScriptableObject> loadedScriptableObjects);
        public static List<LoadObjects> loadObjectDelegates = new List<LoadObjects>();

#if MONO
        public void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

#if IL2CPP
            // Manually init UniverseLib, in case it hasn't been done yet (No UnityExplorer or ConfigManager)
            UniverseLib.Universe.Init(0, null, null, new UniverseLib.Config.UniverseLibConfig()
            {
                Unhollowed_Modules_Folder = System.IO.Path.Combine(Paths.BepInExRootPath, "interop")
            });
#endif

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            new Harmony($"{MyPluginInfo.PLUGIN_GUID}").PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }

        public static AssetBundle LoadBundle(string bundlePath, bool skipCache = false, bool stable = false)
        {
            string patchedBundlePath = System.IO.File.Exists(bundlePath) ? bundlePath : bundlePath + "_stable";

            if (loadedBundles.ContainsKey(patchedBundlePath) && loadedBundles[patchedBundlePath] != null)
            {
                return loadedBundles[patchedBundlePath];
            }

            PluginLogger.LogInfo("Using existing bundle: " + patchedBundlePath);

            EnableLogging = false;

            loadedBundles[patchedBundlePath] = AssetBundle.LoadFromFile(patchedBundlePath);
            return loadedBundles[patchedBundlePath];
        }
    }
}
