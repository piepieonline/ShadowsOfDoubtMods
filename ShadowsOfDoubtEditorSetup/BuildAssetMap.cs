using AssetsTools.NET.Extra;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

public class BuildAssetMap
{
    const string ClassPackageFileName = "./Tools/ProjectTemplate/classdata.tpk";

    public static void Build(string gamePath)
    {
        var pathToGUID = JObject.Parse(File.ReadAllText("./Tools/currentGUIDs.json"));

        var assetsManager = new AssetsManager();
        assetsManager.LoadClassPackage(ClassPackageFileName);

        // var vanillaRefBundle = customManager.LoadBundleFile(Path.Join(gamePath, "\\Shadows of Doubt_Data\\resources.assets"));
        var globalgamemanagers = assetsManager.LoadAssetsFile(Path.Join(gamePath, "\\Shadows of Doubt_Data\\globalgamemanagers"), false); // customManager.LoadAssetsFileFromBundle(vanillaRefBundle, 0);
        assetsManager.LoadClassDatabaseFromPackage(globalgamemanagers.file.Metadata.UnityVersion);  

        var map = new Dictionary<string, string>();

        foreach (var data in assetsManager.GetBaseField(globalgamemanagers, globalgamemanagers.file.GetAssetsOfType(AssetClassID.ResourceManager)[0])["m_Container.Array"].Children)
        {
            var name = data[0].AsString;
            var pathId = data[1]["m_PathID"].AsLong;

            if(pathToGUID["Resources"][name] != null)
            {
                map["resources.assets-" + pathId.ToString()] = (string)pathToGUID["Resources"][name];
            }
        }

        var resources = assetsManager.LoadAssetsFile(Path.Join(gamePath, "\\Shadows of Doubt_Data\\resources.assets"), false);
        assetsManager.LoadClassDatabaseFromPackage(globalgamemanagers.file.Metadata.UnityVersion);
        
        foreach(var typeToKey in new[] {
            (AssetClassID.AnimationClip, "AnimationClip"),
            (AssetClassID.AnimatorController, "AnimatorController"),
            (AssetClassID.ComputeShader, "ComputeShader"),
            (AssetClassID.Cubemap, "Cubemap"),
            (AssetClassID.Font, "Font"),
            (AssetClassID.Material, "Material"),
            (AssetClassID.Mesh, "Mesh"),
            (AssetClassID.MonoBehaviour, "MonoBehaviour"),
            (AssetClassID.GameObject, "PrefabInstance"),
            (AssetClassID.Sprite, "Sprite"),
            (AssetClassID.Texture2D, "Texture2D"),
            (AssetClassID.RenderTexture, "RenderTexture"),
            (AssetClassID.VideoClip, "VideoClip"),
        })
        {
            foreach (var data in resources.file.GetAssetsOfType(typeToKey.Item1))
            {
                var asset = assetsManager.GetBaseField(resources, resources.file.GetAssetInfo(data.PathId));
                var assetName = asset["m_Name"].AsString.ToLower();
                if (pathToGUID[typeToKey.Item2][assetName] != null)
                {
                    map["resources.assets-" + data.PathId.ToString()] = (string)pathToGUID[typeToKey.Item2][assetName];

                    if(typeToKey.Item1 == AssetClassID.GameObject)
                    {
                        map["resources.assets-" + data.PathId.ToString() + "-FileID"] = (string)pathToGUID["PrefabInstanceFileIDs"][assetName];
                    }
                }
            }
        }

        File.WriteAllText("./Tools/AssetRipper/currentPaths.json", JsonConvert.SerializeObject(map));
    }

    class PathToGUIDMap
    {
        public Dictionary<string, string> resources = new Dictionary<string, string>();
    }
}