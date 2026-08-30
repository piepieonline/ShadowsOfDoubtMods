using System.Collections.Generic;
using System.IO;

using UnityEngine;

using JsonWrapper = AssetBundleLoader.JsonLoader.NewtonsoftExtensions;

namespace AssetBundleLoader
{
    public static class PrefabLoader
    {
        const string prefabExtension = ".sodprefab.json";

        static Dictionary<string, GameObject> loadedPrefabs = new Dictionary<string, GameObject>();
        static GameObject prefabContainer;
        
        public static GameObject LoadPrefabFromJSON(string modFolderPath, string relativePath)
        {
            string source = relativePath + prefabExtension;
            string writtenPath = Path.Combine(modFolderPath, source);

            string filePath = ModFilePath.Resolve(writtenPath);
            if (filePath == null)
            {
                BundleLoader.PluginLogger.LogError($"Prefab '{relativePath}' failed to load, {writtenPath} doesn't exist!");
                return null;
            }

            string cacheKey = ModFilePath.CacheKey(filePath);
            if (loadedPrefabs.ContainsKey(cacheKey) && loadedPrefabs[cacheKey] != null)
            {
                return loadedPrefabs[cacheKey];
            }

            // Meshes and textures are named relative to the prefab, so a model folder can be moved around whole
            string assetFolder = Path.GetDirectoryName(writtenPath);

            var json = JsonWrapper.NewtonsoftJson.JToken_Parse(File.ReadAllText(filePath));
            var prefab = BuildObject(json, assetFolder, source, PrefabContainer());

            ApplyPrefabType(prefab, json, source);

            loadedPrefabs[cacheKey] = prefab;

            return prefab;
        }

        static void ApplyPrefabType(GameObject prefab, dynamic json, string source)
        {
            string prefabType = PrefabJson.Text(json, "prefabType") ?? "prop";

            if (prefabType == "prop") return;

            if (prefabType != "building")
            {
                BundleLoader.PluginLogger.LogWarning($"{source} asks for the prefabType '{prefabType}', which isn't one this can build, so it was left as a plain prop");
                return;
            }

            BuildingModelLoader.ApplyBuildingConventions(prefab);

            // Defaults to on, as most usages will want them
            if (PrefabJson.Bool(json, "defaultColliders", true))
            {
                AddDefaultColliders(prefab);
            }
        }

        static GameObject BuildObject(dynamic json, string assetFolder, string source, Transform parent)
        {
            var gameObject = new GameObject(PrefabJson.Text(json, "name") ?? "Prefab");
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = PrefabJson.ReadVector3(json, "position", Vector3.zero);
            gameObject.transform.localEulerAngles = PrefabJson.ReadVector3(json, "rotation", Vector3.zero);
            gameObject.transform.localScale = PrefabJson.ReadVector3(json, "scale", Vector3.one);

            SetTag(gameObject, PrefabJson.Text(json, "tag"), source);
            SetLayer(gameObject, json, source);

            foreach (var component in PrefabJson.Array(json, "components"))
            {
                AddComponent(gameObject, component, assetFolder, source);
            }

            foreach (var child in PrefabJson.Array(json, "children"))
            {
                BuildObject(child, assetFolder, source, gameObject.transform);
            }

            return gameObject;
        }

        static void AddComponent(GameObject gameObject, dynamic component, string assetFolder, string source)
        {
            string type = PrefabJson.Text(component, "type");

            switch (type)
            {
                case "MeshRenderer":
                    AddMeshRenderer(gameObject, component, assetFolder, source);
                    break;
                case "MeshCollider":
                    AddMeshCollider(gameObject, component, assetFolder, source);
                    break;
                case "BoxCollider":
                    AddBoxCollider(gameObject, component);
                    break;
                default:
                    BundleLoader.PluginLogger.LogError($"{source} asks for a component of type '{type}', which isn't one this can build");
                    break;
            }
        }
        
