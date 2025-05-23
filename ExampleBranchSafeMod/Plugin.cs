using BepInEx;
using UnityEngine;
using BepInEx.Logging;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace ExampleBranchSafeMod
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class ExampleBranchSafeModPlugin : BaseUnityPlugin
#elif IL2CPP
    public class ExampleBranchSafeModPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;
#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
            PluginLogger.LogInfo($"I'm in Mono!");
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
            PluginLogger.LogInfo($"I'm in IL2CPP!");
#endif
        }
    }
}