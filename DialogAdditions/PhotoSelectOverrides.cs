using HarmonyLib;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using SOD.Common.Extensions;
using SOD.Common.Helpers;
using Il2CppSystem.Collections.Generic;

namespace DialogAdditions
{
    [HarmonyPatch(typeof(PhotoSelectButtonController), nameof(PhotoSelectButtonController.OnLeftClick))]
    public class PhotoSelectButtonController_OnLeftClick
    {
        // Converted to strings so other mods can extend
        public static System.Collections.Generic.Dictionary<string, Func<Human, Human, Il2CppSystem.Collections.Generic.List<Evidence.DataKey>, bool>> CallTypes = new System.Collections.Generic.Dictionary<string, Func<Human, Human, Il2CppSystem.Collections.Generic.List<Evidence.DataKey>, bool>>() { { "DoYouKnowThisPerson", DoYouKnowThisPersonAdditions }, { "SeenThisPersonWithOthers", SeenThisPersonWithOthersCallback } };
        public static string callType = "DoYouKnowThisPerson";

        internal static bool Prefix(PhotoSelectButtonController __instance)
        {
            bool letMethodContinue = true;

            Human speaker = ((dynamic)InteractionController.Instance.talkingTo.isActor).Cast<Human>();
            Human askTarget = __instance.citizen;
            Il2CppSystem.Collections.Generic.List<Evidence.DataKey> askTargetKeys = __instance.citizen.evidenceEntry.GetTiedKeys(__instance.element.dk);

            CallTypes[callType](speaker, askTarget, askTargetKeys);

            // We only continue on the normal "DoYouKnowThisPerson" version, otherwise we short circuit
            letMethodContinue = callType == "DoYouKnowThisPerson";

            callType = "DoYouKnowThisPerson";

            if(!letMethodContinue)
            {
                __instance.thisWindow.CloseWindow();
                // Can't call the base method, unsure how to do so. Doesn't seem to break anything
                //typeof(ButtonController).GetMethod("OnLeftClick").Invoke(__instance, null);
            }

            return letMethodContinue;
        }

        static bool SeenThisPersonWithOthersCallback(Human speaker, Human askTarget, Il2CppSystem.Collections.Generic.List<Evidence.DataKey> askTargetKeys)
        {
            List<GroupsController.SocialGroup> overlappingGroups = new List<GroupsController.SocialGroup>();

            // Only works with a photo, or if the employee knows the persons name
            if (!askTargetKeys.Contains(Evidence.DataKey.photo) && !(DialogController.Instance.askTargetKeys.Contains(Evidence.DataKey.name) && speaker.FindAcquaintanceExists(askTarget, out _)))
            {
                // That's not enough info for me to help you
                speaker.speechController.Speak("a6815309-f9d4-40b0-8a1e-3ec3550c64a2", speakAbout: askTarget);
                return false;
            }

            // Check all groups
            foreach (var g in askTarget.groups)
            {
                // Does the group meet here?
                if (g.GetMeetingPlace().id == speaker.job.employer.address.id)
                {
                    // Do they meet on the same day as the speaker works?
                    foreach(var d in g.weekDays)
                    {
                        if(speaker.job.workDaysList.Contains(d))
                        {
                            // Are they working at the same time as the meetup?
                            var worksAtSameTimeAsMeetup = Toolbox.Instance.DecimalTimeRangeOverlap(
                                new Vector2(speaker.job.startTimeDecimalHour, speaker.job.startTimeDecimalHour + speaker.job.workHours),
                                new Vector2(g.decimalStartTime, g.decimalStartTime + g.GetPreset().meetUpLength)
                            );

                            if (worksAtSameTimeAsMeetup)
                            {
                                overlappingGroups.Add(g);
                                break;
                            }
                        }
                    }
                }
            }

            // Make sure the groups have more than 1 person in them
            for (int i = overlappingGroups.Count - 1; i >= 0; i--)
            {
                if (overlappingGroups[i].members.Count < 2)
                {
                    overlappingGroups.RemoveAt(i);
                }
            }

            if (overlappingGroups.Count == 0)
            {
                // No, I haven't seen them come in with anybody else
                speaker.speechController.Speak("b37bf9be-fd05-4000-af08-8eef590f54c2", speakAbout: askTarget);
                return false;
            }
            else if (overlappingGroups.Count == 1)
            {
                SpeakAboutGroup(overlappingGroups[0], speaker, askTarget);
                return false;
            }
            else
            {
                // Pick a random group to talk about
                var nameAsIntValue = speaker.name.ToCharArray().Aggregate(0, (result, c) => result + c);
                SpeakAboutGroup(overlappingGroups[nameAsIntValue % overlappingGroups.Count], speaker, askTarget);
                return false;
            }
        }

