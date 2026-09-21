using HarmonyLib;

namespace Pies_Generic_Patch.BugFixes
{
    /// <summary>
    /// Stops shopkeepers demanding a password at addresses that don't have one
    ///
    /// BuySomething/OpeningHours have baseChance 1.0 and "Password needed" as their only fail response, so the
    /// password gate is meant to be the only way to fail. In vanilla, a negative dialogChanceModifier sync disk
    /// side effect or an active side job with alwaysFail on the citizen can fail the dialog anyway.
    /// </summary>
    public class FixDialogPasswordPrompt
    {
        public static void DoPatch(Harmony harmony)
        {
            harmony.PatchAll(typeof(FixDialogPasswordPrompt.DialogController_ExecuteDialog));
        }

        [HarmonyPatch(typeof(DialogController), nameof(DialogController.ExecuteDialog))]
        public class DialogController_ExecuteDialog
        {
            public static void Prefix(EvidenceWitness.DialogOption dialog, Interactable saysTo, ref DialogController.ForceSuccess forceSuccess)
            {
                if (forceSuccess != DialogController.ForceSuccess.none || dialog == null || dialog.preset == null || saysTo == null)
                    return;

                var preset = dialog.preset;

                if (!preset.useSuccessTest || !preset.requiresPassword || preset.baseChance < 1f)
                    return;

                if (PasswordRequiredAndUnknown(saysTo))
                    return;

                forceSuccess = DialogController.ForceSuccess.success;

                if (Pies_Generic_PatchPlugin.DebugLogging.Value)
                    Pies_Generic_PatchPlugin.Log.LogInfo($"Dialog {preset.name}: forcing success as no password is required");
            }

            private static bool PasswordRequiredAndUnknown(Interactable saysTo)
            {
                var address = saysTo.isActor?.currentGameLocation?.thisAsAddress;

                if (address == null || address.addressPreset == null || !address.addressPreset.needsPassword)
                    return false;

                return !GameplayController.Instance.playerKnowsPasswords.Contains(address.id);
            }
        }
    }
}
