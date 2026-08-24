using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace AssetBundleLoader
{
    public static class BundleAssetLoader
    {
        static Dictionary<string, UnityEngine.Object> loadedAssets = new Dictionary<string, UnityEngine.Object>();
        static HashSet<int> bundleAssetIDs = new HashSet<int>();

        public static bool IsBundleAsset(UnityEngine.Object asset)
        {
            return asset != null && bundleAssetIDs.Contains(asset.GetInstanceID());
        }

        public static UnityEngine.Object LoadAsset(string modFolderPath, string relativeBundlePath, string assetName)
        {
            string bundlePath = Path.Combine(modFolderPath, relativeBundlePath);
            string cacheKey = bundlePath + "|" + assetName;

            if (loadedAssets.ContainsKey(cacheKey) && loadedAssets[cacheKey] != null)
            {
                return loadedAssets[cacheKey];
            }

            var bundle = BundleLoader.LoadBundle(bundlePath);
            if (bundle == null)
            {
                BundleLoader.PluginLogger.LogError($"Asset '{assetName}' failed to load, the bundle {bundlePath} couldn't be opened!");
                return null;
            }

            UnityEngine.Object asset = null;
            foreach (var candidate in bundle.LoadAllAssets())
            {
                if (candidate.name != assetName) continue;

                if (asset != null)
                {
                    BundleLoader.PluginLogger.LogWarning($"Bundle {relativeBundlePath} holds more than one asset named '{assetName}', using the first one found");
                    break;
                }

                asset = candidate;
            }

            if (asset == null)
            {
                BundleLoader.PluginLogger.LogError($"Asset '{assetName}' failed to load, bundle {relativeBundlePath} doesn't contain it!");
                return null;
            }

            // Nothing in the scene holds these, so Unity is free to collect them between loads unless told otherwise
            asset.hideFlags = HideFlags.DontUnloadUnusedAsset;

            loadedAssets[cacheKey] = asset;
            bundleAssetIDs.Add(asset.GetInstanceID());

            return asset;
        }
    }
}
