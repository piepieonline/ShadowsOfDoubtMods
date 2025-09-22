using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using SOD.Common.BepInEx;
using UnityEngine;

namespace TheftObjectiveColours
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class TheftObjectiveColoursPlugin : PluginController<TheftObjectiveColoursPlugin>
    {
        private static ConfigEntry<bool> _enabled;

        public override void Load()
        {
            // Plugin startup logic

            _enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required)");
            
            if (_enabled.Value)
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
            }
        }
        
        public static Texture2D OverlayTexture = null;
        public static readonly Texture2D[] CachedTextures = new Texture2D[19];

        public static readonly Color[] Colours =
        [
            new Color(0.902f, 0.098f, 0.294f, 1f), // Red
            new Color(0.235f, 0.706f, 0.294f, 1f), // Green
            new Color(1f, 0.882f, 0.098f, 1f), // Yellow
            new Color(0f, 0.510f, 0.784f, 1f), // Blue
            new Color(0.961f, 0.510f, 0.188f, 1f), // Orange
            new Color(0.569f, 0.118f, 0.706f, 1f), // Purple
            new Color(0.275f, 0.941f, 0.941f, 1f), // Cyan
            new Color(0.941f, 0.196f, 0.902f, 1f), // Magenta
            new Color(0.824f, 0.961f, 0.235f, 1f), // Lime
            new Color(0.980f, 0.745f, 0.831f, 1f), // Pink
            new Color(0f, 0.502f, 0.502f, 1f), // Teal
            new Color(0.863f, 0.745f, 1f, 1f), // Lavender
            new Color(1f, 0.980f, 0.784f, 1f), // Beige
            new Color(0.502f, 0f, 0f, 1f), // Maroon
            new Color(0.667f, 1f, 0.765f, 1f), // Mint
            new Color(0.502f, 0.502f, 0f, 1f), // Olive
            new Color(1f, 0.843f, 0.706f, 1f), // Apricot
            new Color(0f, 0f, 0.502f, 1f), // Navy
            new Color(0.502f, 0.502f, 0.502f, 1f) // Grey
        ];

        public static readonly string[] ColourNames =
        [
            "red",
            "green",
            "yellow",
            "blue",
            "orange",
            "purple",
            "cyan",
            "magenta",
            "lime",
            "pink",
            "teal",
            "lavender",
            "beige",
            "maroon",
            "mint",
            "olive",
            "apricot",
            "navy",
            "grey"
        ];
    }
}
