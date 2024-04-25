using HarmonyLib;
using System;
using Il2CppSystem.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using SOD.Common.Extensions;

namespace DialogAdditions
{
    [HarmonyPatch(typeof(PhotoSelectButtonController), nameof(PhotoSelectButtonController.OnLeftClick))]
    public class PhotoSelectButtonController_OnLeftClick
    {
        public static CallType callType = CallType.DoYouKnowThisPerson;

        public enum CallType { DoYouKnowThisPerson, SeenThisPersonWithOthers }

        internal static bool Prefix(PhotoSelectButtonController __instance)
        {
            bool letMethodContinue = true;

            Human speaker = ((dynamic)InteractionController.Instance.talkingTo.isActor).Cast<Human>();
            Human askTarget = __instance.citizen;
            Il2CppSystem.Collections.Generic.List<Evidence.DataKey> askTargetKeys = __instance.citizen.evidenceEntry.GetTiedKeys(__instance.element.dk);

            switch (callType)
            {
                case CallType.SeenThisPersonWithOthers:
                    SeenThisPersonWithOthersCallback(speaker, askTarget, askTargetKeys);
                    letMethodContinue = false;
                    break;
                default:
                    letMethodContinue = DoYouKnowThisPersonAdditions(speaker, askTarget, askTargetKeys);
                    break;
            }

            callType = CallType.DoYouKnowThisPerson;

            if(!letMethodContinue)
            {
                __instance.thisWindow.CloseWindow();
                // Can't call the base method, unsure how to do so. Doesn't seem to break anything
                //typeof(ButtonController).GetMethod("OnLeftClick").Invoke(__instance, null);
            }

            return letMethodContinue;
        }

        static void SeenThisPersonWithOthersCallback(Human speaker, Human askTarget, Il2CppSystem.Collections.Generic.List<Evidence.DataKey> askTargetKeys)
        {
            List<GroupsController.SocialGroup> overlappingGroups = new List<GroupsController.SocialGroup>();

            // Only works with a photo, or if the employee knows the persons name
            if (!askTargetKeys.Contains(Evidence.DataKey.photo) && !(DialogController.Instance.askTargetKeys.Contains(Evidence.DataKey.name) && speaker.FindAcquaintanceExists(askTarget, out _)))
            {
                // That's not enough info for me to help you
                speaker.speechController.Speak("a6815309-f9d4-40b0-8a1e-3ec3550c64a2", speakAbout: askTarget);
                return;
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
                return;
            }
            else if (overlappingGroups.Count == 1)
            {
                SpeakAboutGroup(overlappingGroups[0], speaker, askTarget);
                return;
            }
            else
            {
                // Pick a random group to talk about
                var nameAsIntValue = speaker.name.ToCharArray().Aggregate(0, (result, c) => result + c);
                SpeakAboutGroup(overlappingGroups[nameAsIntValue % overlappingGroups.Count], speaker, askTarget);
                return;
            }
        }

