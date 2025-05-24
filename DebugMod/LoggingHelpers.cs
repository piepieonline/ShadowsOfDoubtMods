using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniverseLib;

namespace DebugMod
{
    public class LoggingHelpers
    {
        public enum HumanDebugOverloaded
        {
            movement,
            actions,
            attacks,
            updates,
            misc,
            sight,
            none = -1,
            all = 99
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Log))]
        public class Game_Log
        {
            public static bool Prefix(object print, int level)
            {
                if (DebugModPlugin.EnableGameLog.Value)
                {
                    var t = print.GetActualType();
                    var toString = t.GetMethod("ToString", System.Type.EmptyTypes); // t.GetMethods().Where(mi => mi.Name == "ToString" && mi.GetParameters().Length == 0).First();
                    string stringPrintedValue = (string)toString.Invoke((print).TryCast(t), null);
                    if (DebugModPlugin.GameLogFilter.Value == "" || stringPrintedValue.ToLower().Contains(DebugModPlugin.GameLogFilter.Value.ToLower()))
                        if (DebugModPlugin.GameLogInverseFilter.Value == "" || !stringPrintedValue.ToLower().Contains(DebugModPlugin.GameLogInverseFilter.Value.ToLower()))
                            return true; //  PluginLogger.Log((LogLevel)(1 << level), $"{t.Name}: {stringPrintedValue}");
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(Actor), nameof(Actor.SelectedDebug))]
        public class Actor_SelectedDebug
        {
            public static void Prefix(Actor __instance, string str, Actor.HumanDebug debug)
            {
                if (DebugModPlugin.ActorLogName.Value != "" && __instance.name == DebugModPlugin.ActorLogName.Value)
                {
                    if(DebugModPlugin.ActorLogCategory.Value == HumanDebugOverloaded.all || (Actor.HumanDebug)DebugModPlugin.ActorLogCategory.Value == debug)
                    {
                        Game.Log($"Actor Debugging ({System.Enum.GetName(debug)}): {__instance.name} - {str}");
                    }
                }
            }
        }
    }
}
