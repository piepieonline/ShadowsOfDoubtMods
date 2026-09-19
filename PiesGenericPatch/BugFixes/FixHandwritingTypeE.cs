using HarmonyLib;
using TMPro;
using UniverseLib;
using Il2CppType = Il2CppInterop.Runtime.Il2CppType;

namespace Pies_Generic_Patch.BugFixes
{
    /// <summary>
    /// In vanilla, the Halogen HandwritingPreset points at the Denise_Handwriting font instead of the shipped Halogen font
    /// </summary>
    public class FixHandwritingTypeE
    {
        public const string PresetName = "Halogen";
        public const string FontName = "Halogen SDF";

        public static void DoPatch(Harmony harmony)
        {
            harmony.PatchAll(typeof(FixHandwritingTypeE.Toolbox_Start));
        }

        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Start))]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                RepointHalogenFont();
            }
        }

        public static void RepointHalogenFont()
        {
            var handwritingType = Il2CppType.Of<HandwritingPreset>();

            if (!Toolbox.Instance.resourcesCache.ContainsKey(handwritingType) || !Toolbox.Instance.resourcesCache[handwritingType].ContainsKey(PresetName))
            {
                Pies_Generic_PatchPlugin.Log.LogWarning($"HandwritingPreset {PresetName} not found, handwriting fix skipped");
                return;
            }

            var preset = Toolbox.Instance.resourcesCache[handwritingType][PresetName].TryCast<HandwritingPreset>();

            if (preset == null || preset.fontAsset == null)
            {
                Pies_Generic_PatchPlugin.Log.LogWarning($"HandwritingPreset {PresetName} has no font, handwriting fix skipped");
                return;
            }

            // No-op if the data is fixed upstream
            if (preset.fontAsset.name == FontName)
                return;

            if (DDSControls.Instance == null)
            {
                Pies_Generic_PatchPlugin.Log.LogWarning("DDSControls not ready, handwriting fix skipped");
                return;
            }

            TMP_FontAsset halogenFont = null;

            foreach (var font in DDSControls.Instance.fonts)
            {
                if (font != null && font.name == FontName)
                {
                    halogenFont = font;
                    break;
                }
            }

            if (halogenFont == null)
            {
                Pies_Generic_PatchPlugin.Log.LogWarning($"Font {FontName} not in DDSControls.fonts, handwriting fix skipped");
                return;
            }

            var previousFontName = preset.fontAsset.name;
            preset.fontAsset = halogenFont;

            // Rich text <font="Halogen SDF"> tags (signatures, address book) resolve through here, and nothing in vanilla has ever assigned this font to a text component
            MaterialReferenceManager.AddFontAsset(halogenFont);

            if (Pies_Generic_PatchPlugin.DebugLogging.Value)
                Pies_Generic_PatchPlugin.Log.LogInfo($"HandwritingPreset {PresetName}: font changed from {previousFontName} to {halogenFont.name}");
        }
    }
}
