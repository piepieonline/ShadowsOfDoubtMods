using Cpp2IL.Core.Api;
using HarmonyLib;
using SOD.Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniverseLib;

namespace Pies_Generic_Patch.BugFixes
{
    /// <summary>
    /// In vanilla, Groups are broken for multiple reasons:
    /// - When calculating group meet-ups, the time check is incorrect, so any location that isn't 24 hours (the diner) is marked as closed
    /// - Routine goals are created before groups, so when it creates routine goals there are no groups assigned to the citizen
    /// - Groups are created before all citizens are created, so only the first 50 or so citizens will be in a group properly
    /// - Specifically for cheaters, there is another issue when meeting up isn't possible (times don't align), so no meetup event is created
    /// </summary>
    public class FixGroups
    {
        public static void DoPatch(Harmony harmony)
        {
            harmony.PatchAll(typeof(FixGroups.Company_IsOpenAtDecimalTime));
            harmony.PatchAll(typeof(FixGroups.GroupsController_CreateGroups));
        }

        // Fix the time of day check, ensure that the checked company is open when the group wishes to meet
        [HarmonyPatch(typeof(Company), nameof(Company.IsOpenAtDecimalTime))]
        public class Company_IsOpenAtDecimalTime
        {
            public static bool Prefix(ref bool __result, Company __instance, SessionData.WeekDay day, float hour)
            {
                float openHour = __instance.preset.workHours.retailOpenHours.x;
                float closeHour = __instance.preset.workHours.retailOpenHours.y;

                // Check if the store is open on this day
                bool isOpenToday = day switch
                {
                    SessionData.WeekDay.monday => __instance.preset.workHours.monday,
                    SessionData.WeekDay.tuesday => __instance.preset.workHours.tuesday,
                    SessionData.WeekDay.wednesday => __instance.preset.workHours.wednesday,
                    SessionData.WeekDay.thursday => __instance.preset.workHours.thursday,
                    SessionData.WeekDay.friday => __instance.preset.workHours.friday,
                    SessionData.WeekDay.saturday => __instance.preset.workHours.saturday,
                    SessionData.WeekDay.sunday => __instance.preset.workHours.sunday,
                    _ => false
                };

                // Check if the store was open on the previous day
                SessionData.WeekDay previousDay = (SessionData.WeekDay)(((int)day + 6) % 7);
                bool isOpenYesterday = previousDay switch
                {
                    SessionData.WeekDay.monday => __instance.preset.workHours.monday,
                    SessionData.WeekDay.tuesday => __instance.preset.workHours.tuesday,
                    SessionData.WeekDay.wednesday => __instance.preset.workHours.wednesday,
                    SessionData.WeekDay.thursday => __instance.preset.workHours.thursday,
                    SessionData.WeekDay.friday => __instance.preset.workHours.friday,
                    SessionData.WeekDay.saturday => __instance.preset.workHours.saturday,
                    SessionData.WeekDay.sunday => __instance.preset.workHours.sunday,
                    _ => false
                };

                if (openHour <= closeHour && !(openHour < 1 && closeHour > 23))
                {
                    // Regular open hours (same day)
                    __result = isOpenToday && hour >= openHour && hour <= closeHour;
                }
                else
                {
                    // Overnight hours (e.g., 12-2 (bars))
                    bool inTodayRange = isOpenToday && hour >= openHour;
                    bool inYesterdayRange = isOpenYesterday && hour <= closeHour;
                    __result = inTodayRange || inYesterdayRange;
                }

                // Always short circuit
                return false;
            }
        }

        // Delayed until all citizens are created, otherwise most cheaters and dates won't get groups created
        // Add the goal (when the base game tries to do this, groups haven't been assigned yet)
        [HarmonyPatch(typeof(GroupsController), nameof(GroupsController.CreateGroups))]
        public class GroupsController_CreateGroups
        {
            public static bool ReadyForGroups = false;

            public static bool Prefix()
            {
                if (!ReadyForGroups)
                {
                    if (Pies_Generic_PatchPlugin.DebugLogging.Value)
                        Pies_Generic_PatchPlugin.Log.LogInfo($"Skipping groups");
                    return false;
                }
                return true;
            }

            public static void Postfix()
            {
                // Base game creates groups partway through citizen creation, delay until all citizens are ready
                if (!ReadyForGroups)
                    return;

                if (Pies_Generic_PatchPlugin.DebugLogging.Value)
                    Pies_Generic_PatchPlugin.Log.LogInfo($"CreateGroups running");

                var meetingCheaters = new HashSet<string>();

                foreach (var group in GroupsController.Instance.groups)
                {
                    // Goals are created before groups are assigned in the base game
                    // So go back and add the goals now
                    if(group.meetingPlace > 0)
                    {
                        var groupPreset = group.GetPreset();
                        var newPassedGameLocation = group.GetMeetingPlace();
                        foreach (var cId in group.members)
                        {
                            var citizen = CityData.Instance.citizenDictionary[cId];
                            citizen.ai.CreateNewGoal(groupPreset.meetUpGoal, 0.0f, groupPreset.meetUpLength, newPassedGameLocation: newPassedGameLocation, newPassedGroup: group).name = $"{groupPreset.meetUpGoal.name}: {groupPreset.name}";
                            
                            if (Pies_Generic_PatchPlugin.DebugLogging.Value)
                                Pies_Generic_PatchPlugin.Log.LogInfo($"Created goal for {citizen.citizenName} - {group.preset} ({group.id})");

                            if (group.preset == "CheatersMeet")
                                meetingCheaters.Add(citizen.citizenName);
                        }
                    }
                }

                var seenList = new HashSet<string>();
                foreach(var citizen in CityData.Instance.citizenDirectory)
                {
                    if(citizen.paramour == null)
                    {
                        continue;
                    }

                    if (!seenList.Contains(citizen.citizenName) && !seenList.Contains(citizen.paramour.citizenName))
                    {
                        seenList.Add(citizen.citizenName);
                        seenList.Add(citizen.paramour.citizenName);

                        if (Pies_Generic_PatchPlugin.DebugLogging.Value)
                        {
                            Pies_Generic_PatchPlugin.Log.LogInfo($"Checking cheaters: {citizen.citizenName} & {citizen.paramour.citizenName}");
                            Pies_Generic_PatchPlugin.Log.LogInfo($"\t{citizen.citizenName} has meeting: {meetingCheaters.Contains(citizen.citizenName)}");
                            Pies_Generic_PatchPlugin.Log.LogInfo($"\t{citizen.paramour.citizenName} has meeting: {meetingCheaters.Contains(citizen.paramour.citizenName)}");
                        }
                    }
                }
            }
        }

        // Delay the group creation until all citizens are created
        // We have to patch the virtual method as no real implementation exists
        [HarmonyPatch(typeof(Creator), nameof(Creator.SetComplete))]
        public class CitizenCreator_SetComplete
        {
            public static void Prefix(object __instance)
            {
                // Ensure that it's the CitizenCreator that is complete, not another inheriting class
                if (__instance.GetActualType() == typeof(RelationshipCreator) &&
                    (CityConstructor.Instance.generateNew || false)
                )
                {
                    GroupsController_CreateGroups.ReadyForGroups = true;
                    GroupsController.Instance.CreateGroups();
                }
            }
        }
    }
}