        static void AddMeshRenderer(GameObject gameObject, dynamic component, string assetFolder, string source)
        {
            var mesh = LoadMesh(component, assetFolder, source);
            if (mesh != null)
            {
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            }
            
            var renderer = gameObject.AddComponent<MeshRenderer>();

            var materialJson = PrefabJson.Field(component, "material");
            if (materialJson == null)
            {
                BundleLoader.PluginLogger.LogError($"{source} has a MeshRenderer with no material, so it will render untextured");
                return;
            }

            var material = MaterialLoader.LoadFromPrefabBlock(materialJson, assetFolder, source);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        static void AddMeshCollider(GameObject gameObject, dynamic component, string assetFolder, string source)
        {
            // Left unsaid, a collider takes the mesh sitting on the same object, which is the usual case
            var mesh = LoadMesh(component, assetFolder, source);
            if (mesh == null)
            {
                var meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter != null) mesh = meshFilter.sharedMesh;
            }

            if (mesh == null)
            {
                BundleLoader.PluginLogger.LogError($"{source} has a MeshCollider with no mesh of its own, and nothing to borrow one from");
                return;
            }

            var collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = PrefabJson.Bool(component, "convex", false);

            bool isTrigger = PrefabJson.Bool(component, "isTrigger", false);
            if (isTrigger && !collider.convex)
            {
                BundleLoader.PluginLogger.LogWarning($"{source} has a MeshCollider asking to be a trigger, which Unity only allows on a convex one");
                return;
            }

            collider.isTrigger = isTrigger;
        }

        static void AddBoxCollider(GameObject gameObject, dynamic component)
        {
            var collider = gameObject.AddComponent<BoxCollider>();
            collider.center = PrefabJson.ReadVector3(component, "center", Vector3.zero);
            collider.size = PrefabJson.ReadVector3(component, "size", Vector3.one);
            collider.isTrigger = PrefabJson.Bool(component, "isTrigger", false);
        }

        static Mesh LoadMesh(dynamic component, string assetFolder, string source)
        {
            string meshFile = PrefabJson.Text(component, "mesh");
            if (meshFile == null) return null;

            string meshPath = ModFilePath.Resolve(Path.Combine(assetFolder, meshFile));
            if (meshPath == null)
            {
                BundleLoader.PluginLogger.LogError($"{source} failed to load the mesh {meshFile}, it doesn't exist");
                return null;
            }

            return ObjMeshLoader.Load(meshPath, Path.GetFileNameWithoutExtension(meshFile));
        }

        static void AddDefaultColliders(GameObject prefab)
        {
            foreach (var meshFilter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh == null || meshFilter.GetComponent<Collider>() != null) continue;

                meshFilter.gameObject.AddComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;
            }
        }

        /// Tags are an index into the game's own tag list, so setting one it doesn't have throws rather than fails
        static void SetTag(GameObject gameObject, string tag, string source)
        {
            if (tag == null) return;

            try
            {
                gameObject.tag = tag;
            }
            catch
            {
                BundleLoader.PluginLogger.LogError($"{source} asks for the tag '{tag}', which the game doesn't have");
            }
        }

        static void SetLayer(GameObject gameObject, dynamic json, string source)
        {
            var layer = PrefabJson.Field(json, "layer");
            if (layer == null) return;

            if (JsonWrapper.NewtonsoftJson.JToken_Type(layer) == JsonWrapper.e_JToken_Type.Integer)
            {
                gameObject.layer = json.Value<int>("layer");
                return;
            }

            string layerName = layer.ToString();
            int named = LayerMask.NameToLayer(layerName);

            if (named < 0)
            {
                BundleLoader.PluginLogger.LogError($"{source} asks for the layer '{layerName}', which the game doesn't have");
                return;
            }

            gameObject.layer = named;
        }
        
        public static Transform PrefabContainer()
        {
            if (prefabContainer == null)
            {
                prefabContainer = new GameObject("AssetBundleLoader_RuntimePrefabs");
                UnityEngine.Object.DontDestroyOnLoad(prefabContainer);
                prefabContainer.SetActive(false);
            }

            return prefabContainer.transform;
        }
    }
}
