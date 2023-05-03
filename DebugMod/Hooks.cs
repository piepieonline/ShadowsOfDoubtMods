using HarmonyLib;
using Il2CppDumper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using UnityEngine;
using static Il2CppSystem.Net.WebCompletionSource;

namespace DebugMod
{
    class Hooks
    {
        // [HarmonyPatch(typeof(Human), "GetCitizenName")]
        public class Citizen_GetCitizenName
        {
            public static void Prefix(Citizen __instance)
            {
                try
                {
                    Plugin.Logger.LogInfo($"ORG: Getting name of {__instance.firstName}");
                }
                catch
                {
                    Plugin.Logger.LogInfo($"Patcher Error!");
                }
            }
        }

        // Works, disabled as unused
        // [HarmonyPatch(typeof(Player), "OnBuildingChange")]
        public class Player_OnBuildingChange
        {
            static GameplayController gameplayController;
            static InterfaceController interfaceController;

            public static void Prefix(Player __instance)
            {
                Plugin.Logger.LogInfo($"Building: {__instance.currentBuilding?.ToString()}");

                if(__instance.currentBuilding != null)
                {
                    foreach (var address in GameplayController.Instance.forSale)
                    {
                        if (address.building == __instance.currentBuilding)
                        {
                            Plugin.Logger.LogInfo($"For Sale: {address.ToString()}");
                            InterfaceController.Instance.NewGameMessage(InterfaceController.GameMessageType.notification, 0, $"For Sale: {address.ToString()}");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MainMenuController), "Start")]
        public class MainMenuController_Start
        {
            static bool hasInit = false;

            public static void Prefix()
            {
                if (hasInit) return;
                hasInit = true;

                var livingRoom = Resources.FindObjectsOfTypeAll<RoomTypeFilter>().Where(roomType => roomType.name == "LivingRoom").FirstOrDefault();
                Plugin.Logger.LogInfo($"1 {livingRoom}");
                var apartmentAddressPreset = Resources.FindObjectsOfTypeAll<AddressPreset>().Where(roomType => roomType.name == "Apartment").FirstOrDefault();
                Plugin.Logger.LogInfo($"2 {apartmentAddressPreset}");
                var phoneBoothFurniturePreset = Resources.FindObjectsOfTypeAll<FurniturePreset>().Where(furniturePreset => furniturePreset.name == "TelephoneBooth").FirstOrDefault();
                Plugin.Logger.LogInfo($"3 {phoneBoothFurniturePreset}");
                var tvFurnitureClass = Resources.FindObjectsOfTypeAll<FurnitureClass>().Where(furnitureClass => furnitureClass.name == "1x1Television").FirstOrDefault();
                Plugin.Logger.LogInfo($"4 {phoneBoothFurniturePreset}");

                /*
                var newFurniturePreset = FurniturePreset.Instantiate(Resources.FindObjectsOfTypeAll<FurniturePreset>().Where(furniturePreset => furniturePreset.name == "ModernTelevison").FirstOrDefault());
                newFurniturePreset.prefab = phoneBoothFurniturePreset.prefab;
                newFurniturePreset.subObjects = phoneBoothFurniturePreset.subObjects;
                newFurniturePreset.isJobBoard = true;

                newFurniturePreset.name = "Hello?";

                Toolbox.Instance.allFurniture.Add(newFurniturePreset);
                */

                var newClasses = new List<FurnitureClass>();

                
                foreach (var a in Resources.FindObjectsOfTypeAll<FurniturePreset>().Where(furniturePreset => furniturePreset.name.Contains("Telev")))
                {
                    // a.prefab = phoneBoothFurniturePreset.prefab;
                    // a.subObjects = phoneBoothFurniturePreset.subObjects;
                    // a.subObjects.Add(phoneBoothFurniturePreset.subObjects[7]);
                    // a.isJobBoard = true;
                    foreach(var b in a.classes)
                    {
                        newClasses.Add(b);
                    }
                    a.classes.Clear();
                }
                
                Plugin.Logger.LogInfo($"5 cleared");

                /// ModernTelevison
                
                /*
                phoneBoothFurniturePreset.allowedInAddressesOfType.Clear();
                foreach(var a in Resources.FindObjectsOfTypeAll<AddressPreset>().ToList())
                {
                    phoneBoothFurniturePreset.allowedInAddressesOfType.Add(a);
                }
                // phoneBoothFurniturePreset.allowedInAddressesOfType.Add(apartmentAddressPreset);


                phoneBoothFurniturePreset.allowedRoomFilters.Clear();
                foreach(var a in Resources.FindObjectsOfTypeAll<RoomTypeFilter>())
                {
                    phoneBoothFurniturePreset.allowedRoomFilters.Add(a);
                }
                // phoneBoothFurniturePreset.allowedRoomFilters.Add(livingRoom);


                // phoneBoothFurniturePreset.classes.Clear();

                foreach(var a in newClasses)
                {
                    phoneBoothFurniturePreset.classes.Add(a);
                }
                // phoneBoothFurniturePreset.classes.Add(tvFurnitureClass);

                phoneBoothFurniturePreset.onlyAllowInFollowing = false;
                */
                Plugin.Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} has custom assets!");



                /// Custom Cruncher App
                Plugin.Logger.LogInfo($"Custom Cruncher Start");

                var newtestassets = UniverseLib.AssetBundle.LoadFromFile("E:\\SteamLibrary\\steamapps\\common\\Shadows of Doubt\\Shadows of Doubt_Data\\customcontent");

                // This is literally the scriptableobject the game stores these in, so that works
                foreach(var cruncher in Resources.FindObjectsOfTypeAll<InteractablePreset>().Where(preset => preset.name.Contains("Cruncher")))
                {
                    cruncher.additionalApps.Insert(cruncher.additionalApps.Count - 2, DebugMod.Plugin.customAssets.LoadAsset<CruncherAppPreset>("ForSale"));
                    cruncher.additionalApps.Insert(cruncher.additionalApps.Count - 2, newtestassets.LoadAsset<CruncherAppPreset>("TestForSaleApp"));
                }

                // CustomCruncherApp.myPreset = employeeDB; // CruncherAppPreset.Instantiate(employeeDB);
                CustomCruncherApp.myPreset = DebugMod.Plugin.customAssets.LoadAsset<CruncherAppPreset>("ForSale");

                // CustomCruncherApp.myPreset.name = "Custom DB";
                CustomCruncherApp.myPreset.openOnEnd = Resources.FindObjectsOfTypeAll<CruncherAppPreset>().Where(res => res.name.Contains("Desktop")).FirstOrDefault();

                CustomCruncherApp.myPreset.appContent[0].AddComponent<CustomCruncherApp>();

                /*
                var oldDB = CustomCruncherApp.myPreset.appContent[0].GetComponent<DatabaseApp>();
                var newDB = CustomCruncherApp.myPreset.appContent[0].AddComponent<CustomDatabaseApp>();
                
                newDB.titleText = oldDB.titleText;
                newDB.searchText = oldDB.searchText;
                newDB.list = oldDB.list;
                newDB.printButton = oldDB.printButton;
                newDB.searchString = oldDB.searchString;
                newDB.ddsPrintout = oldDB.ddsPrintout;
                newDB.citizenPool = oldDB.citizenPool;

                UnityEngine.Object.Destroy(oldDB);
                */
                
                Plugin.customAssets.LoadAllAssets();

                Plugin.Logger.LogInfo($"Custom Cruncher End");
            }
        }

