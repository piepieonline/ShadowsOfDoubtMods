using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace DebugMod
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static ManualLogSource Logger;

        public static UniverseLib.AssetBundle customAssets;
        public static UnityEngine.Object[] allCustomAssets;

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

            // ClassInjector.RegisterTypeInIl2Cpp<MonoTest>();

            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} has added custom types!");

            Plugin.Logger.LogInfo("Loading custom asset bundle start");

            /*
            Plugin.customAssets = UniverseLib.AssetBundle.LoadFromFile("D:\\UnityDev\\ShadowsOfDoubtModding\\Assets\\AssetBundles\\debugmodassets");
            // Plugin.customAssets = UniverseLib.AssetBundle.LoadFromFile("E:\\SteamLibrary\\steamapps\\common\\Shadows of Doubt\\Shadows of Doubt_Data\\debugmodassets");
            if (Plugin.customAssets == null)
            {
                Plugin.Logger.LogInfo("Failed to load AssetBundle!");
            }

            Plugin.allCustomAssets = Plugin.customAssets.LoadAllAssets();
            */


            Plugin.Logger.LogInfo("Loading custom asset bundle complete");
        }

        public static void Assets()
        {
            var bedroom = Resources.FindObjectsOfTypeAll<RoomTypeFilter>().Where(roomType => roomType.name == "LivingRoom").FirstOrDefault();
            Plugin.Logger.LogInfo($"1 {bedroom}");
            var apartmentAddressPreset = Resources.FindObjectsOfTypeAll<AddressPreset>().Where(roomType => roomType.name == "Apartment").FirstOrDefault();
            Plugin.Logger.LogInfo($"2 {apartmentAddressPreset}");
            var phoneBoothFurniturePreset = Resources.FindObjectsOfTypeAll<FurniturePreset>().Where(furniturePreset => furniturePreset.name == "TelephoneBooth").FirstOrDefault();
            Plugin.Logger.LogInfo($"3 {phoneBoothFurniturePreset}");
            var tvFurnitureClass = Resources.FindObjectsOfTypeAll<FurnitureClass>().Where(furnitureClass => furnitureClass.name == "1x1Television").FirstOrDefault();
            Plugin.Logger.LogInfo($"4 {phoneBoothFurniturePreset}");

            phoneBoothFurniturePreset.allowedInAddressesOfType.Add(apartmentAddressPreset);
            phoneBoothFurniturePreset.allowedRoomFilters.Add(bedroom);
            phoneBoothFurniturePreset.classes.Add(tvFurnitureClass);

            Plugin.Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} has custom assets!");
        }

        public static T FindByTypeAndName<T>(string name) where T : UnityEngine.Object
        {
            if (!typeof(UnityEngine.GameObject).IsAssignableFrom(typeof(T)))
                throw new System.ArgumentException();

            return Resources.FindObjectsOfTypeAll<T>().Where(obj => obj.name == name).FirstOrDefault();
        }

        public static Company FindNearestThatSells(string itemName)
        {
            // return Toolbox.Instance.FindNearestThatSells(FindByTypeAndName<InteractablePreset>(itemName), Resources.FindObjectsOfTypeAll<Player>()[0].currentGameLocation);
            return Toolbox.Instance.FindNearestThatSells(Resources.FindObjectsOfTypeAll<InteractablePreset>().Where(obj => obj.name == itemName).FirstOrDefault(), Resources.FindObjectsOfTypeAll<Player>()[0].currentGameLocation);
        }

        public static void CreateTestCube()
        {
            GameObject.Instantiate(customAssets.LoadAsset<GameObject>("TestCube"), Player.Instance.transform.position, Quaternion.identity);
        }
    }
}