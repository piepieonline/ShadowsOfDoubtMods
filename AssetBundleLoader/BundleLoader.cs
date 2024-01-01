using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx;
using BepInEx.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

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

        static ManualLogSource PluginLogger;

        static AssetsManager vanillaManager;
        static AssetsFileInstance vanillaAssetsFile;

        static Dictionary<long, long> oldToNew = new Dictionary<long, long>();
        static Dictionary<string, string> pathToId = new Dictionary<string, string>();

        static string classPackageFileName = "classdata.tpk";

        static Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

        static string bundleFileName = "DUMMY";

#if MONO
        public void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            // TODO: Not sure how to do this properly. Right now, read the largest bundle as the base (and hope the devs don't split bundles :S)
            string fileToRead = System.IO.Directory.EnumerateFiles(System.IO.Path.Combine(Paths.GameRootPath, "Shadows of Doubt_Data", "StreamingAssets", "aa", "StandaloneWindows64"))
                .OrderByDescending(file => new System.IO.FileInfo(file).Length)
                .FirstOrDefault();

            vanillaManager = new AssetsManager();
            vanillaManager.LoadClassPackage(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), classPackageFileName));
            var bundle = vanillaManager.LoadBundleFile(fileToRead);
            vanillaAssetsFile = vanillaManager.LoadAssetsFileFromBundle(bundle, 0, false);
            vanillaManager.LoadClassDatabaseFromPackage(vanillaAssetsFile.file.Metadata.UnityVersion);

            PluginLogger.LogInfo($"{MyPluginInfo.PLUGIN_GUID}: Loading bundle {bundle.name} with file {vanillaAssetsFile.name}");

            bundleFileName = vanillaAssetsFile.name;

#if IL2CPP
            // Manually init UniverseLib, in case it hasn't been done yet (No UnityExplorer or ConfigManager)
            UniverseLib.Universe.Init(0, null, null, new UniverseLib.Config.UniverseLibConfig()
            {
                Unhollowed_Modules_Folder = System.IO.Path.Combine(Paths.BepInExRootPath, "interop")
            });
#endif

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }

        public static AssetBundle LoadBundle(string bundlePath, bool skipCache = false, bool stable = false)

        {
            string patchedBundlePath = stable ? (bundlePath + "_stable") : (bundlePath + "_patched_" + Game.Instance.buildID);

            if (stable) skipCache = false;

            if(loadedBundles.ContainsKey(patchedBundlePath) && loadedBundles[patchedBundlePath] != null)
            {
                return loadedBundles[patchedBundlePath];
            }

            if(!System.IO.File.Exists(patchedBundlePath) || skipCache)
            {
                var customManager = new AssetsManager();
                customManager.LoadClassPackage(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), classPackageFileName));
                var customBundle = customManager.LoadBundleFile(bundlePath, false);
                var customAssetsFile = customManager.LoadAssetsFileFromBundle(customBundle, 0, false);
                customManager.LoadClassDatabaseFromPackage(customAssetsFile.file.Metadata.UnityVersion);

                var existingMappingText = System.IO.File.ReadAllText(bundlePath + ".manifest.json");
                PluginLogger.LogInfo(existingMappingText);
                var existingMapping = JsonConvert.DeserializeObject<AssetMappingList>(existingMappingText);

                foreach (var mapping in existingMapping.assets)
                {
                    if (EnableLogging)
                        PluginLogger.LogInfo($"Replacing {mapping.Value.PathID} with {mapping.Value.AddressablePathID} by Matching (Name and type)");
                    oldToNew[mapping.Value.PathID] = mapping.Value.AddressablePathID;
                }

                var bundleReplacers = new List<BundleReplacer>();
                var assetsReplacers = new List<AssetsReplacer>();

                foreach (var goInfo in customAssetsFile.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
                {
                    var goBase = customManager.GetBaseField(customAssetsFile, goInfo);
                    if (WalkAndReplace(goInfo, goBase, false))
                    {
                        assetsReplacers.Add(new AssetsReplacerFromMemory(customAssetsFile.file, goInfo, goBase));
                    }
                }

                customAssetsFile.file.Metadata.Externals[0].PathName = $"archive:/{bundleFileName}/{bundleFileName}";

                bundleReplacers.Add(new BundleReplacerFromAssets(customAssetsFile.name, null, customAssetsFile.file, assetsReplacers));

                using (AssetsFileWriter writer = new AssetsFileWriter(patchedBundlePath))
                {
                    customBundle.file.Write(writer, bundleReplacers);
                }

                PluginLogger.LogInfo("Using new bundle: " + patchedBundlePath);
            }
            else
            {
                PluginLogger.LogInfo("Using existing bundle: " + patchedBundlePath);
            }

            EnableLogging = false;

            loadedBundles[patchedBundlePath] = AssetBundle.LoadFromFile(patchedBundlePath);
            return loadedBundles[patchedBundlePath];
        }

        static bool WalkAndReplace(AssetFileInfo info, AssetTypeValueField parent, bool hasReplaced)
        {
            var pathId = parent.Get("m_PathID");

            if (pathId != null && pathId.FieldName != "DUMMY")
            {
                if (oldToNew.ContainsKey(pathId.AsLong))
                {
                    parent["m_PathID"].AsLong = oldToNew[pathId.AsLong];
                    return true;
                }
            }
            else
            {
                foreach (var child in parent)
                {
                    if (WalkAndReplace(info, child, hasReplaced))
                    {
                        hasReplaced = true;
                    }
                }
            }
            return hasReplaced;
        }
    }
}
