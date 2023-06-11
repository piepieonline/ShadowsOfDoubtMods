using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

class SetupHelper : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        var editorSettings = AssetDatabase.LoadAssetAtPath<SettingsScriptableObject>("Assets/EditorSettings.asset");
        if (!editorSettings.HasInit)
        {
            if (EditorUtility.DisplayDialog("First-Start Initialisation", "Click continue to run first-start initialisation", "Continue"))
            {
                SoDEditorMenu.FixMaterials();
                SoDEditorMenu.FixTexture2DGUIDs();

                editorSettings.HasInit = true;
                EditorUtility.SetDirty(editorSettings);
                AssetDatabase.SaveAssets();
                Debug.Log("First-Start Initialisation Complete");
            }
        }
    }


}