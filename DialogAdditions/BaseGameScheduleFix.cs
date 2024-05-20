using HarmonyLib;
using Il2CppInterop.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DialogAdditions
{
    internal class BaseGameScheduleFix
    {


        [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.CreateNewGoal))]
        public class NewAIController_CreateNewGoal
        {
            public static void Prefix(NewAIController __instance, ref AIGoalPreset newPreset, float newTrigerTime, ref NewGameLocation newPassedGameLocation)
            {
                if (newPreset != null && newPreset.name.Contains("MeetUpEvent"))
                {
                    DialogAdditionPlugin.PluginLogger.LogInfo($"{newPreset.name}: {newTrigerTime} @ {__instance?.human?.citizenName} at {newPassedGameLocation.thisAsAddress.name}");
                    newPassedGameLocation = CityData.Instance.addressDictionary[newPassedGameLocation.thisAsAddress.id - 1];
                    newPreset.basePriority = 5;
                    DialogAdditionPlugin.PluginLogger.LogInfo($"{newPreset.name}: {newTrigerTime} @ {__instance?.human?.citizenName} at {newPassedGameLocation.thisAsAddress.name}");
                }
            }
        }

        // This method has a bug in the base game, has to be completely replaced
        [HarmonyPatch]
        public class SessionData_GetNextOrPreviousGameTimeForThisHour
        {
            [HarmonyTargetMethod]
            internal static System.Reflection.MethodBase FindBaseMethod()
            {
                var mi = typeof(SessionData).GetMethods().Where(mi => mi.Name == "GetNextOrPreviousGameTimeForThisHour" && mi.GetParameters().Length == 6).First();
                return mi;
            }

            [HarmonyPrefix]
            public static bool GetNextOrPreviousGameTimeForThisHour(float newTime, float newDecimalClock, SessionData.WeekDay newDay, Il2CppSystem.Collections.Generic.List<SessionData.WeekDay> days, float startHour, float endHour, ref float __result)
            {
                float num1 = newTime - newDecimalClock;
                if ((double)endHour >= (double)startHour)
                {
                    if (days.Contains(newDay))
                    {
                        if ((double)newDecimalClock >= (double)startHour && (double)newDecimalClock <= (double)endHour)
                        {
                            float num2 = newDecimalClock - startHour;
                            __result = newTime - num2;
                            return false;
                        }
                        if ((double)newDecimalClock < (double)startHour)
                        {
                            __result = num1 + startHour;
                            return false;
                        }
                    }
                }
                else if ((double)endHour < (double)startHour)
                {
                    if ((double)newDecimalClock >= (double)endHour)
                    {
                        __result = num1 + startHour;
                        return false;
                    }
                    SessionData.WeekDay weekDay = SessionData.Instance.day != SessionData.WeekDay.monday ? SessionData.Instance.day - 1 : SessionData.WeekDay.sunday;
                    if (days.Contains(weekDay))
                    {
                        __result = num1 - (24f - startHour);
                        return false;
                    }
                }
                int num3 = (int)(newDay + 1);
                if (num3 >= 7)
                    num3 = 0;
                SessionData.WeekDay weekDay1 = (SessionData.WeekDay)num3;
                float num4 = num1 + 24f;
                for (int index = 8; !days.Contains(weekDay1) && index > 0; --index)
                {
                    // This is the base game bug
                    // weekDay1 = weekDay1 != SessionData.WeekDay.sunday ? __instance.day + 1 : SessionData.WeekDay.monday;
                    // This is the fix
                    weekDay1 = weekDay1 != SessionData.WeekDay.sunday ? weekDay1 + 1 : SessionData.WeekDay.monday;
                    num4 += 24f;
                }
                __result = num4 + startHour;
                return false;
            }
        }
    }
}
