using BepInEx.Logging;
using HarmonyLib;
using SOD.Common.Extensions;

namespace DialogAdditions;

public class SpeakOverrides
{
    [HarmonyPatch(typeof(SpeechController), nameof(SpeechController.Speak), typeof(string), typeof(bool),typeof(bool), typeof(Human), typeof(SideJob), typeof(Human.InteractionDialogInstance))]
    class SpeechController_Speak
    {
        public static void Prefix(SpeechController __instance, ref string ddsMessage, Human speakAbout)
        {
            if (ddsMessage == "0e443d05-2bd7-4a52-adcb-5957a5d82860")
            {
                if (((Human)__instance.actor).FindAcquaintanceExists(speakAbout, out var returnAcq))
                {
                    if(returnAcq.connections.Any(conn => conn == Acquaintance.ConnectionType.regularCustomer))
                    {
                        if (returnAcq.known >= 0.35f)
                        {
                            // DialogAdditionPlugin.PluginLogger.Log(LogLevel.Warning, "Found a known regular customer, replacing speech");
                            ddsMessage = "d2913883-e343-45f8-b735-3671960e3078";
                            PhotoSelectButtonController_MergeTargetKeys.SkipCount = 1;
                            
                            foreach (Evidence.DataKey dataKey in DialogController.Instance.askTargetKeys)
                            {
                                speakAbout.evidenceEntry.MergeDataKeys(dataKey, Evidence.DataKey.firstName);
                            }
                        }
                        else
                        {
                            // DialogAdditionPlugin.PluginLogger.Log(LogLevel.Warning, "Found an unknown regular customer, replacing speech");
                            ddsMessage = "79450f40-a776-4e00-b676-719fadc99e8b";
                            PhotoSelectButtonController_MergeTargetKeys.SkipCount = 1;
                        }
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PhotoSelectButtonController), nameof(PhotoSelectButtonController.MergeTargetKeys))]
    class PhotoSelectButtonController_MergeTargetKeys
    {
        public static int SkipCount = 0;
        public static bool Prefix(Evidence.DataKey key)
        {
            if (SkipCount > 0)
            {
                SkipCount--;
                return false;
            }
            return true;
        }
    }
}