using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DialogAdditions.NewDialogOptions
{
    internal class SeenThisPersonWithOthers : CustomDialogPreset
    {
        public SeenThisPersonWithOthers(bool canFail = false, float chanceToSucceed = 1f)
        {
            Name = "SeenThisPersonWithOthers";

            var preset = ScriptableObject.CreateInstance<DialogPreset>();

            preset.name = Name;
            preset.msgID = "a8267eeb-1d40-442f-9edb-1176bfd51986";
            preset.defaultOption = true;
            preset.tiedToKey = Evidence.DataKey.voice;
            preset.useSuccessTest = true;
            preset.baseChance = canFail ? chanceToSucceed : 1;
            preset.ranking = 6;
            preset.removeAfterSaying = false;
            preset.affectChanceIfRestrained = -1;

            // If it's not a 100% success, influence it with traits.
            if (canFail)
            {
                preset.modifySuccessChanceTraits = DialogAdditionPlugin.dialogPresetRefs["DoYouKnowThisPerson"].modifySuccessChanceTraits;
            }

            preset.responses.Add(new AIActionPreset.AISpeechPreset()
            {
                dictionaryString = "Fail",
                ddsMessageID = "f897a39e-975b-464a-ad9d-0674d57f4bf8",
                isSuccessful = false,
                chance = 1,
                useParsing = true
            });

            Preset = preset;
        }

        public override bool IsAvailable(DialogPreset preset, Citizen saysTo, SideJob jobRef)
        {
            return (saysTo.isAtWork);
        }

        public override void RunDialogMethod(DialogController instance, Citizen saysTo, Interactable saysToInteractable, NewNode where, Actor saidBy, bool success, NewRoom roomRef, SideJob jobRef)
        {
            if (success)
            {
                PhotoSelectButtonController_OnLeftClick.callType = "SeenThisPersonWithOthers";
                SessionData.Instance.PauseGame(true);
                instance.askTarget = null;
                instance.askTargetKeys.Clear();
                instance.askWindow = InterfaceController.Instance.SpawnWindow(null, presetName: "SelectPhoto", worldInteraction: true, autoPosition: false, forcePosition: InterfaceControls.Instance.handbookWindowPosition, passDialogSuccess: success);
            }
        }

        public override DialogController.ForceSuccess ShouldDialogSucceedOverride(DialogController instance, EvidenceWitness.DialogOption dialog, Citizen saysTo, NewNode where, Actor saidBy)
        {
            return DialogController.ForceSuccess.none;
        }
    }
}
