using System.Collections.Generic;
using HarmonyLib;
using DialogAdditions;
using HireAHitman.Dialog;
using AssetBundleLoader;
using UnityEngine;
using System;
using Cpp2IL.Core.Api;
using BigGustave;
using static MurderController;
using SOD.Common.Extensions;
using System.Linq;

namespace HireAHitman
{
    internal class HireAHitmanHooks
    {
        public static List<int> TargetHumanIds = new List<int>();
        public static int fakePhoneNumber = -1;
        public static MurderMO HitmanMO;

        private static int currentHitmanTargetId = -1;

        public static bool AddHitmanTarget(Human human)
        {
            // Sanity checks
            if (human.isDead)
            {
                HireAHitmanPlugin.DebugLogIfEnabled(BepInEx.Logging.LogLevel.Info, $"Invalid target: {human.citizenName} - Already dead");
                return false;
            }

            if (MurderController.Instance.previousMurderers.Contains(human))
            {
                HireAHitmanPlugin.DebugLogIfEnabled(BepInEx.Logging.LogLevel.Info, $"Invalid target: {human.citizenName} - Is a previous murderer");
                return false;
            }

            if (TargetHumanIds.Contains(human.humanID))
            {
                HireAHitmanPlugin.DebugLogIfEnabled(BepInEx.Logging.LogLevel.Info, $"Invalid target: {human.citizenName} - Already a target");
                return false;
            }

            TargetHumanIds.Add(human.humanID);

            return true;
        }

        public static void VMailBuildingSecurity(object sender, SOD.Common.Helpers.GameplayObjects.VictimKilledArgs e)
        {
            if(e.Murder.mo == HitmanMO && MurderController.Instance.activeMurders.Count == 1)
            {
                Human neighbour = null;
                float score = float.MaxValue;

                foreach (var acquaintance in e.Murder.murderer.acquaintances)
                {
                    if(acquaintance.with.isDead) continue;

                    if(acquaintance.connections.Contains(Acquaintance.ConnectionType.neighbor))
                    {
                        float newScore = acquaintance.known - acquaintance.like;

                        if (newScore < score)
                        {
                            score = newScore;
                            neighbour = acquaintance.with;
                        }
                    }
                }

                if (neighbour == null)
                {
                    HireAHitmanPlugin.DebugLogIfEnabled(BepInEx.Logging.LogLevel.Info, $"Not sending, no neighbour");
                    return;
                }

                var buildingSecurity = CityData.Instance.jobsDirectory.ToList()
                    .FindAll(item => item.employee != null && item.preset.name == "SecurityGuardDesk")
                    .Where(occupation => occupation.employer.address.building.buildingID == e.Murder.murderer.home.building.buildingID)
                    .FirstOrDefault()?.employee;

                if (buildingSecurity == null)
                {
                    HireAHitmanPlugin.DebugLogIfEnabled(BepInEx.Logging.LogLevel.Info, $"Not sending, no guard");
                    return;
                }

                if (HireAHitmanPlugin.DebugMode.Value)
                {
                    HireAHitmanPlugin.DebugLogIfEnabled(BepInEx.Logging.LogLevel.Info, $"First murder, sending vmail from {neighbour.citizenName} to {buildingSecurity.citizenName}");
                }

                Toolbox.Instance.NewVmailThread(neighbour, buildingSecurity, null, null, null, "17f7b4f4-56c6-444b-b1e8-7546fcbe3602", SessionData.Instance.gameTime + Toolbox.Instance.SeedRand(-48f, -12f));
            }
        }

        internal class Patches_MurderController
        {
            // Use a custom murder case type
            [HarmonyPatch(typeof(MurderController), "PickNewMurderer")]
            internal class MurderController_PickNewMurderer
            {
                static List<MurderMO> overriddenDisabledMOs = new List<MurderMO>();
                static bool wasHitmanDisabled = false;