        // Patch to force fix a case?
        [HarmonyPatch(typeof(CasePanelController), "SetActiveCase")]
        public class CasePanelController_SetActiveCase
        {
            public static void Prefix(CasePanelController __instance, Case newCase)
            {
                try
                {
                    Plugin.Logger.LogInfo($"Previous: {__instance.activeCase?.name}");
                    Plugin.Logger.LogInfo($"New: Getting name of {newCase?.name}");
                }
                catch
                {
                    Plugin.Logger.LogInfo($"SetActive Error!");
                }
            }
        }

        [HarmonyPatch(typeof(GenerationController), "PickFurniture")]
        public class GenerationController_PickFurniture
        {
            public static int count = 0;
            public static void Prefix(GenerationController __instance, FurnitureClass furnClass, NewRoom room)
            {
                if (count < 1 && furnClass.name.Contains("elev"))
                {
                    Plugin.Logger.LogInfo($"Looking for {furnClass.name}");
                    count++;
                }
            }
        }

        [HarmonyPatch(typeof(GenerationController), "GetValidFurniture")]
        public class GenerationController_GetValidFurniture
        {
            public static int count = 0;
            public static void Postfix(GenerationController __instance, FurnitureClass furnClass, NewRoom room, bool returnList, List<FurniturePreset> possibleFurniture)
            {
                if(count < 1 && returnList && furnClass.name.Contains("elev"))
                {
                    Plugin.Logger.LogInfo($"Looking for {furnClass.name}, found {possibleFurniture.Count}");
                    count++;
                }
            }
        }

