using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
using Il2CppSystem.Runtime.InteropServices;
#endif

namespace DDSLoader
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class DDSLoaderPlugin : BaseUnityPlugin
#elif IL2CPP
    public class DDSLoaderPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        public static List<DirectoryInfo> modsToLoadFrom = new List<DirectoryInfo>();

        public static ConfigEntry<bool> debugLogConversations;
        public static ConfigEntry<string> debugPauseTreeGUID;
        public static ConfigEntry<bool> debugClearNewspaperArticles;

        public static ConfigEntry<bool> debugPrintLoadedStrings;

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            // Plugin startup logic
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

            debugLogConversations = Config.Bind("Debug", "Enable debugging of conversations", false);
            debugPauseTreeGUID = Config.Bind("Debug", "Pause when this conversation tree GUID begins", "");
            debugClearNewspaperArticles = Config.Bind("Debug", "Clear existing newspaper articles", false);

            debugPrintLoadedStrings = Config.Bind("Debug", "Log loaded strings to the console", false);

            // Load all folders named DDSContent (includes subfolders), unless they have a disable file
            modsToLoadFrom = Directory.GetDirectories(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), ".."), "DDSContent", SearchOption.AllDirectories)
                .Select(dirPath => new DirectoryInfo(dirPath))
                .Where(dir => !File.Exists(Path.Combine(dir.Parent.FullName, "disabled.txt")))
                .ToList();
        }
    }
}
