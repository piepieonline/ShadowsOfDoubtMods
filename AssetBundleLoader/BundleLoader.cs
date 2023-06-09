﻿using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AssetBundleLoader
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class BundleLoader : BasePlugin
    {
        public static bool EnableLogging = false;

        static ManualLogSource Logger;

        static AssetsManager vanillaManager;
        static AssetsFileInstance vanillaAssetsFile;

        static Dictionary<long, long> oldToNew = new Dictionary<long, long>();
        static Dictionary<string, string> pathToId = new Dictionary<string, string>();

        static string classPackageFileName = "classdata.tpk";

        static Dictionary<string, UniverseLib.AssetBundle> loadedBundles = new Dictionary<string, UniverseLib.AssetBundle>();

        public override void Load()
        {
            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            vanillaManager = new AssetsManager();
            vanillaManager.LoadClassPackage(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), classPackageFileName));
            vanillaAssetsFile = vanillaManager.LoadAssetsFile(Paths.GameRootPath + "\\Shadows of Doubt_Data\\resources.assets", false);
            vanillaManager.LoadClassDatabaseFromPackage(vanillaAssetsFile.file.Metadata.UnityVersion);

            /*
            // Path matching is corrupting, needs more research
            // Probably a container at the path, with no child reference
         
            var vanillaGMManager = new AssetsManager();
            vanillaGMManager.LoadClassPackage(classPackagePath);
            var vanillaGMAssetsFile = vanillaGMManager.LoadAssetsFile(gameManagerPath, false);
            vanillaGMManager.LoadClassDatabaseFromPackage(vanillaGMAssetsFile.file.Metadata.UnityVersion);
            var vanillaGMResourceManager = vanillaGMManager.GetBaseField(vanillaGMAssetsFile, vanillaGMAssetsFile.file.GetAssetsOfType(AssetClassID.ResourceManager)[0]);

            foreach (var data in vanillaGMResourceManager["m_Container.Array"].Children)
            {
                var path = data[0].AsString;
                var pathId = data[1]["m_PathID"].AsString;

                pathToId[path] = pathId;
            }
            */

            // Manually init UniverseLib, in case it hasn't been done yet (No UnityExplorer or ConfigManager)
            UniverseLib.Universe.Init(0, null, null, new UniverseLib.Config.UniverseLibConfig()
            {
                Unhollowed_Modules_Folder = System.IO.Path.Combine(Paths.BepInExRootPath, "interop")
            });
        }

        public static UniverseLib.AssetBundle LoadBundle(string bundlePath, bool skipCache = false)
        {
            string patchedBundlePath = bundlePath + "_patched_" + Game.Instance.buildID;
            
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

                var existingMapping = JsonConvert.DeserializeObject<AssetMappingList>(System.IO.File.ReadAllText(bundlePath + ".manifest.json"));

                foreach (var mapping in existingMapping.assets)
                {
                    if (mapping.Value.Path != null && pathToId.ContainsKey(mapping.Value.Path))
                    {
                        if(EnableLogging)
                            Logger.LogInfo($"Replacing {mapping.Value.PathID} with {pathToId[mapping.Value.Path]} by Path");
                        oldToNew[mapping.Value.PathID] = long.Parse(pathToId[mapping.Value.Path]);
                    }
                    else
                    {

                        for (int i = 0; i < vanillaAssetsFile.file.AssetInfos.Count; i++)
                        {
                            long newPathId = mapping.Value.PathID + i;
                            if (AssetMappingList.CheckMatch(
                                mapping.Value,
                                vanillaManager.GetBaseField(vanillaAssetsFile, vanillaAssetsFile.file.GetAssetInfo(newPathId))
                                ))
                            {
                                if (EnableLogging)
                                    Logger.LogInfo($"Replacing {mapping.Value.PathID} with {newPathId} by Matching (Name and type)");

                                if(oldToNew.ContainsKey(mapping.Value.PathID))
                                {
                                    if (oldToNew[mapping.Value.PathID] != newPathId)
                                    {
                                        Logger.LogWarning($"Duplicate mismatch {mapping.Value.PathID} matches both {oldToNew[mapping.Value.PathID]} (kept) and {newPathId} (discarded)");
                                    }
                                }
                                else
                                {
                                    oldToNew.Add(mapping.Value.PathID, newPathId);
                                }
                                break;
                            }
                        }
                    }
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

                bundleReplacers.Add(new BundleReplacerFromAssets(customAssetsFile.name, null, customAssetsFile.file, assetsReplacers));

                using (AssetsFileWriter writer = new AssetsFileWriter(patchedBundlePath))
                {
                    customBundle.file.Write(writer, bundleReplacers);
                }

                Logger.LogInfo("Using new bundle: " + patchedBundlePath);
            }
            else
            {
                Logger.LogInfo("Using existing bundle: " + patchedBundlePath);
            }

            EnableLogging = false;

            loadedBundles[patchedBundlePath] = UniverseLib.AssetBundle.LoadFromFile(patchedBundlePath);
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