        static void SpeakAboutGroup(GroupsController.SocialGroup group, Human speaker, Human askTarget)
        {
            var groupPreset = group.GetPreset();

            ModifyDDSScopes.GroupToSpeakAbout = group;
            // There are |receiver.currentgroup.membercount| of them that meet here on |receiver.currentgroup.days| at |receiver.currentgroup.time|
            // SeenThisPersonWithOthers.GroupToSpeakAbout = null;
            if(group.members.Count == 2)
            {
                speaker.speechController.Speak("d5af67b2-9c85-41f1-a703-add0be0b1ddf", speakAbout: askTarget);
            }
            else if (group.members.Count <= 4)
            {
                speaker.speechController.Speak("76ec3908-68e5-4238-bc2e-57545ce46850", speakAbout: askTarget);
            }
            else
            {
                speaker.speechController.Speak("669bd28e-22fd-4db7-8b64-c4425ed7b0e7", speakAbout: askTarget);
            }
            
            if(groupPreset.groupType == GroupPreset.GroupType.interestGroup)
            {
                // I think it's a |receiver.currentgroup.type|.
                speaker.speechController.Speak("35388bb9-002d-4afc-960c-c22e33693030", speakAbout: askTarget);
            }
            else if(groupPreset.groupType == GroupPreset.GroupType.work)
            {
                // I think they are work colleagues
                speaker.speechController.Speak("c96c565c-819d-4bf8-804d-d0af4565a60f", speakAbout: askTarget);
            }
            else if (groupPreset.groupType == GroupPreset.GroupType.couples || groupPreset.groupType == GroupPreset.GroupType.cheaters)
            {
                // I think they are together?
                speaker.speechController.Speak("dc1396c0-561d-45f4-a1c1-63c14bf5b29c", speakAbout: askTarget);

                if (groupPreset.groupType == GroupPreset.GroupType.cheaters)
                {
                    // They were looking slightly nervous about being seen
                    speaker.speechController.Speak("e5cb4b85-b636-4d64-87d7-9607bd589863", speakAbout: askTarget);
                }
            }

            if (group.members.Count == 2)
            {
                var speakAbout = CityData.Instance.citizenDictionary[group.members.Where(memberId => memberId != askTarget.humanID).First()];

                if (speaker.FindAcquaintanceExists(askTarget, out var returnAcq))//  && returnAcq.known > 0.5f)
                {
                    // TODO: Levels of certainity?
                    // returnAcq.known
                    // I think their name is |receiver.casualname|?
                    speaker.speechController.Speak("846d9642-f500-41d4-97a2-c2821ae7c2ba", speakAbout: speakAbout);
                }
                else
                {
                    // They were...
                    speaker.speechController.Speak("b700b7d9-69f4-479a-b448-2a4234f3d4fb", speakAbout: speakAbout);
                    // |receiver.height|, with a |receiver.build| build. - dynamic based on traits
                    speaker.speechController.Speak("80409e68-0be4-4657-8afd-9a519a386f4f", speakAbout: speakAbout);
                }
            }
            else
            {
                bool firstMember = true;
                int descriptionsGiven = 0;

                // TODO: Random members

                foreach (var memberId in group.members)
                {
                    if (memberId == askTarget.humanID)
                    {
                        continue;
                    }

                    var member = CityData.Instance.citizenDictionary[memberId];

                    if (speaker.FindAcquaintanceExists(member, out var returnAcq))//  && returnAcq.known > 0.5f)
                    {
                        // TODO: Levels of certainity?
                        // returnAcq.known
                        // I think their name is |receiver.casualname|?
                        speaker.speechController.Speak("846d9642-f500-41d4-97a2-c2821ae7c2ba", speakAbout: member);
                    }
                    else
                    {
                        if (firstMember)
                        {
                            // One of them was...
                            speaker.speechController.Speak("a1711366-5c92-4b65-ab49-bc986c1e3fc8", speakAbout: member);
                            // |receiver.height|, with a |receiver.build| build. - dynamic based on traits
                            speaker.speechController.Speak("80409e68-0be4-4657-8afd-9a519a386f4f", speakAbout: member);
                        }
                        else
                        {
                            // Another one was...
                            speaker.speechController.Speak("1b8d9be3-baee-416d-8814-ec26a804d5bb", speakAbout: member);
                            // |receiver.height|, with a |receiver.build| build. - dynamic based on traits
                            speaker.speechController.Speak("80409e68-0be4-4657-8afd-9a519a386f4f", speakAbout: member);
                        }
                    }

                    firstMember = false;
                    descriptionsGiven++;

                    if (descriptionsGiven > 2)
                    {
                        break;
                    }
                }
            }
        }

