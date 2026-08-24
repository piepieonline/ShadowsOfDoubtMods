using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using UnityEngine;
using UniverseLib;

namespace AssetBundleLoader
{
    public static class BuildingModelLoader
    {
        const int buildingModelLayer = 27;

        static readonly string[] buildingTags = { "BuildingModel", "BuildingModelLights", "BuildingSnowCollider" };

        static Dictionary<string, Shader> gameShaders = new Dictionary<string, Shader>();

        /// A prefab built in Unity arrives with its own copy of the game's shaders, and with the tags of the project
        /// it was built in, so give the game the tags and layers it looks for when it puts a building together.
        /// Only models loaded from a bundle are touched, so patching a building the game shipped leaves it alone.
        public static void TryPrepareBuildingModel(ScriptableObject scriptableObject)
        {
            var preset = scriptableObject.TryCast<BuildingPreset>();
            if (preset == null || !BundleAssetLoader.IsBundleAsset(preset.prefab)) return;

            preset.hideFlags = HideFlags.DontUnloadUnusedAsset;
            preset.prefab.hideFlags = HideFlags.DontUnloadUnusedAsset;

            if (!HasBuildingTag(preset.prefab))
            {
                preset.prefab.tag = "BuildingModel";
            }

            foreach (var renderer in preset.prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var model = renderer.gameObject;

                // The game tags these itself, by matching them back to the model they are a static copy of
                if (model.name.Contains("_static_")) continue;

                // Lit models are the ones the game hands the emission map to, so windows stay lit without this
                if (!HasBuildingTag(model))
                {
                    model.tag = "BuildingModelLights";
                }

                if (model.layer == 0)
                {
                    model.layer = buildingModelLayer;
                }

                foreach (var material in renderer.sharedMaterials)
                {
                    UseGameShader(material);
                }
            }
        }

        /// <summary>
        /// Tags are stored as an index into the tag list of the project the prefab was built in, so a bundle can
        /// arrive holding tags that mean something else entirely here, or nothing at all
        /// </summary>
        static bool HasBuildingTag(GameObject model)
        {
            try
            {
                return buildingTags.Contains(model.tag);
            }
            catch
            {
                return false;
            }
        }

        static void UseGameShader(Material material)
        {
            if (material == null || material.shader == null) return;

            var gameShader = FindGameShader(material.shader.name);
            if (gameShader == null || gameShader.GetInstanceID() == material.shader.GetInstanceID()) return;

            // Buildings render on their own queue, which assigning a shader resets
            int renderQueue = material.renderQueue;
            material.shader = gameShader;
            if (renderQueue >= 0) material.renderQueue = renderQueue;
        }

        /// Shader.Find can return the bundle's own copy, so take the shader from a building the game shipped
        static Shader FindGameShader(string shaderName)
        {
            if (gameShaders.ContainsKey(shaderName)) return gameShaders[shaderName];

            gameShaders[shaderName] = GameBuildingShaders().FirstOrDefault(shader => shader.name == shaderName) ?? Shader.Find(shaderName);

            if (gameShaders[shaderName] == null)
            {
                BundleLoader.PluginLogger.LogWarning($"No copy of the shader '{shaderName}' could be found in the game, the bundle's own will be used");
            }

            return gameShaders[shaderName];
        }

        // Cables are sized from the world height of the shaft's top tile, which only matches the shaft when it runs the
        // full height of the building. A shaft covering just the upper floors gets a cable that hangs out of the model.
        // TODO: Doesn't completely work, but it's a good start. (Overlaps by 1 when down, completely when up)
        [HarmonyPatch(typeof(Elevator), nameof(Elevator.UpdateCables))]
        public class Elevator_UpdateCables
        {
            public static void Postfix(Elevator __instance)
            {
                if (__instance.top == null || __instance.bottom == null || __instance.cable1 == null) return;

                // Only ever shorten, so a shaft that does run the building's full height keeps the size the game gave it
                float shaftHeight = __instance.top.position.y - __instance.bottom.position.y + 0.5f;
                if (shaftHeight >= __instance.cable1.localScale.y) return;

                __instance.cable1.localScale = new Vector3(1f, shaftHeight, 1f);
                __instance.cable2.localScale = new Vector3(1f, shaftHeight, 1f);
            }
        }

        static IEnumerable<Shader> GameBuildingShaders()
        {
            foreach (var entry in JsonLoader.ScriptableObjectIDMap)
            {
                if (!entry.Key.StartsWith("BuildingPreset|")) continue;

                var preset = entry.Value.scriptableObject.TryCast<BuildingPreset>();
                if (preset == null || preset.prefab == null) continue;

                foreach (var renderer in preset.prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader != null) yield return material.shader;
                    }
                }
            }
        }
    }
}
