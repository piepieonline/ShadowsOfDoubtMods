using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Rewired;
using UnityEngine;

namespace AutoWalk
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class AutoWalk : BasePlugin
    {
        public static ManualLogSource Logger;

        public static ConfigEntry<KeyCode> autoWalkKey;

        public override void Load()
        {
            autoWalkKey = Config.Bind("General", "Hold this key and walk forward to start autowalking", KeyCode.LeftAlt);

            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched and has injected types!");
        }
    }

    [HarmonyPatch(typeof(InputController), nameof(InputController.GetAxisRelative))]
    public class InputController_GetAxisRelative
    {
        static bool isAutoWalking = false;
        static float lastActivationGameTime;
        public static void Postfix(ref float __result, string actionId)
        {
            if(actionId == "MoveVertical")
            {
                if(__result > 0 && Input.GetKey(AutoWalk.autoWalkKey.Value))
                {
                    isAutoWalking = true;
                    lastActivationGameTime = Time.time;
                }
                else if(__result > 0 && isAutoWalking && ((Time.time - lastActivationGameTime) > 0.2f))
                {
                    isAutoWalking = false;
                }
                else if(isAutoWalking)
                {
                    __result = 1f;
                }
            }
        }
    }
}
