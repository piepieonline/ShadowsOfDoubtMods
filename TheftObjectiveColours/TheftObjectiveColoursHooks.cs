using DDSScriptExtensions;
using HarmonyLib;
using UnityEngine;
using UniverseLib;

namespace TheftObjectiveColours;

public class TheftObjectiveColoursHooks
{
    [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Start))]
        public class Toolbox_Start
        {
            private static DDSScriptHelper scriptHelper;
            public static void Postfix()
            {
                RebuildTextures();

                scriptHelper = new DDSScriptHelper();
                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["TheftObjectiveColours"] = MoonSharp.Interpreter.UserData.Create(scriptHelper.Instance);
            }

            public static void RebuildTextures()
            {
                // Texture2D baseMaterial = __instance.spawnedObject.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.TryCast<Texture2D>();
                Texture2D baseMaterial = AssetBundleLoader.Texture2DLoader.CreateTexture2DFromPNG(System.Reflection.Assembly.GetExecutingAssembly(), "SealedEnvelope_diffuse.png");
                baseMaterial.filterMode = FilterMode.Point;

                TheftObjectiveColoursPlugin.OverlayTexture = AssetBundleLoader.Texture2DLoader.CreateTexture2DFromPNG(System.Reflection.Assembly.GetExecutingAssembly(), "SealedEnvelope_diffuse_overlay.png");
                TheftObjectiveColoursPlugin.OverlayTexture.filterMode = FilterMode.Point;

                for (int i = 0; i < TheftObjectiveColoursPlugin.Colours.Length; i++)
                {
                    TheftObjectiveColoursPlugin.CachedTextures[i] = AssetBundleLoader.Texture2DLoader.CombineTexture2D(baseMaterial, TheftObjectiveColoursPlugin.OverlayTexture, TheftObjectiveColoursPlugin.Colours[i]);
                    TheftObjectiveColoursPlugin.CachedTextures[i].filterMode = FilterMode.Point;
                }
            }

            public class DDSScriptHelper
            {
                public readonly DDSScriptHelper Instance;

                public DDSScriptHelper()
                {
                    Instance = this;
                }

                public static string GetEnvelopeColour(object inputObject)
                {
                    Interactable interactable = null;
                    if(inputObject.GetActualType() == typeof(SideJob))
                    {
                        interactable = inputObject.TryCast<SideJob>().activeJobItems[JobPreset.JobTag.A];
                    }

                    var seed = interactable.seed;
                    int cacheIndex = Toolbox.Instance.RandContained(0, TheftObjectiveColoursPlugin.CachedTextures.Length, ref seed);
                    return TheftObjectiveColoursPlugin.ColourNames[cacheIndex];
                }
            }
            
        }

        [HarmonyPatch(typeof(Interactable), nameof(Interactable.OnSpawn))]
        public class Interactable_OnSpawn
        {
            public static void Postfix(Interactable __instance)
            {
                if (__instance.preset.presetName == "SealedEnvelope")
                {
                    var seed = __instance.seed;
                    int cacheIndex = Toolbox.Instance.RandContained(0, TheftObjectiveColoursPlugin.CachedTextures.Length, ref seed);
                    if (TheftObjectiveColoursPlugin.CachedTextures[cacheIndex] == null)
                    {
                        Toolbox_Start.RebuildTextures();
                    }

                    __instance.spawnedObject.GetComponent<MeshRenderer>().material.mainTexture = TheftObjectiveColoursPlugin.CachedTextures[cacheIndex];
                }
            }
        }

        [HarmonyPatch(typeof(FirstPersonItemController), nameof(FirstPersonItemController.RefreshHeldObjects))]
        public class FirstPersonItemController_RefreshHeldObjects
        {
            public static void Postfix(FirstPersonItemController __instance)
            {
                var interactable = BioScreenController.Instance.selectedSlot?.GetInteractable();
                if (interactable != null && interactable.preset.presetName == "SealedEnvelope")
                {
                    var seed = interactable.seed;
                    int cacheIndex = Toolbox.Instance.RandContained(0, TheftObjectiveColoursPlugin.CachedTextures.Length, ref seed);
                    if (TheftObjectiveColoursPlugin.CachedTextures[cacheIndex] == null)
                    {
                        Toolbox_Start.RebuildTextures();
                    }

                    __instance.rightPrefabReference.GetComponent<MeshRenderer>().material.mainTexture = TheftObjectiveColoursPlugin.CachedTextures[cacheIndex];
                }
            }
        }
}