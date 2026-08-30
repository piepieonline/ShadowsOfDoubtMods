using System.IO;

using UnityEngine;
using UniverseLib;

namespace AssetBundleLoader
{
    public static class MaterialLoader
    {
        public static Material LoadFromPrefabBlock(dynamic materialJson, string assetFolder, string source)
        {
            var material = StartingMaterial(materialJson, source);
            if (material == null) return null;

            material.hideFlags = HideFlags.DontUnloadUnusedAsset;

            string materialName = PrefabJson.Text(materialJson, "name");
            if (materialName != null) material.name = materialName;

            // Copying a material brings the donor's own maps with it, which would otherwise sit under ours
            foreach (var property in PrefabJson.Array(materialJson, "clearTextures"))
            {
                SetTexture(material, property.ToString(), null, source);
            }

            var textures = PrefabJson.Field(materialJson, "textures");
            foreach (var property in PrefabJson.Names(materialJson, "textures"))
            {
                string textureFile = textures.Value<string>(property);
                string texturePath = ModFilePath.Resolve(Path.Combine(assetFolder, textureFile));
                if (texturePath == null)
                {
                    BundleLoader.PluginLogger.LogError($"{source} failed to load the texture {textureFile}, it doesn't exist");
                    continue;
                }

                var texture = Texture2DLoader.CreateTexture2DFromFile(texturePath, Path.GetFileNameWithoutExtension(texturePath));
                SetTexture(material, property, texture, source);
            }

            var colors = PrefabJson.Field(materialJson, "colors");
            foreach (var property in PrefabJson.Names(materialJson, "colors"))
            {
                if (!HasProperty(material, property, source)) continue;

                material.SetColor(property, PrefabJson.ReadColor(PrefabJson.Field(colors, property), Color.white));
            }

            var floats = PrefabJson.Field(materialJson, "floats");
            foreach (var property in PrefabJson.Names(materialJson, "floats"))
            {
                if (!HasProperty(material, property, source)) continue;

                material.SetFloat(property, floats.Value<float>(property));
            }

            return material;
        }

        static Material StartingMaterial(dynamic materialJson, string source)
        {
            string copyFrom = PrefabJson.Text(materialJson, "copyFrom");
            if (copyFrom != null)
            {
                var donor = DonorMaterial(copyFrom, source);
                return donor == null ? null : new Material(donor);
            }

            string shaderName = PrefabJson.Text(materialJson, "shader");
            if (shaderName != null)
            {
                var shader = BuildingModelLoader.FindGameShader(shaderName);
                if (shader == null)
                {
                    BundleLoader.PluginLogger.LogError($"{source} failed to load, the shader '{shaderName}' isn't in the game");
                    return null;
                }

                return new Material(shader);
            }

            BundleLoader.PluginLogger.LogError($"{source} has a material with neither 'copyFrom' nor 'shader', so it can't be built");
            return null;
        }
        
        static Material DonorMaterial(string reference, string source)
        {
            if (reference.StartsWith("VANILLA:"))
            {
                var asset = VanillaAssetLoader.Resolve(reference.Replace("VANILLA:", "").Trim(), source);
                var material = asset == null ? null : asset.TryCast<Material>();
                if (material == null && asset != null)
                {
                    BundleLoader.PluginLogger.LogError($"{source} failed to load, the material it copies from ({reference}) is a {asset.GetActualType().Name}, not a Material");
                }

                return material;
            }

            string key = reference.Replace("REF:", "").Trim();

            if (!JsonLoader.ScriptableObjectIDMap.TryGetValue(key, out var entry))
            {
                BundleLoader.PluginLogger.LogError($"{source} failed to load, the material it copies from ({key}) doesn't exist!");
                return null;
            }

            var materialGroup = entry.scriptableObject.TryCast<MaterialGroupPreset>();
            if (materialGroup != null && materialGroup.material != null)
            {
                return materialGroup.material;
            }

            var buildingPreset = entry.scriptableObject.TryCast<BuildingPreset>();
            if (buildingPreset != null && buildingPreset.prefab != null)
            {
                foreach (var renderer in buildingPreset.prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // These are a static copy of another model, and share its material anyway
                    if (renderer.gameObject.name.Contains("_static_")) continue;

                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader != null) return material;
                    }
                }
            }

            BundleLoader.PluginLogger.LogError($"{source} failed to load, {key} has no material to copy (copy from a MaterialGroupPreset, or a BuildingPreset with a model)");
            return null;
        }

        static void SetTexture(Material material, string property, Texture2D texture, string source)
        {
            if (!HasProperty(material, property, source)) return;

            material.SetTexture(property, texture);
        }

        static bool HasProperty(Material material, string property, string source)
        {
            if (material.HasProperty(property)) return true;

            BundleLoader.PluginLogger.LogWarning($"{source} sets '{property}', which the shader {material.shader.name} doesn't have");
            return false;
        }
    }
}