        [HarmonyPatch(typeof(ResolveOptionsController), "OnEnable")]
        public class ResolveOptionsController_OnEnable
        {
            public static GameObject forceResolveButton;

            public static void Postfix(ResolveOptionsController __instance)
            {
                Plugin.Logger.LogInfo("Checking button");
                if (forceResolveButton == null)
                {
                    forceResolveButton = GameObject.Instantiate(__instance.transform.Find("ResolveOptionsPage/HelpButton").gameObject);

                    forceResolveButton.name = "ForceResolveButton";
                    forceResolveButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "Force Resolve";

                    forceResolveButton.transform.SetParent(__instance.transform.Find("ResolveOptionsPage"), true);
                    forceResolveButton.transform.position = new Vector3(forceResolveButton.transform.position.x, 500, 0);

                    forceResolveButton.GetComponent<ButtonController>().enabled = false;
                    // forceResolveButton.GetComponent<ButtonController>().OnPress. = null;

                    forceResolveButton.GetComponent<UnityEngine.UI.Button>().onClick.RemoveAllListeners(); //  () => { Plugin.Logger.LogInfo("click"); }
                    Action onClickHandlerB = () => { Plugin.Logger.LogInfo("click"); };
                    forceResolveButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(onClickHandlerB); //  () => { Plugin.Logger.LogInfo("click"); }

                    Plugin.Logger.LogInfo("Button added");
                }
            }

            static void TaskOnClick()
            {
                Debug.Log("You have clicked the button!");
            }
        }

        /*
        [HarmonyPatch(typeof(DatabaseApp), "OnPrintEntry")]
        public class DatabaseApp_OnPrintEntry
        {
            public static void Prefix(DatabaseApp __instance)
            {
                __instance.ddsPrintout = DatabaseApp_UpdateSearch.optionToAddressMap[__instance.list.selected.option.text].saleNote.preset;
            }
        }
        */
        
        public class AddressOption : ComputerOSMultiSelect.OSMultiOption
        {
            public NewAddress address;
        }

        [HarmonyPatch(typeof(ComputerController), "OnClickOnOSElement")]
        public class ComputerController_OnClickOnOSElement
        {
            public static void Prefix(ComputerOSUIComponent c)
            {
                Debug.Log($"Clicked: {c.name}");
            }
        }

        [HarmonyPatch(typeof(DesktopApp), "UpdateIcons")]
        public class DesktopApp_UpdateIcons
        {
            public static void Prefix(DesktopApp __instance)
            {
                Debug.Log($"Updating icons");
            }
        }
    }
}