        static bool DoYouKnowThisPersonAdditions(Human speaker, Human askTarget, Il2CppSystem.Collections.Generic.List<Evidence.DataKey> askTargetKeys)
        {
            if (!speaker) return true;

            // Self, check if they would normally give their name
            if (speaker.humanID == askTarget.humanID && (askTargetKeys.Contains(Evidence.DataKey.name) || askTargetKeys.Contains(Evidence.DataKey.photo)))
            {
                var success = InternalMethodClones.TestDialogForSuccess_useSuccessTest("Introduce", speaker);

                DialogController.Instance.askTarget = askTarget;
                DialogController.Instance.askTargetKeys = askTargetKeys;

                if (success)
                {
                    speaker.speechController.Speak("4e8db6e9-04fe-4c5b-9bf6-05d8c3f8a230", speakAbout: askTarget);
                    InternalMethodClones.MergeTargetKeys(askTarget, Evidence.DataKey.name);
                    InternalMethodClones.MergeTargetKeys(askTarget, Evidence.DataKey.photo);
                    InternalMethodClones.MergeTargetKeys(askTarget, Evidence.DataKey.voice);
                }
                else
                {
                    speaker.speechController.Speak("a6815309-f9d4-40b0-8a1e-3ec3550c64a2", speakAbout: askTarget);
                }

                return false;
            }
            // "Yes, that is the suspicious person I saw earlier!"
            // Only if the setting is enabled, the NPC saw something suss, and the NPC saw the murderer
            else if (
                DialogAdditionPlugin.ConfirmSuspiciousPhotos.Value &&
                speaker.lastSightings.ContainsKey(askTarget) &&
                askTarget.humanID == MurderController.Instance.currentMurderer?.humanID &&
                speaker.humanID != MurderController.Instance.currentMurderer?.humanID &&
                InternalMethodClones.TestDialogForSuccess_useSuccessTest("SeenOrHeardUnusual", speaker) &&
                InternalMethodClones.SeenOrHeardUnusual_LastSightingCheckOnly(speaker, askTarget)
                )
            {
                speaker.speechController.Speak("90f766cd-ae3f-482d-9cbb-5f72a69a0a4b", speakAbout: askTarget);
                // Continue, so they can give a location
                return true;
            }

            // Acquaintance
            return true;
        }
    }

    class InternalMethodClones
    {
        // Copied from PhotoSelectButtonController
        public static void MergeTargetKeys(Human askTarget, Evidence.DataKey key)
        {
            foreach (Evidence.DataKey askTargetKey in DialogController.Instance.askTargetKeys)
                askTarget.evidenceEntry.MergeDataKeys(askTargetKey, key);
        }

