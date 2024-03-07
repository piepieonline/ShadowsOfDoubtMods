using UnityEngine;

namespace DialogAdditions
{
    class TalkToPartner : CustomDialogPreset
    {
        public TalkToPartner()
        {
            Name = "TalkToPartner";

            var preset = ScriptableObject.CreateInstance<DialogPreset>();

            preset.name = Name;
            preset.msgID = "cfa7463c-a03d-4c2c-87e9-124a39290716";
            preset.defaultOption = true;
            preset.tiedToKey = Evidence.DataKey.photo;
            preset.useSuccessTest = true;
            preset.baseChance = 1;
            preset.ranking = 1;

            preset.responses.Add(new AIActionPreset.AISpeechPreset()
            {
                dictionaryString = "Success",
                ddsMessageID = "1435a1d4-465f-49df-98c5-12ad5515ff8d",
                isSuccessful = true,
                chance = 1,
                useParsing = true,
                endsDialog = true,
                shout = true
            });

            preset.responses.Add(new AIActionPreset.AISpeechPreset()
            {
                dictionaryString = "Fail",
                ddsMessageID = "b7d97901-3d58-4cdc-afc2-34003de5192b",
                isSuccessful = false,
                chance = 1,
                useParsing = true
            });

            Preset = preset;
        }

        public override bool IsAvailable(DialogPreset preset, Citizen saysTo, SideJob jobRef)
        {
            return (saysTo != null && !saysTo.isHomeless && saysTo.isHome);
        }

        public override void RunDialogMethod(DialogController instance, Citizen saysTo, Interactable saysToInteractable, NewNode where, Actor saidBy, bool success, NewRoom roomRef, SideJob jobRef)
        {
            if (success && saysTo && saysTo.isHome && saysTo.partner && saysTo.partner.isHome)
            {
                saysTo.partner.ai.AnswerDoor(saysTo.home.entrances[0].door, saysTo.currentGameLocation, Player.Instance);
                // TODO: Wait a couple of seconds so the partner is near the door?
                saysTo.ai.currentGoal.Complete();
            }
        }

        public override DialogController.ForceSuccess ShouldDialogSucceedOverride(DialogController instance, EvidenceWitness.DialogOption dialog, Citizen saysTo, NewNode where, Actor saidBy)
        {
            if (!saysTo)
                return DialogController.ForceSuccess.none;

            if (!saysTo.isHome || !saysTo.partner || (saysTo.partner && !saysTo.partner.isHome))
            {
                return DialogController.ForceSuccess.fail;
            }

            return DialogController.ForceSuccess.none;
        }
    }
}
