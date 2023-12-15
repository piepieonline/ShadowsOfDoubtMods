using BepInEx;
using UnityEngine;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace ExampleBranchSafeMod
{
#if MONO
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class ExampleBranchSafeModPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo($"I'm in Mono2!");
        }
    }
#elif IL2CPP
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class ExampleBranchSafeModPlugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo($"I'm in IL2CPP2!");
    }
}
#endif
}