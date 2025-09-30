using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdditionalEvidence
{
    internal class GunSerialNumbersDialog
    {
        [HarmonyPatch(typeof(Human), nameof(Human.TryGiveItem))]
        public class Human_TryGiveItem
        {
            // Method returns a bool.. that doesn't do anything. So we have to give it back manually if required
            // If the gun or casing is given to an NPC, check:
            // - Do they work in a 'Laboratory'? If not, vanilla processing takes over
            // - Are they a 'LabWorker'? If not, tell them to talk to a lab worker
            // - Are they at work? If not, tell the player to come back later
            // - If this all passes, analyse, get the results and tell the player (and pop up the evidence too)
            public static bool Prefix(Human __instance, Interactable givenItem, Human givenBy)
            {
                if(givenBy.isPlayer)
                {
                    var isGun = GunSerialNumbers.IsInteractableAGun(givenItem);
                    var isBullet = GunSerialNumbers.IsInteractableACasing(givenItem) && GunSerialNumbers.SpawnedBulletsToGunIdMap.ContainsKey(givenItem.id);

                    if (isGun || isBullet)
                    {
                        // Make sure the NPC is the right type of job
                        if (__instance.job.preset.name == "LabWorker")
                        {
                            // And actually at work
                            if (__instance.isAtWork)
                            {
                                // "Sure, I can analyse that for you"
                                __instance.speechController.Speak("59639f4f-c928-45ad-8d41-9d81536b90da");

                                if (isGun)
                                {
                                    var gunSerialEvidence = GunSerialNumbers.GetOrCreateEvidence("EP_GunSerialNumber|", "EP_GunSerialNumber", givenItem);
                                    var gunStampEvidence = GunSerialNumbers.GetOrCreateEvidence("EP_GunHeadStamp|", "EP_GunHeadStamp", givenItem);
                                    InterfaceController.Instance.SpawnWindow(gunSerialEvidence, passedInteractable: givenItem);
                                    InterfaceController.Instance.SpawnWindow(gunStampEvidence, passedInteractable: givenItem);

                                    GunSerialNumbers.ItemGivenToHuman = givenItem;

                                    // Speech to indicate the scan is complete.
                                    var gunCompleteMessage = __instance.ParseDDSMessage(new DDSSaveClasses.DDSMessageSettings() { msgID = "5959709d-6c59-4c86-806b-dc3b95512b0e" }, null, givenItem)[0];
                                    __instance.speechController.Speak("dds.blocks", gunCompleteMessage, useParsing: true, delay: 1.5f);

                                    // Give the item back
                                    givenBy.TryGiveItem(givenItem, __instance, true, false);
                                }
                                else
                                {
                                    var gunBulletStampEvidence = GunSerialNumbers.GetOrCreateEvidence("EP_GunBulletHeadStamp|", "EP_GunHeadStamp", givenItem);
                                    InterfaceController.Instance.SpawnWindow(gunBulletStampEvidence, passedInteractable: givenItem);

                                    GunSerialNumbers.ItemGivenToHuman = givenItem;

                                    // Speech to indicate the scan is complete.
                                    var casingCompleteMessage = __instance.ParseDDSMessage(new DDSSaveClasses.DDSMessageSettings() { msgID = "6469af6a-97b1-4b31-a673-35f2f827af57" }, null, givenItem)[0];
                                    __instance.speechController.Speak("dds.blocks", casingCompleteMessage, useParsing: true, delay: 1.5f);

                                    // Give the item back
                                    givenBy.TryGiveItem(givenItem, __instance, true, false);
                                }
                            }
                            else
                            {
                                // Otherwise, "give it to me at work and I'll analyse it for you"
                                __instance.speechController.Speak("66da9109-6801-4e14-9847-d235648dfed0");

                                // Give the item back
                                givenBy.TryGiveItem(givenItem, __instance, true, false);
                            }

                            return false;
                        }
                        else if (__instance.job.employer.preset.name == "Laboratory")
                        {
                            // Otherwise, "give that to one of the lab workers"
                            __instance.speechController.Speak("cf7df173-6a99-4dd6-8a07-a993981a9ddd");

                            // Give the item back
                            givenBy.TryGiveItem(givenItem, __instance, true, false);

                            return false;
                        }
                    }
                }

                return true;
            }
        }
    }
}