                public static void Prefix()
                {
                    overriddenDisabledMOs.Clear();
                    wasHitmanDisabled = HitmanMO.disabled;

                    // Don't do anything if we don't have a target lined up
                    if (TargetHumanIds.Count == 0) return;

                    // Make sure we use our specific MurderMO
                    HitmanMO.disabled = false;
                    foreach (MurderMO murderMO in Toolbox.Instance.allMurderMOs)
                    {
                        if (!murderMO.disabled && murderMO != HitmanMO)
                        {
                            overriddenDisabledMOs.Add(murderMO);
                            murderMO.disabled = true;
                        }
                    }
                }

                public static void Postfix()
                {
                    foreach (var murderMO in overriddenDisabledMOs)
                    {
                        murderMO.disabled = false;
                    }
                    overriddenDisabledMOs.Clear();
                    HitmanMO.disabled = wasHitmanDisabled;
                }
            }

            [HarmonyPatch(typeof(MurderController), "PickNewVictim")]
            internal class MurderController_PickNewVictim
            {
                public static bool Prefix(MurderController __instance)
                {
                    if (TargetHumanIds.Count > 0)
                    {
                        currentHitmanTargetId = TargetHumanIds[0];
                        __instance.currentVictim = CityData.Instance.citizenDictionary[currentHitmanTargetId];
                        TargetHumanIds.RemoveAt(0);

                        // Make sure the victim exists
                        if (__instance.currentVictim == null ||
                            // And they pass the normal victim criteria (straight from mono)
                            !(!__instance.currentVictim.isPlayer && !__instance.currentVictim.isDead && !(__instance.currentMurderer == __instance.currentVictim) && !__instance.currentVictim.isHomeless && !__instance.previousMurderers.Contains(__instance.currentVictim))
                        )
                        {
                            // Failed to target the specific target... Just let the game pick a target (TODO: Pick the next target on our list)
                            HireAHitmanPlugin.PluginLogger.LogError("Cannot find new valid victim!");
                            return true;
                        }
                        else
                        {
                            if (HireAHitmanPlugin.DebugMode.Value)
                            {
                                HireAHitmanPlugin.PluginLogger.LogInfo("Murder: Chosen " + __instance.currentVictim.GetCitizenName() + " to be new victim...");
                                if (__instance.currentVictim.home != null)
                                    HireAHitmanPlugin.PluginLogger.LogInfo("Murder: Victim " + __instance.currentVictim.GetCitizenName() + " lives at " + __instance.currentVictim.home.name);
                            }
                        }
                        if (SideJobController.Instance != null)
                            SideJobController.Instance.DeadPeopleJobCheck();

                        return false;
                    }

                    return true;
                }
            }

            /*
            // Coverups don't really work for this yet, disable for now
            [HarmonyPatch(typeof(MurderController), "OnVictimKilled")]
            internal class MurderController_OnVictimKilled
            {
                static bool wasForcedCoverup = false;
                public static void Prefix()
                {
                    wasForcedCoverup = Game.Instance.forceCoverUpOffers;
                    Game.Instance.forceCoverUpOffers = true;
                }

                public static void Postfix()
                {
                    Game.Instance.forceCoverUpOffers = wasForcedCoverup;
                }
            }
            */
        }

