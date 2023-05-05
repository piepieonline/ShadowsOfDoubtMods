using UnityEditor;
using System.IO;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class CreateAssetBundles
{
    [MenuItem("Shadows Of Doubt/Build AssetBundles")]
    static void BuildAllAssetBundles()
    {
        string assetBundleDirectory = "Assets/AssetBundles~";
        if(Directory.Exists(assetBundleDirectory))
        {
            Directory.Delete(assetBundleDirectory, true );
        }
        Directory.CreateDirectory(assetBundleDirectory);

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

        Debug.Log("Building bundles");

        var bundles = BuildPipeline.BuildAssetBundles(assetBundleDirectory, bundleBuilds.ToArray(), BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);

        Debug.Log("Rewriting bundles");

        string output = "";
        string error = "";
        foreach(var s in bundles.GetAllAssetBundles())
        {
            if (s == "vanillacontent") continue;

            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = @"D:\Python\python.exe",
                    Arguments = @"Assets\Editor\ModifyBundles.py " + s,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            };

            proc.Start();

            output += proc.StandardOutput.ReadToEnd();
            // error += proc.StandardError.ReadToEnd();

            proc.WaitForExit();
        }

        EditorUtility.DisplayDialog("Bundle Creation Complete", output, "OK");
        Debug.LogWarning(error);
        Debug.Log(output);
    }

    [MenuItem("Shadows Of Doubt/Extras/Fix Materials")]
    static void FixMaterials()
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

    [MenuItem("Shadows Of Doubt/Extras/Construct GUID Mapping")]
    static void ConstructGUIDMap()
    {

        Dictionary<string, string> type_nameToGuid = new Dictionary<string, string>();

        foreach(var assetPath in AssetDatabase.GetAllAssetPaths())
        {
            var asset = AssetDatabase.LoadAssetAtPath(assetPath, AssetDatabase.GetMainAssetTypeAtPath(assetPath));

            if(asset != null)
            {
                type_nameToGuid[asset.GetType().Name + "_" + asset.name] = AssetDatabase.GUIDFromAssetPath(assetPath).ToString();
            }
        }

        var map = JsonUtility.FromJson<PathIdMap>(File.ReadAllText("D:\\Game Modding\\ShadowsOfDoubt\\ShadowsOfDoubtEditor\\AuxiliaryFiles\\path_id_map.json"));
        var guidMap = "{";

        Dictionary<string, List<string>> seenAssets = new Dictionary<string, List<string>>();

        foreach(var file in map.Files)
        {
            foreach(var asset in file.Assets)
            {
                if(type_nameToGuid.TryGetValue(asset.Type + "_" + asset.Name, out var guid))
                {
                    if (!seenAssets.ContainsKey(guid))
                    {
                        seenAssets[guid] = new List<string>() { asset.Name };
                    }
                    else
                    {
                        seenAssets[guid].Add($"{asset.Name}");
                        continue;
                    }
                    
                    guidMap += $"\"{guid}\": \"{asset.PathID}\",";
                }
            }
        }

        string errors = "";
        foreach(var keyValue in seenAssets)
        {
            errors += $"{keyValue.Key}: {string.Join(", ", keyValue.Value)}\r\n";
        }

        File.WriteAllText("D:\\Game Modding\\ShadowsOfDoubt\\ShadowsOfDoubtEditor\\AuxiliaryFiles\\errors.txt", errors);
        File.WriteAllText("D:\\Game Modding\\ShadowsOfDoubt\\ShadowsOfDoubtEditor\\AuxiliaryFiles\\path_id_map_new.json", guidMap + "}");

        Debug.Log("New Mapping Written");
    }

    [Serializable]
    public class PathIdMap
    {
        [Serializable]
        public class PathIdMap_Files
        {
            public string Name;
            public PathIdMap_Assets[] Assets;
        }

        [Serializable]
        public class PathIdMap_Assets
        {
            public int PathID;
            public string Name;
            public string Type;
            public string GUID;
        }

        public PathIdMap_Files[] Files;
    }

    [Serializable]
    public class GuidIdMap
    {
        public Dictionary<string, int> mapping = new Dictionary<string, int>();
    }
}