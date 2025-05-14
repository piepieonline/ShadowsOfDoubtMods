using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using DialogAdditions;

namespace HireAHitman.Dialog
{
    internal class HireHitmanAgencyDialog_Welcome : CustomDialogPreset
    {
        static AIActionPreset.AISpeechPreset aiSpeechPreset;

        CustomDialogPreset PhotoSelectDialog;

        public HireHitmanAgencyDialog_Welcome(CustomDialogPreset photoSelectDialog)
        {
            Name = "HireHitmanAgencyDialog_Welcome";

            var preset = ScriptableObject.CreateInstance<DialogPreset>();

            preset.presetName = Name;
            preset.name = Name;
            preset.msgID = "";
            preset.defaultOption = false;
            preset.useSuccessTest = false;
            preset.baseChance = 1;
            preset.ranking = 1;
            preset.removeAfterSaying = false;
            preset.telephoneCallOption = true;
            
            PhotoSelectDialog = photoSelectDialog;
            // preset.followUpDialogSuccess.Add(photoSelectDialog.Preset);
            // preset.followUpDialogFail.Add(photoSelectDialog.Preset);

            aiSpeechPreset = new AIActionPreset.AISpeechPreset()
            {
                dictionaryString = "Success",
                ddsMessageID = "b6233341-7556-4b9d-a454-bf15eceac9e1",
                isSuccessful = true,
                chance = 1,
                useParsing = true,
                endsDialog = false
            };

            DialogController_OnDialogEnd.WatchedPresets.Add(aiSpeechPreset.ddsMessageID, (AIActionPreset.AISpeechPreset dialogPreset, string dialogPresetStr, Interactable saysToInteractable, Actor saidBy, int jobRef) =>
            {
                HireAHitmanPlugin.PluginLogger.LogInfo("Starting photo dialog");
                EvidenceWitness.DialogOption dialog = new EvidenceWitness.DialogOption();
                dialog.preset = photoSelectDialog.Preset;
                Interactable saysTo = saysToInteractable;
                DialogController.Instance.ExecuteDialog(dialog, Player.Instance.phoneInteractable, Player.Instance.currentNode, Player.Instance);
                HireAHitmanPlugin.PluginLogger.LogInfo("Ending photo dialog");
            });

            preset.responses.Add(aiSpeechPreset);

            Preset = preset;
        }

        public override bool IsAvailable(DialogPreset preset, Citizen saysTo, SideJob jobRef)
        { return false; }
        
        public override void RunDialogMethod(DialogController instance, Citizen saysTo, Interactable saysToInteractable, NewNode where, Actor saidBy, bool success, NewRoom roomRef, SideJob jobRef)
        {}

        public override DialogController.ForceSuccess ShouldDialogSucceedOverride(DialogController instance, EvidenceWitness.DialogOption dialog, Citizen saysTo, NewNode where, Actor saidBy)
        { return DialogController.ForceSuccess.success; }

        [HarmonyPatch(typeof(SpeechBubbleController), nameof(SpeechBubbleController.OnDestroy))]
        public class DialogController_OnDialogEnd
        {
            public static Dictionary<string, System.Action<AIActionPreset.AISpeechPreset, string, Interactable, Actor, int>> WatchedPresets = new Dictionary<string, System.Action<AIActionPreset.AISpeechPreset, string, Interactable, Actor, int>>();

            [HarmonyPrefix]
            internal static void Prefix(SpeechBubbleController __instance)
            {
                if (__instance != null && __instance.speech != null && __instance.speech.dialog != null && WatchedPresets.ContainsKey(__instance.speech.dialog.ddsMessageID))
                {
                    WatchedPresets[__instance.speech.dialog.ddsMessageID].Invoke(__instance.speech.dialog, null, null, null, 0);
                }
            }
        }
    }
}