        internal class Dialog_HireHitman
        {
            [HarmonyPatch(typeof(CityData), "CreateSingletons")]
            internal class CityData_CreateSingletons
            {
                public static void Postfix()
                {
                    // Dialog is complicated, because we are using phones and fake numbers
                    // We need to fire a response first, because that's the first thing the player sees - and if the welcome dialog is photo select, the window opens before the dialog is said
                    // Once that response is complete, we trigger the photo select dialog, which doesn't actually say anything, just presents the window
                    // But the validation of that window comes too late, so we use a third DialogPreset to actually give the player a response

                    // Fake numbers don't get a dialog window, so we can't use a welcome, or send any message before the text has appeared

                    var hireHitmanAgencyResponseDialog = new HireHitmanAgencyDialog_Response();
                    var hireHitmanAgencySelectDialog = new HireHitmanAgencyDialog_Select(hireHitmanAgencyResponseDialog);
                    var hireHitmanAgencyWelcomeDialog = new HireHitmanAgencyDialog_Welcome(hireHitmanAgencySelectDialog);

                    DialogAdditionPlugin.LoadedCustomDialogPresets.Add(hireHitmanAgencyResponseDialog);
                    DialogAdditionPlugin.LoadedCustomDialogPresets.Add(hireHitmanAgencySelectDialog);
                    DialogAdditionPlugin.LoadedCustomDialogPresets.Add(hireHitmanAgencyWelcomeDialog);

                    DialogAdditionPlugin.AddDialogInterceptor(hireHitmanAgencyResponseDialog);
                    DialogAdditionPlugin.AddDialogInterceptor(hireHitmanAgencySelectDialog);
                    DialogAdditionPlugin.AddDialogInterceptor(hireHitmanAgencyWelcomeDialog);

                    PhotoSelectButtonController_OnLeftClick.CallTypes.Add(hireHitmanAgencySelectDialog.Name, HireHitmanAgencyDialog_Select.OnPhotoSelected);

                    var citySeed = CityData.Instance.seed;
                    fakePhoneNumber = Toolbox.Instance.GetPsuedoRandomNumber(7000000, 7999000, ref citySeed);

                    if (citySeed != CityData.Instance.seed)
                    {
                        HireAHitmanPlugin.PluginLogger.LogError($"City seed changed?: {CityData.Instance.seed} != {citySeed}");
                    }
                    
                    if(HireAHitmanPlugin.PublicPhoneNumber.Value)
                    {
                        GameplayController.Instance.AddOrMergePhoneNumberData(fakePhoneNumber, false, null, "hireahitman", false);
                    }
                    
                    TelephoneController.Instance.AddFakeNumber(fakePhoneNumber, new TelephoneController.CallSource(TelephoneController.CallType.fakeOutbound, hireHitmanAgencyWelcomeDialog.Preset));
                }
            }
        }


        [HarmonyPatch(typeof(TextToImageController), nameof(TextToImageController.CaptureTextToImage))]
        internal class TextToImageController_CaptureTextToImage
        {
            public static void Prefix(ref TextToImageController.TextToImageSettings settings)
            {
                if (settings.textSize < 0)
                {
                    float rawTextSize = settings.textSize;
                    float workingIndex = Mathf.Abs(settings.textSize);
                    settings.textSize = Mathf.Abs(MathF.Truncate(settings.textSize));
                    workingIndex -= settings.textSize;

                    settings.textString = fakePhoneNumber.ToString().Insert(3, "-");

                    if (HireAHitmanPlugin.DebugMode.Value)
                    {
                        HireAHitmanPlugin.PluginLogger.LogInfo($"Found override index {workingIndex} with textSize {settings.textSize} from raw {rawTextSize}");
                        HireAHitmanPlugin.PluginLogger.LogInfo($"Selected output: {settings.textString}");
                    }
                }
            }
        }

        // Setup the art, and get a reference to our MurderMO
        [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.Start))]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                foreach (var art in Toolbox.Instance.allArt)
                {
                    if(art != null && art.presetName == "HitmanPhoneNumber")
                    {
                        art.material = Material.Instantiate(art.material);
                        art.material.mainTexture = AssetBundleLoader.Texture2DLoader.CreateTexture2DFromPNG(System.Reflection.Assembly.GetExecutingAssembly(), System.IO.Path.Join("HireAHitman", "hireAHitmanWallArt.png"));
                    }
                }

                HitmanMO = Toolbox.Instance.allMurderMOs.Where(mo => mo.presetName == "ProfessionalHitMO").First();
                HitmanMO.disabled = !HireAHitmanPlugin.EnableHitmanInNormalGameplay.Value;
            }
        }


        // Debug helper
        [HarmonyPatch(typeof(GenerationController), nameof(GenerationController.PickArt))]
        internal class GenerationController_PickArt
        {
            public static void Postfix(ArtPreset __result, NewRoom room)
            {
                if (HireAHitmanPlugin.DebugMode.Value && __result.useDynamicText)
                {
                    HireAHitmanPlugin.PluginLogger.LogWarning($"Picked {__result.presetName} for {room.name} in {room.gameLocation?.name}");
                }
            }
        }
    }
}
