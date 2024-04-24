using UnityEngine;

namespace DialogAdditions
{
    class TalkToPartner : CustomDialogPreset
    {
        public TalkToPartner(bool canFail, float chanceToSucceed)
        {
            Name = "TalkToPartner";

            var preset = ScriptableObject.CreateInstance<DialogPreset>();

            preset.name = Name;
            preset.msgID = "cfa7463c-a03d-4c2c-87e9-124a39290716";
            preset.defaultOption = true;
            preset.tiedToKey = Evidence.DataKey.photo;
            preset.useSuccessTest = true;
            preset.baseChance = canFail ? chanceToSucceed : 1;
            preset.ranking = 1;
            preset.removeAfterSaying = false;
            preset.affectChanceIfRestrained = -1;

            // If it's not a 100% success, influence it with traits.
            if(canFail)
            {
                preset.modifySuccessChanceTraits = DialogAdditionPlugin.dialogPresets["Introduce"].modifySuccessChanceTraits;
            }

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

            /*
            // Disabled, use specific responses below
            preset.responses.Add(new AIActionPreset.AISpeechPreset()
            {
                dictionaryString = "Fail",
                ddsMessageID = "b7d97901-3d58-4cdc-afc2-34003de5192b",
                isSuccessful = false,
                chance = 1,
                useParsing = true
            });
            */

            Preset = preset;
        }

        public override bool IsAvailable(DialogPreset preset, Citizen saysTo, SideJob jobRef)
        {
            return (!saysTo.isHomeless && saysTo.isHome);
        }

        public override void RunDialogMethod(DialogController instance, Citizen saysTo, Interactable saysToInteractable, NewNode where, Actor saidBy, bool success, NewRoom roomRef, SideJob jobRef)
        {
            if (success && saysTo && saysTo.isHome && saysTo.partner && saysTo.partner.isHome)
            {
                saysTo.partner.ai.AnswerDoor(saysTo.home.entrances[0].door, saysTo.currentGameLocation, Player.Instance);
                // TODO: Wait a couple of seconds so the partner is near the door?
                saysTo.ai.currentGoal.Complete();
            }
            else
            {
                if(!saysTo.partner)
                {
                    // No partner
                    saysTo.speechController.Speak("4f7266c4-a631-482b-8798-074428d66a55");
                }
                else if (!saysTo.partner.isHome)
                {
                    // Partner not home
                    saysTo.speechController.Speak("9f436995-fd25-4116-8eab-68509be5721b");
                }
                else
                {
                    // Generic failure
                    saysTo.speechController.Speak("b7d97901-3d58-4cdc-afc2-34003de5192b");
                }
            }
        }

        public override DialogController.ForceSuccess ShouldDialogSucceedOverride(DialogController instance, EvidenceWitness.DialogOption dialog, Citizen saysTo, NewNode where, Actor saidBy)
        {
            if (!saysTo.isHome || !saysTo.partner || (saysTo.partner && !saysTo.partner.isHome))
            {
                return DialogController.ForceSuccess.fail;
            }

            return DialogController.ForceSuccess.none;
        }
    }
}
