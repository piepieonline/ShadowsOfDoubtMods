using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using Il2CppSystem.Reflection;
using BepInEx.Configuration;
using Cpp2IL.Core.Api;
using System.Linq;



#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

/*
 * TODO:
 *  - Ask to talk to the partner:
 *      - Don't let someone cycle between people at the door
 *      - Tidy up lines used when rejecting
 *  
 *  - Other dialog additions?
 */

namespace DialogAdditions
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class DialogAdditionPlugin : BaseUnityPlugin
#elif IL2CPP
    public class DialogAdditionPlugin : BasePlugin
#endif
    {
        public static Dictionary<string, DialogPreset> dialogPresets = new Dictionary<string, DialogPreset>();
        public static Dictionary<string, CustomDialogPreset> customDialogInterceptors = new Dictionary<string, CustomDialogPreset>();
        public static ManualLogSource PluginLogger;

        public static ConfigEntry<bool> TalkToPartnerCanFail;
        public static ConfigEntry<float> TalkToPartnerBaseSuccess;
        public static ConfigEntry<float> SeenUnusualLikeBlock;

        private static DialogAdditionPlugin instance;
#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif
            // Plugin startup logic

            instance = this;

            TalkToPartnerCanFail = Config.Bind("Talk to Partner", "Can asking for the partner fail?", false);
            TalkToPartnerBaseSuccess = Config.Bind("Talk to Partner", "Base chance of success (before trait modifications, in the range 0 to 1)?", 0.25f);
            SeenUnusualLikeBlock = Config.Bind("Have you seen anything?", "How well liked does the perp have to be before acquaintances protect them? (in the range 0 to 1)?", 0.45f);

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }
    }

    public abstract class CustomDialogPreset
    {
        public string Name { get; protected set; }
        public DialogPreset Preset { get; protected set; }

        public abstract bool IsAvailable(DialogPreset preset, Citizen saysTo, SideJob jobRef);

        public abstract void RunDialogMethod(DialogController instance, Citizen saysTo, Interactable saysToInteractable, NewNode where, Actor saidBy, bool success, NewRoom roomRef, SideJob jobRef);

        public abstract DialogController.ForceSuccess ShouldDialogSucceedOverride(DialogController instance, EvidenceWitness.DialogOption dialog, Citizen saysTo, NewNode where, Actor saidBy);
    }

    // Patch to add the dialog to the list of available options
    [HarmonyPatch(typeof(DialogController), nameof(DialogController.Start))]
    public class DialogController_Start
    {
        [HarmonyPostfix]
        internal static void Postfix(DialogController __instance)
        {
            MethodInfo warnNotewriterMI = null;
            foreach (var dialogPreset in Toolbox.Instance.allDialog)
            {
                if (dialogPreset.name == "WarnNotewriter")
                {
                    warnNotewriterMI = __instance.dialogRef[dialogPreset];
                }
                DialogAdditionPlugin.dialogPresets[dialogPreset.name] = dialogPreset;
            }

            foreach (var customDialog in new CustomDialogPreset[]
            {
                new TalkToPartner(DialogAdditionPlugin.TalkToPartnerCanFail.Value, DialogAdditionPlugin.TalkToPartnerBaseSuccess.Value),
                new SeenThisPersonWithOthers()
            })
            {
                Toolbox.Instance.allDialog.Add(customDialog.Preset);
                
                if(customDialog.Preset.defaultOption)
                    Toolbox.Instance.defaultDialogOptions.Add(customDialog.Preset);

                __instance.dialogRef.Add(customDialog.Preset, warnNotewriterMI);

                DialogAdditionPlugin.customDialogInterceptors[customDialog.Name] = customDialog;
            }
        }
    }

    // Patch to allow running code when the dialog is selected
    // We can't add extension methods, and the called methods are instance members of DialogController. So always use this one, and just catch the cases of it being called.
    [HarmonyPatch(typeof(DialogController), nameof(DialogController.WarnNotewriter))]
    public class DialogController_WarnNotewriter
    {
        [HarmonyPrefix]
        internal static bool Prefix(DialogController __instance, Citizen saysTo, Interactable saysToInteractable, NewNode where, Actor saidBy, bool success, NewRoom roomRef, SideJob jobRef)
        {
            if (DialogAdditionPlugin.customDialogInterceptors.ContainsKey(__instance.preset.name))
            {
                DialogAdditionPlugin.customDialogInterceptors[__instance.preset.name].RunDialogMethod(__instance, saysTo, saysToInteractable, where, saidBy, success, roomRef, jobRef);
                return false;
            }

            return true;
        }
    }

    // Patch to only show the dialog if it should be visible
    [HarmonyPatch(typeof(DialogController), nameof(DialogController.TestSpecialCaseAvailability))]
    public class DialogController_TestSpecialCaseAvailability
    {
        [HarmonyPrefix]
        internal static bool Prefix(ref bool __result, DialogPreset preset, Citizen saysTo, SideJob jobRef)
        {
            if (DialogAdditionPlugin.customDialogInterceptors.ContainsKey(preset.name))
            {
                if(!saysTo)
                {
                    __result = false;
                }
                else
                {
                    __result = DialogAdditionPlugin.customDialogInterceptors[preset.name].IsAvailable(preset, saysTo, jobRef);
                }
                return false;
            }

            return true;
        }
    }

    // Patch to allow for custom success/failure overrides
    [HarmonyPatch(typeof(DialogController), nameof(DialogController.ExecuteDialog))]
    public class DialogController_ExecuteDialog
    {
        [HarmonyPrefix]
        internal static void Prefix(DialogController __instance, EvidenceWitness.DialogOption dialog, Interactable saysTo, NewNode where, Actor saidBy, ref DialogController.ForceSuccess forceSuccess)
        {
            if (forceSuccess == DialogController.ForceSuccess.none && DialogAdditionPlugin.customDialogInterceptors.ContainsKey(dialog.preset.name))
            {
                Citizen saysToCit = ((dynamic)saysTo.isActor).Cast<Citizen>();
                forceSuccess = DialogAdditionPlugin.customDialogInterceptors[dialog.preset.name].ShouldDialogSucceedOverride(__instance, dialog, saysToCit, where, saidBy);
            }
        }
    }
}