        static void SpeakAboutGroup(GroupsController.SocialGroup group, Human speaker, Human askTarget)
        {
            var groupPreset = group.GetPreset();

            ModifyDDSScopes.GroupToSpeakAbout = group;
            // There are |receiver.currentgroup.membercount| of them that meet here on |receiver.currentgroup.days| at |receiver.currentgroup.time|
            speaker.speechController.Speak("669bd28e-22fd-4db7-8b64-c4425ed7b0e7", speakAbout: askTarget);
            // SeenThisPersonWithOthers.GroupToSpeakAbout = null;

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
                if (speaker.FindAcquaintanceExists(askTarget, out var returnAcq))//  && returnAcq.known > 0.5f)
                {
                    // TODO: Levels of certainity?
                    // returnAcq.known
                    // I think their name is |receiver.casualname|?
                    speaker.speechController.Speak("846d9642-f500-41d4-97a2-c2821ae7c2ba", speakAbout: askTarget);
                }
                else
                {
                    // They were...
                    speaker.speechController.Speak("b700b7d9-69f4-479a-b448-2a4234f3d4fb", speakAbout: askTarget);
                    // |receiver.height|, with a |receiver.build| build. - dynamic based on traits
                    speaker.speechController.Speak("80409e68-0be4-4657-8afd-9a519a386f4f", speakAbout: askTarget);
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

        static CharacterTrait charImpulsive;
        static CharacterTrait charAwkward;
        static bool DoYouKnowThisPersonAdditions(Human speaker, Human askTarget, Il2CppSystem.Collections.Generic.List<Evidence.DataKey> askTargetKeys)
        {
            // Self, check if they would normally give their name
            if (speaker.humanID == askTarget.humanID && (askTargetKeys.Contains(Evidence.DataKey.name) || askTargetKeys.Contains(Evidence.DataKey.photo)))
            {
                var success = InternalMethodClones.TestDialogForSuccess("Introduce", speaker);

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
            else if (speaker.humanID == MurderController.Instance.currentMurderer.humanID)
            {
                if (charImpulsive == null) charImpulsive = Toolbox.Instance.allCharacterTraits.Where(item => item.name == "Char-Impulsive").First();
                if (charAwkward == null) charAwkward = Toolbox.Instance.allCharacterTraits.Where(item => item.name == "Char-Awkward").First();

                // If they have these traits, just clam up and pretend to not know them
                if ((speaker.TraitExists(charImpulsive) || speaker.TraitExists(charAwkward)) && MurderController.Instance.activeMurders.Exists((Il2CppSystem.Predicate<MurderController.Murder>)(murder => murder.victimID == askTarget.humanID)))
                {
                    speaker.speechController.Speak("a6815309-f9d4-40b0-8a1e-3ec3550c64a2", speakAbout: askTarget);
                    return false;
                }
            }

            // Acquaintance
            return true;
        }

        // If the questioned person is the murdered, they should pretend to not have seen the vic
        [HarmonyPatch(typeof(Human), nameof(Human.RevealSighting), [typeof(Human), typeof(Human.Sighting)])]
        class Human_RevealSighting_Murderer
        {
            static bool Prefix(Human __instance, Human prospectCitizen)
            {
                if (__instance.humanID == MurderController.Instance.currentMurderer.humanID)
                {
                    if (MurderController.Instance.activeMurders.Exists((Il2CppSystem.Predicate<MurderController.Murder>)(murder => murder.victimID == prospectCitizen.humanID)))
                    {
                        __instance.speechController.Speak("aeba5683-cb14-4df7-a95c-04025dfcd5d0", speakAbout: prospectCitizen);
                        return false;
                    }
                }

                return true;
            }
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

        public static bool TestDialogForSuccess(string presetName, Human cit)
        {
            var introduceDialogPreset = DialogAdditionPlugin.dialogPresets[presetName];
            bool success = true;

            // Start copied from game: DialogController.ExecuteDialog
            float baseChance = introduceDialogPreset.baseChance;
            if (cit && introduceDialogPreset.modifySuccessChanceTraits.Count > 0)
                baseChance = InternalMethodClones.GetChance(((dynamic)cit).Cast<Citizen>(), introduceDialogPreset.modifySuccessChanceTraits, baseChance);
            // Can't use cit.GetChance because of the ref parameter
            // baseChance = cit.GetChance(ref modifySuccessTraits, baseChance);

            if (cit && cit.ai.restrained)
                baseChance += introduceDialogPreset.affectChanceIfRestrained;

            if (introduceDialogPreset.specialCase == DialogPreset.SpecialCase.lookAroundHome)
                baseChance += UpgradeEffectController.Instance.GetUpgradeEffect(SyncDiskPreset.Effect.guestPassIssueModifier);

            float num = Mathf.Clamp01(baseChance + UpgradeEffectController.Instance.GetUpgradeEffect(SyncDiskPreset.Effect.dialogChanceModifier));
            if (introduceDialogPreset.specialCase == DialogPreset.SpecialCase.mustBeMurdererForSuccess && MurderController.Instance.currentMurderer != cit)
            {
                num = 0.0f;
                success = false;
            }
            success = (double)Toolbox.Instance.GetPsuedoRandomNumber(0.0f, 1f, cit.name) <= (double)num;
            // End copied from game

            return success;
        }
    }
}
