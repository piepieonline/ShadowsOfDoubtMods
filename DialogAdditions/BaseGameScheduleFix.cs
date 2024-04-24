using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DialogAdditions
{
    internal class BaseGameScheduleFix
    {
        /*
         [HarmonyPatch(typeof(NewAIGoal), nameof(NewAIGoal.UpdateNextGroupTimes))]
         public class NewAIGoal_UpdateNextGroupTimes
         {
             public static void Postfix(NewAIGoal __instance)
             {
                 DialogAdditionPlugin.PluginLogger.LogInfo($"UpdateNextGroupTimes: {__instance?.passedGroup?.id} : {__instance?.passedGroup?.preset} @ {__instance?.triggerTime}");
             }
         }
         */


        [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.AITick))]
        public class NewAIController_AITick
        {
            public static void Postfix(NewAIController __instance)
            {
                if (__instance != null && __instance.currentGoal != null && __instance.currentGoal.name.Contains("MeetUpEvent"))
                {
                    DialogAdditionPlugin.PluginLogger.LogInfo($"{__instance.currentGoal.name}: {__instance.currentGoal.triggerTime} @ {__instance?.human?.citizenName} at {__instance?.currentGoal?.gameLocation?.name}");
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
