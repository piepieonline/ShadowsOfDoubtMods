using AssetBundleLoader;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NewMurderTypes
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static ManualLogSource Logger;

        public override void Load()
        {
            if (!Config.Bind("General", "Enabled", true).Value)
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is disabled.");
                return;
            }

            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }
    }

    // TODO: Breaking on reload?
    [HarmonyPatch(typeof(Toolbox), "Start")]
    public class Toolbox_Start
    {
        public static void Postfix()
        {
            var murderBundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "BundleContent\\newmurdertypes"), true);
            // TODO
            Toolbox.Instance.allMurderMOs.Clear();
            Toolbox.Instance.allMurderMOs.Add(murderBundle.LoadAsset<MurderMO>("TheftGoneWrong"));
        }
    }

    [HarmonyPatch(typeof(SideJobController), "Start")]
    public class SideJobController_Start
    {
        public static void Prefix()
        {
            var sideJobBundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "BundleContent\\newsidejobtypes"), true);
            // TODO
            Toolbox.Instance.allSideJobs.Clear();
            Toolbox.Instance.allSideJobs.Add(sideJobBundle.LoadAsset<JobPreset>("AnonVmailThreat"));
        }
    }

    [HarmonyPatch(typeof(MurderController), "Update")]
    public class MurderController_Update
    {
        public static void Postfix()
        {
            if (Murder_SetMurderState.murderIsExecuting)
            {
                Murder_SetMurderState.murderDuration += SessionData.Instance.gameTimePassedThisFrame;

                if (Murder_SetMurderState.murderDuration > 60)
                {
                    Plugin.Logger.LogInfo($"Time passed: {Time.time} - {Murder_SetMurderState.murderDuration}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(MurderController.Murder), "SetMurderState")]
    public class Murder_SetMurderState
    {
        public static bool murderIsExecuting;
        public static double murderDuration;

        public static void Postfix(MurderController.MurderState newState)
        {
            if (newState == MurderController.MurderState.executing)
            {
                murderIsExecuting = true;
                murderDuration = 0;
                Plugin.Logger.LogInfo($"Time started: {Time.time}");
            }
            else
            {
                if(murderIsExecuting)
                    Plugin.Logger.LogInfo($"Time ended: {Time.time} - {murderDuration}");
                murderIsExecuting = false;
            }
        }
    }
}
