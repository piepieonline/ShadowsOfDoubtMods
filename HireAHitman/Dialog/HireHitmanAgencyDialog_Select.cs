using UnityEngine;
using DialogAdditions;

namespace HireAHitman.Dialog
{
    internal class HireHitmanAgencyDialog_Select : CustomDialogPreset
    {
        static CustomDialogPreset ResponseDialog;

        public HireHitmanAgencyDialog_Select(CustomDialogPreset responseDialog)
        {
            Name = "HireHitmanAgencyDialog_Select";

            var preset = ScriptableObject.CreateInstance<DialogPreset>();

            preset.presetName = Name;
            preset.name = Name;
            preset.msgID = "";
            preset.defaultOption = false;
            preset.useSuccessTest = true;
            preset.baseChance = 0;
            preset.ranking = 1;
            preset.removeAfterSaying = false;
            preset.telephoneCallOption = true;

            Preset = preset;
            ResponseDialog = responseDialog;
        }

        public override bool IsAvailable(DialogPreset preset, Citizen saysTo, SideJob jobRef)
        {
            return true;
        }

        public override void RunDialogMethod(DialogController instance, Citizen saysTo, Interactable saysToInteractable, NewNode where, Actor saidBy, bool success, NewRoom roomRef, SideJob jobRef)
        {
            PhotoSelectButtonController_OnLeftClick.callType = Name;
            SessionData.Instance.PauseGame(true);
            instance.askTarget = null;
            instance.askTargetKeys.Clear();
            instance.askWindow = InterfaceController.Instance.SpawnWindow(null, presetName: "SelectPhoto", worldInteraction: true, autoPosition: false, forcePosition: InterfaceControls.Instance.handbookWindowPosition, passDialogSuccess: success);
        }

        public override DialogController.ForceSuccess ShouldDialogSucceedOverride(DialogController instance, EvidenceWitness.DialogOption dialog, Citizen saysTo, NewNode where, Actor saidBy)
        {
            return DialogController.ForceSuccess.success;
        }

        public static bool OnPhotoSelected(Human speaker, Human askTarget, Il2CppSystem.Collections.Generic.List<Evidence.DataKey> askTargetKeys)
        {
            if (HireAHitmanPlugin.DebugMode.Value)
            {
                HireAHitmanPlugin.PluginLogger.LogInfo($"Asking about: {askTarget.citizenName}");
                HireAHitmanPlugin.PluginLogger.LogInfo($"\tKnows name: {askTargetKeys.Contains(Evidence.DataKey.name)}");
                HireAHitmanPlugin.PluginLogger.LogInfo($"\tKnows address: {askTargetKeys.Contains(Evidence.DataKey.address)}");
                HireAHitmanPlugin.PluginLogger.LogInfo($"\tKnows photo: {askTargetKeys.Contains(Evidence.DataKey.photo)}");
            }

            bool wasTargetIdentified = (askTargetKeys.Contains(Evidence.DataKey.name) && askTargetKeys.Contains(Evidence.DataKey.address));

            var dialog = new EvidenceWitness.DialogOption()
            {
                preset = ResponseDialog.Preset
            };

            if (wasTargetIdentified && GameplayController.Instance.money >= HireAHitmanPlugin.CostToHire.Value && HireAHitmanHooks.AddHitmanTarget(askTarget))
            {
                GameplayController.Instance.AddMoney(-HireAHitmanPlugin.CostToHire.Value, true, "Hired a hitman");
                HireAHitmanCustomDDSData.Instance.Target = askTarget;
                DialogController.Instance.ExecuteDialog(dialog, Player.Instance.phoneInteractable, Player.Instance.currentNode, Player.Instance, DialogController.ForceSuccess.success);
            }
            else
            {
                DialogController.Instance.ExecuteDialog(dialog, Player.Instance.phoneInteractable, Player.Instance.currentNode, Player.Instance, DialogController.ForceSuccess.fail);
            }

            return false;
        }
    }
}
