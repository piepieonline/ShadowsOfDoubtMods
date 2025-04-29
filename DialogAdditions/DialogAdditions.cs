using System.IO;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using Il2CppSystem.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UniverseLib;
using UnityEngine;
using DialogAdditions.NewDialogOptions;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

/*
 * TODO:
 *  - A good interface for external mods to implement
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
        public static List<CustomDialogPreset> LoadedCustomDialogPresets = new List<CustomDialogPreset>();

        public static Dictionary<string, DialogPreset> dialogPresetRefs = new Dictionary<string, DialogPreset>();
        public static Dictionary<string, CustomDialogPreset> customDialogInterceptors = new Dictionary<string, CustomDialogPreset>();
        public static ManualLogSource PluginLogger;

        public static ConfigEntry<bool> TalkToPartnerCanFail;
        public static ConfigEntry<float> TalkToPartnerBaseSuccess;
        public static ConfigEntry<float> SeenUnusualLikeBlock;
        public static ConfigEntry<bool> ConfirmSuspiciousPhotos;
        public static ConfigEntry<bool> AddAskForPasscode;

        public static MethodInfo WarnNotewriterMI;

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
            ConfirmSuspiciousPhotos = Config.Bind("Do you know this person?", "If the NPC has seen something someone suspicous, and you present a photo, will they confirm it?", true);
            AddAskForPasscode = Config.Bind("What is your passcode?", "Should asking for passcodes be enabled?", true, new ConfigDescription("Restart Required"));

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

            AssetBundleLoader.BundleLoader.loadObjectDelegates.Add(LoadObjectsCallback);
        }

        public List<ScriptableObject> LoadObjectsCallback(Il2CppSystem.Collections.Generic.List<ScriptableObject> loadedScriptableObjects)
        {
            var objectsToLoad = new List<ScriptableObject>();

            foreach (var so in loadedScriptableObjects)
            {
                if(so.GetActualType() == typeof(DialogPreset))
                {
                    DialogPreset dialogPreset = so.TryCast<DialogPreset>();
                    dialogPresetRefs[dialogPreset.name] = dialogPreset;
                }
            }

            var talkToPartnerDialog = new TalkToPartner(TalkToPartnerCanFail.Value, TalkToPartnerBaseSuccess.Value);
            var seenThisPersonWithOthersDialog = new SeenThisPersonWithOthers();
            LoadedCustomDialogPresets.Add(talkToPartnerDialog);
            LoadedCustomDialogPresets.Add(seenThisPersonWithOthersDialog);

            // Add custom dialog that needs code execution
            foreach (var customDialog in LoadedCustomDialogPresets)
            {
                AddDialogInterceptor(customDialog);
                objectsToLoad.Add(customDialog.Preset);
            }

            // Account for both Thunderstore install and manual install
            string basePath = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location).FullName;
            if (Directory.Exists(Path.Combine(basePath, "plugins")))
            {
                basePath = Path.Combine(basePath, "plugins");
            }

            var dialogFilesToLoad = new List<string>();

            if(AddAskForPasscode.Value)
            {
                dialogFilesToLoad.Add("WhatIsYourPasscode/WhatIsYourPasscodeTemplate");
                dialogFilesToLoad.Add("WhatIsYourPasscode/WhatIsYourPasscodeBribe2");
                dialogFilesToLoad.Add("WhatIsYourPasscode/WhatIsYourPasscodeBribe1");
                dialogFilesToLoad.Add("WhatIsYourPasscode/WhatIsYourPasscode");
            }

            // Add custom dialog that doesn't need code execution
            foreach (var dialogPresetFileName in dialogFilesToLoad)
            {
                string filePath = Path.Combine(basePath, dialogPresetFileName + ".sodso.json");
                if (File.Exists(filePath))
                {
                    var fileContent = File.ReadAllText(filePath);

                    DialogPreset loadedPreset = AssetBundleLoader.JsonLoader.LoadFileToGame(fileContent).TryCast<DialogPreset>();

                    objectsToLoad.Add(loadedPreset);
                }
                else
                {
                    DialogAdditionPlugin.PluginLogger.LogError($"File {dialogPresetFileName} not found, looking {filePath}");
                }
            }

            return objectsToLoad;
        }

        public static void AddDialogInterceptor(CustomDialogPreset customDialogPreset)
        {
            if(!customDialogInterceptors.ContainsKey(customDialogPreset.Name))
            {
                customDialogInterceptors[customDialogPreset.Name] = customDialogPreset;
            }

            // Short circuit, we'll add them once the WarnNotewriter dialog is loaded and cached
            if (WarnNotewriterMI == null)
                return;

            if (!DialogController.Instance.dialogRef.ContainsKey(customDialogPreset.Preset))
            {
                DialogController.Instance.dialogRef.Add(customDialogPreset.Preset, WarnNotewriterMI);
            }
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

    [HarmonyPatch(typeof(DialogController), nameof(DialogController.Start))]
    public class DialogController_Start
    {
        [HarmonyPostfix]
        static void Postfix(DialogController __instance)
        {
            foreach (var dialogKV in __instance.dialogRef)
            {
                if (dialogKV.Key.name == "WarnNotewriter")
                {
                    DialogAdditionPlugin.WarnNotewriterMI = dialogKV.Value;
                    break;
                }
            }
            
            foreach (var customDialogKV in DialogAdditionPlugin.customDialogInterceptors)
            {
                DialogAdditionPlugin.AddDialogInterceptor(customDialogKV.Value);
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
            if (DialogAdditionPlugin.customDialogInterceptors.ContainsKey(__instance.preset.name) && DialogAdditionPlugin.customDialogInterceptors[__instance.preset.name] != null)
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
            if (DialogAdditionPlugin.customDialogInterceptors.ContainsKey(preset.name) && DialogAdditionPlugin.customDialogInterceptors[preset.name] != null)
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
            if (forceSuccess == DialogController.ForceSuccess.none && DialogAdditionPlugin.customDialogInterceptors.ContainsKey(dialog.preset.name) && DialogAdditionPlugin.customDialogInterceptors[dialog.preset.name] != null)
            {
                Citizen saysToCit = null;
                try
                {
                    // No saysTo on the phone, for eg
                    saysToCit = ((dynamic)saysTo.isActor).Cast<Citizen>();
                }
                catch
                { }

                forceSuccess = DialogAdditionPlugin.customDialogInterceptors[dialog.preset.name].ShouldDialogSucceedOverride(__instance, dialog, saysToCit, where, saidBy);
            }
        }
    }
}