using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Text;

namespace AssetBundleLoader
{
    [System.Serializable]
    class AssetMappingList
    {
        [System.Serializable]
        public class AssetMapping
        {
            public long PathID;
            public string? Name;
            public string? Type;
            public string? Container;
            public string? Path;
        }

        public Dictionary<string, AssetMapping> assets = new Dictionary<string, AssetMapping>();

        public static bool CheckMatch(AssetMapping mapping, AssetTypeValueField asset)
        {
            return mapping.Name == asset["m_Name"].AsString && mapping.Type == asset.TypeName;
        }
    }
}
