using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AssetBundleLoader
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class BundleLoader : BasePlugin
    {
        static ManualLogSource Logger;

        static AssetsManager vanillaManager;
        static AssetsFileInstance vanillaAssetsFile;

        static Dictionary<long, long> oldToNew = new Dictionary<long, long>();
        static Dictionary<string, string> pathToId = new Dictionary<string, string>();

        static string classPackageFileName = "classdata.tpk";

        public override void Load()
        {
            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

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
        }

        public static UniverseLib.AssetBundle LoadBundle(string bundlePath)
        {
            string patchedBundlePath = bundlePath + "_patched_" + Game.Instance.buildID;
            
            if(!System.IO.File.Exists(patchedBundlePath))
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
                                Logger.LogInfo($"Replacing {mapping.Value.PathID} with {newPathId} by Matching (Name and type)");
                                oldToNew.Add(mapping.Value.PathID, newPathId);
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

            return UniverseLib.AssetBundle.LoadFromFile(patchedBundlePath);
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