        // Copied from Citizen (ref doesn't work nicely)
        public static float GetChance(Citizen cit, List<CharacterTrait.TraitPickRule> pickRules, float baseChance)
        {
            bool flag1 = true;
            foreach (CharacterTrait.TraitPickRule traitPickRule in pickRules)
            {
                bool flag2 = false;
                if (traitPickRule.rule == CharacterTrait.RuleType.ifAnyOfThese)
                {
                    foreach (CharacterTrait trait in traitPickRule.traitList)
                    {
                        CharacterTrait searchTrait = trait;
                        if (cit.characterTraits.Exists((Il2CppSystem.Predicate<Human.Trait>)(item => item.trait == searchTrait)))
                        {
                            flag2 = true;
                            break;
                        }
                    }
                }
                else if (traitPickRule.rule == CharacterTrait.RuleType.ifAllOfThese)
                {
                    flag2 = true;
                    foreach (CharacterTrait trait in traitPickRule.traitList)
                    {
                        CharacterTrait searchTrait = trait;
                        if (!cit.characterTraits.Exists((Il2CppSystem.Predicate<Human.Trait>)(item => item.trait == searchTrait)))
                        {
                            flag2 = false;
                            break;
                        }
                    }
                }
                else if (traitPickRule.rule == CharacterTrait.RuleType.ifNoneOfThese)
                {
                    flag2 = true;
                    foreach (CharacterTrait trait in traitPickRule.traitList)
                    {
                        CharacterTrait searchTrait = trait;
                        if (cit.characterTraits.Exists((Il2CppSystem.Predicate<Human.Trait>)(item => item.trait == searchTrait)))
                        {
                            flag2 = false;
                            break;
                        }
                    }
                }
                else if (traitPickRule.rule == CharacterTrait.RuleType.ifPartnerAnyOfThese && cit.partner)
                {
                    foreach (CharacterTrait trait in traitPickRule.traitList)
                    {
                        CharacterTrait searchTrait = trait;
                        if (cit.partner.characterTraits.Exists((Il2CppSystem.Predicate<Human.Trait>)(item => item.trait == searchTrait)))
                        {
                            flag2 = true;
                            break;
                        }
                    }
                }
                if (flag2)
                    baseChance += traitPickRule.baseChance;
                else if (traitPickRule.mustPassForApplication)
                    flag1 = false;
            }
            return !flag1 ? 0.0f : Mathf.Clamp01(baseChance);
        }

        public static bool TestDialogForSuccess_useSuccessTest(string presetName, Human cit)
        {
            var dialogPreset = DialogAdditionPlugin.dialogPresetRefs[presetName];
            bool success = true;

            // Start copied from game: DialogController.ExecuteDialog
            float baseChance = dialogPreset.baseChance;
            if (cit && dialogPreset.modifySuccessChanceTraits.Count > 0)
                baseChance = InternalMethodClones.GetChance(((dynamic)cit).Cast<Citizen>(), dialogPreset.modifySuccessChanceTraits, baseChance);
            // Can't use cit.GetChance because of the ref parameter
            // baseChance = cit.GetChance(ref modifySuccessTraits, baseChance);

            if (cit && cit.ai.restrained)
                baseChance += dialogPreset.affectChanceIfRestrained;

            if (dialogPreset.specialCase == DialogPreset.SpecialCase.lookAroundHome)
                baseChance += UpgradeEffectController.Instance.GetUpgradeEffect(SyncDiskPreset.Effect.guestPassIssueModifier);

            float num = Mathf.Clamp01(baseChance + UpgradeEffectController.Instance.GetUpgradeEffect(SyncDiskPreset.Effect.dialogChanceModifier));
            if (dialogPreset.specialCase == DialogPreset.SpecialCase.mustBeMurdererForSuccess && MurderController.Instance.currentMurderer?.humanID != cit?.humanID)
            {
                num = 0.0f;
                success = false;
            }
            string psuedoRandomSeed = cit.name;
            success = (double)Toolbox.Instance.GetPsuedoRandomNumber(0.0f, 1f, ref psuedoRandomSeed) <= (double)num;
            // End copied from game

            return success;
        }

        public static bool SeenOrHeardUnusual_LastSightingCheckOnly(Human speaker, Human saysTo)
        {
            float num = -99999f;
            Human.Sighting sighting = null;
            Human human = null;
            if (MurderController.Instance == null || MurderController.Instance.activeMurders == null)
            {
                foreach (KeyValuePair<Human, Human.Sighting> lastSighting in saysTo.lastSightings)
                {
                    if (!(lastSighting.Key == saysTo) && lastSighting.Value.poi && !lastSighting.Value.phone && (double)lastSighting.Value.time > (double)num && lastSighting.Value.sound == 0)
                    {
                        num = lastSighting.Value.time;
                        human = lastSighting.Key;
                        sighting = lastSighting.Value;
                    }
                }
            }

            return sighting != null;
        }
    }
}
