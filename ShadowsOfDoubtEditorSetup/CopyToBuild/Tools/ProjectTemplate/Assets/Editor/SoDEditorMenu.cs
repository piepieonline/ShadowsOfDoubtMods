using UnityEditor;
using System.IO;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class SoDEditorMenu
{
    [MenuItem("Shadows Of Doubt/Build AssetBundles", priority = 0)]
    static void BuildAllAssetBundles()
    {
        string assetBundleDirectory = "AssetBundles/Unpatched";

        var bundleBuilds = new List<AssetBundleBuild>();

        Debug.Log("Creating dependency tags");

        foreach (var bundle in AssetDatabase.GetAllAssetBundleNames())
        {
            bundleBuilds.Add(new AssetBundleBuild() { assetBundleName = bundle, assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(bundle) });

            if (bundle == "vanillacontent") continue;
            foreach (var asset in AssetDatabase.GetAssetPathsFromAssetBundle(bundle))
            {
                foreach (var dep in AssetDatabase.GetDependencies(asset))
                {
                    if (dep.ToLower().Contains("gameextract"))
                    {
                        AssetImporter.GetAtPath(dep).assetBundleName = "vanillacontent";
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Building bundles");

        var bundles = BuildPipeline.BuildAssetBundles(assetBundleDirectory, bundleBuilds.ToArray(), BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows);
        var builtBundles = bundles.GetAllAssetBundles();

        Debug.Log("Rewriting bundles");

        RewriteAssetBundle.Rewrite(builtBundles);
        
        Debug.Log("Installing bundles");

        foreach(var bundleToInstall in AssetDatabase.LoadAssetAtPath<SettingsScriptableObject>("Assets/EditorSettings.asset").BundleNameToInstallPathMaps)
        {
            if(builtBundles.Contains(bundleToInstall.BundleName))
            {
                File.Copy($"AssetBundles/Patched/{bundleToInstall.BundleName}", $"{bundleToInstall.InstallPath}{bundleToInstall.BundleName}", true);
                File.Copy($"AssetBundles/Patched/{bundleToInstall.BundleName}.manifest.json", $"{bundleToInstall.InstallPath}{bundleToInstall.BundleName}.manifest.json", true);
            }
        }

        EditorUtility.DisplayDialog("Bundle Creation Complete", "Bundle creation complete.\nCheck the console for any issues.", "OK");
    }

    [MenuItem("Shadows Of Doubt/Extras/Fix Materials", priority = 1)]
    public static void FixMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t: material");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material temp = (Material)AssetDatabase.LoadAssetAtPath(path, typeof(Material));
            if (temp != null && (temp.shader.name == "" || temp.shader.name == "Hidden/InternalErrorShader" || temp.shader.name == null))
                temp.shader = Shader.Find("HDRP/Lit");
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Shadows Of Doubt/Extras/Fix Texture GUIDs", priority = 1)]
    public static void FixTexture2DGUIDs()
    {
        Dictionary<string, PathIdMap_Asset> nameToAsset = new Dictionary<string, PathIdMap_Asset>();
        foreach (var file in JsonUtility.FromJson<PathIdMap>(File.ReadAllText("path_id_map.json")).Files)
        {
            foreach (var asset in file.Assets)
            {
                if (asset.Type == "Texture2D")
                {
                    if (nameToAsset.ContainsKey(asset.Name) && nameToAsset[asset.Name].GUID != asset.GUID)
                        Debug.LogWarning($"File collision: {asset.Name} has {nameToAsset[asset.Name].GUID} and {asset.GUID}");
                    nameToAsset[asset.Name] = asset;
                }
            }
        }

        foreach (var file in Directory.GetFiles("Assets/GameExtract/Texture2D", "*.meta", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            var guid = content.Split('\n')[1].Substring(6);
            var fileName = file.Split('\\').Last().Split('.')[0];

            if (nameToAsset.ContainsKey(fileName) && nameToAsset[fileName].GUID != guid && nameToAsset[fileName].Type == "Texture2D")
            {
                File.WriteAllText(file, content.Replace(guid, nameToAsset[fileName].GUID));
            }
        }

        /*
        if (File.Exists("Texture2DGUIDs.json"))
        {
            Dictionary<string, string> pathToGUID = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("Texture2DGUIDs.json"));

            foreach (var file in Directory.GetFiles("Assets/GameExtract/Texture2D", "*.meta", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                var guid = content.Split('\n')[1].Substring(6);
                var fileName = file.Split('\\').Last().Split('.')[0];

                File.WriteAllText(file, content.Replace(guid, pathToGUID[fileName]));
            }
        }
        */
    }
}