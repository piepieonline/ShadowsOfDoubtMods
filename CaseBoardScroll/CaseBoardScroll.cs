using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Rewired;
using UnityEngine;

namespace CaseBoardScroll
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class CaseBoardScroll : BasePlugin
    {
        public static ManualLogSource Logger;

        public static ConfigEntry<float> xScrollSpeed;
        public static ConfigEntry<float> yScrollSpeed;

        public static ConfigEntry<float> minZoomModifier;
        public static ConfigEntry<float> maxZoomModifier;

        public override void Load()
        {
            xScrollSpeed = Config.Bind("General", "Horizontal scroll speed", .15f);
            yScrollSpeed = Config.Bind("General", "Vertical scroll speed", .15f);

            minZoomModifier = Config.Bind("General", "Scroll modifier at min zoom", 5f);
            maxZoomModifier = Config.Bind("General", "Scroll modifier at max zoom", 1f);

            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            ClassInjector.RegisterTypeInIl2Cpp<CaseBoardScrollRectWASD>();
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched and has injected types!");
        }
    }

    [HarmonyPatch(typeof(UnityEngine.UI.ScrollRect), "OnEnable")]
    public class CustomScrollRect_Awake
    {
        public static void Postfix(UnityEngine.UI.ScrollRect __instance)
        {
            if (__instance.name == "CorkBoard" && __instance.GetComponent<CaseBoardScrollRectWASD>() == null)
            {
                __instance.gameObject.AddComponent<CaseBoardScrollRectWASD>();
            }
        }
    }

    public class CaseBoardScrollRectWASD : MonoBehaviour
    {
        CustomScrollRect scrollRect;
        ZoomContent zoomContent;

        void Start()
        {
            scrollRect = GetComponent<CustomScrollRect>();
            zoomContent = GetComponentInChildren<ZoomContent>();
        }

        void LateUpdate()
        {
            var currentSelectedUIObject = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            // Make sure the player isn't entering text into a caseboard UI component
            if (currentSelectedUIObject == null || currentSelectedUIObject.GetComponent<TMPro.TMP_InputField>() == null)
            {
                float zoomScrollFactor = Mathf.Lerp(CaseBoardScroll.minZoomModifier.Value, CaseBoardScroll.maxZoomModifier.Value, zoomContent.normalizedZoom);
                
                float xMax = CaseBoardScroll.xScrollSpeed.Value * zoomScrollFactor * Time.deltaTime;
                float yMax = CaseBoardScroll.yScrollSpeed.Value * zoomScrollFactor * Time.deltaTime;
    
                if (Input.GetKey(KeyCode.W))
                {
                    scrollRect.SetVerticalNormalizedPosition(scrollRect.verticalNormalizedPosition + yMax);
                }
                else if (Input.GetKey(KeyCode.S))
                {
                    scrollRect.SetVerticalNormalizedPosition(scrollRect.verticalNormalizedPosition - yMax);
                }
    
                if (Input.GetKey(KeyCode.A))
                {
                    scrollRect.SetHorizontalNormalizedPosition(scrollRect.horizontalNormalizedPosition - xMax);
                }
                else if (Input.GetKey(KeyCode.D))
                {
                    scrollRect.SetHorizontalNormalizedPosition(scrollRect.horizontalNormalizedPosition + xMax);
                }
            }
        }
    }
}
