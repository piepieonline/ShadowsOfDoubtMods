using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace AssetBundleLoader
{
    internal class Hooks_ErrorsDebugging
    {
        private static List<string> ContentThatFailedToLoad = new List<string>();

        [HarmonyPatch(typeof(SaveStateController), nameof(SaveStateController.LoadSaveState))]
        public class SaveStateController_LoadSaveState
        {
            public static void Prefix()
            {
                ContentThatFailedToLoad.Clear();
            }
        }

        [HarmonyPatch(typeof(MurderController), nameof(MurderController.OnStartGame))]
        internal class MurderController_OnStartGame
        {
            public static void Prefix()
            {
                if (ContentThatFailedToLoad.Count > 0)
                {
                    if(!Strings.stringTable["ui.popups"].ContainsKey("pie_abl_quit"))
                    {
                        Strings.stringTable["ui.popups"].Add("pie_abl_quit", new Strings.DisplayString() { displayStr = "Quit" });
                        Strings.stringTable["ui.popups"].Add("pie_abl_continuebroken", new Strings.DisplayString() { displayStr = "Continue (Game may be in a broken state)" });
                        Strings.stringTable["ui.popups"].Add("pie_abl_modloaderror_title", new Strings.DisplayString() { displayStr = "Modded Content Failed To Load" });
                        Strings.stringTable["ui.popups"].Add("pie_abl_modloaderror_body", new Strings.DisplayString() { displayStr = "This save was made with modded content that is now missing.\nContinuing might put your game into a broken state - do you wish to continue?\n\nThe missing content includes (Other content may also be missing!)" });
                    }

                    PopupMessageController.Instance.PopupMessage($"pie_abl_modloaderror", true, true, "pie_abl_quit", "pie_abl_continuebroken", true, PopupMessageController.AffectPauseState.yes, true, ContentThatFailedToLoad.Join());
                    PopupMessageController.Instance.OnLeftButton = (PopupMessageController.LeftButton)Quit;
                }
            }

            public static void Quit()
            {
                Application.Quit();
            }
        }

        // Patch known failure locations to try and avoid the need for the console or logs 

        // Interactables are easy, they have an obvious place to intercept
        [HarmonyPatch(typeof(Interactable), nameof(Interactable.OnLoad))]
        public class Interactable_OnLoad
        {
            public static bool Prefix(Interactable __instance)
            {
                if (!Toolbox.Instance.objectPresetDictionary.ContainsKey(__instance.p))
                {
                    if(!ContentThatFailedToLoad.Contains(__instance.p))
                    {
                        BundleLoader.PluginLogger.LogError($"Unable to find preset for {__instance.p}, are you missing a mod?");
                        ContentThatFailedToLoad.Add(__instance.p);
                    }
                    return false;
                }

                return true;
            }
        }

        // Lots of content is loaded through Toolbox.LoadDataFromResources
        // But it's a. generic and b. has a ref param
        // So just watch the error log instead, and hope the format doesn't change
        [HarmonyPatch]
        public class Game_LogError
        {
            [HarmonyTargetMethods]
            internal static IEnumerable<System.Reflection.MethodBase> CalculateMethods()
            {
                var mi = typeof(Game).GetMethods().Where(mi => mi.Name == "LogError");
                return mi;
            }

            public static void Prefix(object print)
            {
                if (print.ToTypedString().Contains("Resources load error"))
                {
                    ContentThatFailedToLoad.Add(
                        new Regex("name or ID (.+?),").Match(print.ToTypedString()).Groups[1].Value
                    );
                }
            }
        }
    }
}
