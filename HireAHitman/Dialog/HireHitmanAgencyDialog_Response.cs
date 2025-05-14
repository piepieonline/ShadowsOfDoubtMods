using UnityEngine;
using DialogAdditions;

namespace HireAHitman.Dialog
{
    internal class HireHitmanAgencyDialog_Response : CustomDialogPreset
    {
        public HireHitmanAgencyDialog_Response()
        {
            Name = "HireHitmanAgencyDialog_Response";

            var preset = ScriptableObject.CreateInstance<DialogPreset>();

            preset.presetName = Name;
            preset.name = Name;
            preset.msgID = "";
            preset.defaultOption = false;
            preset.useSuccessTest = true;
            preset.baseChance = 1;
            preset.ranking = 1;
            preset.removeAfterSaying = false;
            preset.telephoneCallOption = true;

            preset.responses.Add(new AIActionPreset.AISpeechPreset()
            {
                dictionaryString = "Success",
                ddsMessageID = "0f928a76-eaa9-4bf2-ac11-0a5fab1ecbfb",
                isSuccessful = true,
                chance = 1,
                useParsing = true,
                endsDialog = true,
            });

            preset.responses.Add(new AIActionPreset.AISpeechPreset()
            {
                dictionaryString = "Fail",
                ddsMessageID = "795afb08-f6e1-43fa-ac76-0c59ddcb25b7",
                isSuccessful = false,
                chance = 1,
                useParsing = true,
                endsDialog = true,
            });
            
            Preset = preset;
        }

        public override bool IsAvailable(DialogPreset preset, Citizen saysTo, SideJob jobRef)
        { return false; }

        public override void RunDialogMethod(DialogController instance, Citizen saysTo, Interactable saysToInteractable, NewNode where, Actor saidBy, bool success, NewRoom roomRef, SideJob jobRef)
        {}

        public override DialogController.ForceSuccess ShouldDialogSucceedOverride(DialogController instance, EvidenceWitness.DialogOption dialog, Citizen saysTo, NewNode where, Actor saidBy)
        {
            return DialogController.ForceSuccess.success;
        }
    }
}
