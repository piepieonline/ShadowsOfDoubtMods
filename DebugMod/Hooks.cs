using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DebugMod
{
    class Hooks
    {
        [HarmonyPatch(typeof(InterfaceController), "NewGameMessage")]
        public class InterfaceController_NewGameMessage
        {
            public static void Prefix(InterfaceController.GameMessageType newType, string newMessage)
            {
                if(newMessage.ToLower().Contains("[scanner]"))
                {
                    Plugin.Logger.LogInfo($"Murder detected");
                    InterfaceController.Instance.ToggleNotebookButton();
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

                if (__instance.currentBuilding != null)
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

                /*
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
                *

                var newClasses = new List<FurnitureClass>();


                foreach (var a in Resources.FindObjectsOfTypeAll<FurniturePreset>().Where(furniturePreset => furniturePreset.name.Contains("Telev")))
                {
                    // a.prefab = phoneBoothFurniturePreset.prefab;
                    // a.subObjects = phoneBoothFurniturePreset.subObjects;
                    // a.subObjects.Add(phoneBoothFurniturePreset.subObjects[7]);
                    // a.isJobBoard = true;
                    foreach (var b in a.classes)
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

            }
        }

        // Patch to force fix a case?
        [HarmonyPatch(typeof(SideJobController), "Start")]
        public class SideJobController_Start
        {
            public static void Prefix()
            {
                var moddedAssetBundle = UniverseLib.AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "newsidejob"));

                var newSideJob = moddedAssetBundle.LoadAsset<JobPreset>("NewSideJob");
                // var newSideJob = moddedAssetBundle.LoadAsset<JobPreset>("StealAndTossItem");
                Toolbox.Instance.allSideJobs.Clear();
                Toolbox.Instance.allSideJobs.Add(newSideJob);

                Plugin.Logger.LogInfo("Job added");
            }
        }

            // Patch to force fix a case?
        // [HarmonyPatch(typeof(CasePanelController), "SetActiveCase")]
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

        // [HarmonyPatch(typeof(GenerationController), "PickFurniture")]
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

        // [HarmonyPatch(typeof(GenerationController), "GetValidFurniture")]
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
        
        // [HarmonyPatch()]
        public class NameGenerator_GenerateName_Any
        {
            static IEnumerable<System.Reflection.MethodInfo> TargetMethods()
            {
                return typeof(NameGenerator).GetMethods().Where(methodInfo => methodInfo.Name.Contains("Generate"));
            }

            [HarmonyPrefix]
            public static void Prefix()
            {
                Plugin.Logger.LogInfo($"Name Generator");
                var trace = new System.Diagnostics.StackTrace();
                foreach (var frame in trace.GetFrames())
                {
                    var method = frame.GetMethod();
                    if (method.Name.Equals("LogStack")) continue;
                    Plugin.Logger.LogInfo(string.Format("{0}::{1}",
                        method.ReflectedType != null ? method.ReflectedType.Name : string.Empty,
                        method.Name));
                }
            }
        }

        // [HarmonyPatch()]
        public class SystemFileInfo_Read_Any
        {
            static IEnumerable<System.Reflection.MethodInfo> TargetMethods()
            {
                return typeof(System.IO.FileInfo).GetMethods().Where(methodInfo => methodInfo.Name.Contains("Open") || methodInfo.Name.Contains("Read")).Concat(
                        typeof(System.IO.FileStream).GetMethods().Where(methodInfo => methodInfo.Name.Contains("Open") || methodInfo.Name.Contains("Read"))).Concat(
                        typeof(System.IO.File).GetMethods().Where(methodInfo => methodInfo.Name.Contains("Open") || methodInfo.Name.Contains("Read")));
            }

            [HarmonyPrefix]
            public static void Prefix()
            {
                // Plugin.Logger.LogInfo($"File Read System");
                var frame = new System.Diagnostics.StackFrame(1);
                var method = frame.GetMethod();
                var type = method.DeclaringType;
                var name = method.Name;
                Plugin.Logger.LogInfo($"File Read System: {type} - {name}");
            }
        }

        // [HarmonyPatch()]
        public class FileStream_Read
        {
            static IEnumerable<System.Reflection.MethodInfo> TargetMethods()
            {
                return typeof(System.IO.FileStream).GetMethods().Where(methodInfo => methodInfo.Name == "Read");
            }

            [HarmonyPrefix]
            public static void Prefix(System.IO.FileStream __instance)
            {
                Plugin.Logger.LogInfo($"File Read {__instance.Name}");
            }
        }

        // [HarmonyPatch(typeof(ResolveOptionsController), "OnEnable")]
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
    }
}
