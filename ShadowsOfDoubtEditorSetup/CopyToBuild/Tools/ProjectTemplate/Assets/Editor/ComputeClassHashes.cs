using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ComputeClassHashes
{
    // [MenuItem("Shadows Of Doubt/Compute Type Hashes")]
    static void ComputeHashes()
    {
        var assembly = typeof(DatabaseApp).Assembly;
        var typeMap = new TypeToIDMap();

        typeMap.stringMapping = new string[assembly.GetTypes().Length];
        typeMap.mapping = new TypeToIDMap.Mapping[assembly.GetTypes().Length];

        int i = 0;
        foreach (var type in assembly.GetTypes())
        {
            // if(type.Namespace != "") continue;

            typeMap.stringMapping[i] = $"{type.Name}: {FileIDUtil.Compute(type).ToString()}";
            typeMap.mapping[i] = new TypeToIDMap.Mapping() { name = type.Name, id = FileIDUtil.Compute(type).ToString() };

            i++;
        }

        Debug.Log(typeMap.mapping.Length);

        System.IO.File.WriteAllText("./ExtractedTypeIds.json", JsonUtility.ToJson(typeMap, true));
    }

    [System.Serializable]
    class TypeToIDMap
    {
        public string[] stringMapping;
        public Mapping[] mapping;

        [System.Serializable]
        public class Mapping
        {
            public string id;
            public string name;
        }
    }
}