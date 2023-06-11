using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsScriptableObject : ScriptableObject
{
    [HideInInspector]
    public bool HasInit = false;
    public string GamePath;
    public BundleToPathMap[] BundleNameToInstallPathMaps = new BundleToPathMap[0];
    
    [Serializable]
    public class BundleToPathMap
    {
        public string BundleName;
        public string InstallPath;
    }
}
