using UnityEditor;
using System.IO;
using UnityEngine;

public class CreateAssetBundles
{
    [MenuItem("SOD/Build AssetBundles")]
    static void BuildAllAssetBundles()
    {
        string assetBundleDirectory = "Assets/AssetBundles";
        if(!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }

        var gameExtractDirectory = new DirectoryInfo("Assets/GameExtract");
        gameExtractDirectory.Move("Assets\\GameExtract~");
        var bundles = BuildPipeline.BuildAssetBundles(assetBundleDirectory, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
        gameExtractDirectory.Move("Assets\\GameExtract");

        foreach(var s in bundles.GetAllAssetBundles())
        {
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = @"D:\Python\python.exe",
                    Arguments = @"Assets\Editor\ModifyBundles.py " + s,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            proc.WaitForExit();

            string output = proc.StandardOutput.ReadToEnd();
            EditorUtility.DisplayDialog("Bundle Creation Complete", output, "OK");
            Debug.Log(output);
        }
    }

    [MenuItem("Component/Item")]
    static void Test()
    {
        Debug.Log("helo");
        Debug.Log(UnityEditor.Selection.activeGameObject);

        // UnityEditor.Selection.activeGameObject.AddComponent<CruncherAppContent>();
    }

    [MenuItem("Component/SO")]
    static void TestSO()
    {

        
        var asset = ScriptableObject.CreateInstance<JobPreset>();
        //var asset = ScriptableObject.CreateInstance<PieTestScriptableObject>();

        AssetDatabase.CreateAsset(asset, "Assets/JobPreset.asset");
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();

        Selection.activeObject = asset;
        
    }
}