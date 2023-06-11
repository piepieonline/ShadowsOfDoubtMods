using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class RewriteAssetBundle
{
    const string ClassPackageFileName = "classdata.tpk";

    static Dictionary<string, PathIdMap_Asset> guidToAsset = new Dictionary<string, PathIdMap_Asset>();

    static Dictionary<long, PathIdMap_Asset> oldPathToAsset = new Dictionary<long, PathIdMap_Asset>();

    public static void Rewrite(string[] bundleNames)
    {
        var customManager = new AssetsManager();
        customManager.LoadClassPackage(ClassPackageFileName);

        var vanillaRefBundle = customManager.LoadBundleFile($"AssetBundles/Unpatched/vanillacontent");
        var vanillaRefAssetsFile = customManager.LoadAssetsFileFromBundle(vanillaRefBundle, 0);
        customManager.LoadClassDatabaseFromPackage(vanillaRefAssetsFile.file.Metadata.UnityVersion);

        foreach(var asset in JsonUtility.FromJson<PathIdMap>(File.ReadAllText("path_id_map.json")).Files.Where(file => file.Name == "resources.assets").First().Assets)
        {
            guidToAsset[asset.GUID] = asset;
        }

        foreach (var data in customManager.GetBaseField(vanillaRefAssetsFile, vanillaRefAssetsFile.file.GetAssetsOfType(AssetClassID.AssetBundle)[0])["m_Container.Array"].Children)
        {
            var name = data[0].AsString;
            var pathId = data[1]["asset.m_PathID"].AsLong;

            var guid = File.ReadAllLines(name + ".meta")[1].Substring(6);

            if(guidToAsset.ContainsKey(guid))
            {
                oldPathToAsset[pathId] = guidToAsset[guid];
            }
            else
            {
                Debug.LogWarning($"mismatch - {guid} - {name}");
            }
        }

        foreach (var bundleName in bundleNames)
        {
            if (bundleName == "vanillacontent") continue;

            string unpatchedPath = $"AssetBundles/Unpatched/{bundleName}";
            string patchedPath = $"AssetBundles/Patched/{bundleName}";

            var customBundle = customManager.LoadBundleFile(unpatchedPath);
            var customAssetsFile = customManager.LoadAssetsFileFromBundle(customBundle, 0, false);
            customManager.LoadClassDatabaseFromPackage(customAssetsFile.file.Metadata.UnityVersion);

            var bundleReplacers = new List<BundleReplacer>();
            var assetsReplacers = new List<AssetsReplacer>();

            Dictionary<long, PathIdMap_Asset> manifest = new Dictionary<long, PathIdMap_Asset>();

            // Remap PathIDs
            foreach (var goInfo in customAssetsFile.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
            {
                var goBase = customManager.GetBaseField(customAssetsFile, goInfo);
                if (WalkAndReplace(goInfo, goBase, false, manifest))
                {
                    assetsReplacers.Add(new AssetsReplacerFromMemory(customAssetsFile.file, goInfo, goBase));
                }
            }
            
            // Write the remapped manifest
            File.WriteAllText(patchedPath + ".manifest.json", JsonConvert.SerializeObject(new Manifest() { assets = manifest }));

            // Remap GameDll to Assembly-CSharp
            foreach (var goInfo in customAssetsFile.file.GetAssetsOfType(AssetClassID.MonoScript))
            {
                var goBase = customManager.GetBaseField(customAssetsFile, goInfo);
                if(goBase.Get("m_AssemblyName").AsString == "GameDll.dll")
                {
                    goBase["m_AssemblyName"].AsString = "Assembly-CSharp.dll";
                    assetsReplacers.Add(new AssetsReplacerFromMemory(customAssetsFile.file, goInfo, goBase));
                }
            }

            // Replace vanilla bundle reference with resources
            customAssetsFile.file.Metadata.Externals[0].PathName = "resources.assets";

            bundleReplacers.Add(new BundleReplacerFromAssets(customAssetsFile.name, null, customAssetsFile.file, assetsReplacers));

            using (AssetsFileWriter writer = new AssetsFileWriter(patchedPath))
            {
                customBundle.file.Write(writer, bundleReplacers);
            }

            customManager.UnloadBundleFile(unpatchedPath);
        }
    }

    static bool WalkAndReplace(AssetFileInfo info, AssetTypeValueField parent, bool hasReplaced, Dictionary<long, PathIdMap_Asset> manifest)
    {
        var pathId = parent.Get("m_PathID");
        var fileId = parent.Get("m_FileID");

        if (pathId != null && fileId != null && pathId.FieldName != "DUMMY" && fileId.AsLong != 0)
        {
            var oldPathId = pathId.AsLong;
            if (oldPathToAsset.ContainsKey(oldPathId))
            {
                var asset = oldPathToAsset[oldPathId];
                parent["m_PathID"].AsLong = asset.PathID;
                manifest[asset.PathID] = asset;
                UnityEngine.Debug.Log($"Mapped: {asset.Name} from {oldPathId} to {asset.PathID}");
                return true;
            }
        }
        else
        {
            foreach (var child in parent)
            {
                if (WalkAndReplace(info, child, hasReplaced, manifest))
                {
                    hasReplaced = true;
                }
            }
        }
        return hasReplaced;
    }
}