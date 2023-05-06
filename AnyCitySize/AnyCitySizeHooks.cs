using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AnyCitySize
{
    internal class AnyCitySizeHooks
    {
        [HarmonyPatch(typeof(MainMenuController), "Start")]
        public class MainMenuController_Start
        {
            public static int selectedX, selectedY;

            static TMPro.TextMeshProUGUI newSizeTMPButtonLabel;

            static PopupMessageController.LeftButton popupLeftCallbackCache;
            static PopupMessageController.RightButton popupRightCallbackCache;

            public static void Postfix()
            {
                if (newSizeTMPButtonLabel != null) return;

                AnyCitySizePlugin.Logger.LogInfo($"Modifying the Generate City menu");

                selectedX = RestartSafeController.Instance.cityX;
                selectedY = RestartSafeController.Instance.cityY;

                // Copy the city name input box as our template
                var inputTemplate = GameObject.Find("MenuCanvas").transform.Find("MainMenu/GenerateCityPanel/GenerateNewCityComponents/CityNameInput").gameObject;

                if (inputTemplate != null)
                {
                    var newInputBox = GameObject.Instantiate(inputTemplate.gameObject);
                    newInputBox.SetActive(true);

                    // Change the label in front
                    newInputBox.transform.Find("LabelText").GetComponent<TMPro.TextMeshProUGUI>().SetText("Size");
                    // Remove the randomize button
                    newInputBox.transform.Find("ButtonArea").gameObject.SetActive(false);

                    var newInputBoxButton = newInputBox.GetComponentInChildren<UnityEngine.UI.Button>();
                    newInputBoxButton.onClick.RemoveAllListeners();
                    newInputBoxButton.onClick.AddListener((Action)(() => {
                        popupLeftCallbackCache = PopupMessageController.Instance.OnLeftButton;
                        popupRightCallbackCache = PopupMessageController.Instance.OnRightButton;

                        PopupMessageController.Instance.inputField.SetText($"{RestartSafeController.Instance.cityX - 2}x{RestartSafeController.Instance.cityY - 2}");
                        PopupMessageController.Instance.OnLeftButton = (PopupMessageController.LeftButton)HandlePopupCancel;
                        PopupMessageController.Instance.OnRightButton = (PopupMessageController.RightButton)HandlePopupSubmit;
                        PopupMessageController.Instance.PopupMessage("Enter city width.", true, true, RButton: "Confirm", enableInputField: true);

                    }));

                    newSizeTMPButtonLabel = newInputBoxButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    UpdateMenuText();

                    // Disable the normal input box
                    inputTemplate.transform.parent.GetChild(1).gameObject.SetActive(false);

                    // Add the new input area
                    newInputBox.transform.SetParent(inputTemplate.transform.parent, true);
                    newInputBox.transform.SetSiblingIndex(1);
                }
            }
            
            // TODO: This is being called on submit for all popups, for unknown reasons
            public static void HandlePopupSubmit()
            {
                PopupMessageController.Instance.OnLeftButton -= (PopupMessageController.LeftButton)HandlePopupCancel;
                PopupMessageController.Instance.OnRightButton -= (PopupMessageController.RightButton)HandlePopupSubmit;
                PopupMessageController.Instance.OnLeftButton = popupLeftCallbackCache;
                PopupMessageController.Instance.OnRightButton = popupRightCallbackCache;

                string enteredValue = PopupMessageController.Instance.inputField.text;

                if ((new System.Text.RegularExpressions.Regex("^\\d+x\\d+$")).Match(enteredValue).Length == 0) return;
                AnyCitySizePlugin.Logger.LogInfo($"Setting value to: {enteredValue}");

                var newSize = enteredValue.Split("x").Select(value => int.Parse(value)).ToList();

                RestartSafeController.Instance.cityX = 2 + newSize[0];
                RestartSafeController.Instance.cityY = 2 + newSize[1];

                UpdateMenuText();
            }

            public static void HandlePopupCancel()
            {
                PopupMessageController.Instance.OnLeftButton -= (PopupMessageController.LeftButton)HandlePopupCancel;
                PopupMessageController.Instance.OnRightButton -= (PopupMessageController.RightButton)HandlePopupSubmit;
                PopupMessageController.Instance.OnLeftButton = popupLeftCallbackCache;
                PopupMessageController.Instance.OnRightButton = popupRightCallbackCache;
            }

            public static void UpdateMenuText()
            {
                if(newSizeTMPButtonLabel != null)
                {
                    selectedX = RestartSafeController.Instance.cityX;
                    selectedY = RestartSafeController.Instance.cityY;

                    newSizeTMPButtonLabel.SetText($"Width: {RestartSafeController.Instance.cityX - 2} Height: {RestartSafeController.Instance.cityY - 2}");
                }
            }

            public static bool IsInitialised()
            {
                return newSizeTMPButtonLabel != null;
            }
        }

        // Fallback, in case the sizes get out of sync somehow
        [HarmonyPatch(typeof(MainMenuController), "Update")]
        public class MainMenuController_Update
        {
            public static void Postfix(MainMenuController __instance)
            {
                if (__instance.mainMenuActive && (RestartSafeController.Instance.cityX != MainMenuController_Start.selectedX || RestartSafeController.Instance.cityY != MainMenuController_Start.selectedY))
                {
                    MainMenuController_Start.UpdateMenuText();
                }
            }
        }

        // When we update the other options, we need to persist the size
        [HarmonyPatch(typeof(MainMenuController), "OnChangeCityGenerationOption")]
        public class MainMenuController_OnChangeCityGenerationOption
        {
            public static void Postfix(MainMenuController __instance)
            {
                if (MainMenuController_Start.IsInitialised() && (RestartSafeController.Instance.cityX != MainMenuController_Start.selectedX || RestartSafeController.Instance.cityY != MainMenuController_Start.selectedY))
                {
                    RestartSafeController.Instance.cityX = MainMenuController_Start.selectedX;
                    RestartSafeController.Instance.cityY = MainMenuController_Start.selectedY;
                    MainMenuController_Start.UpdateMenuText();
                }
            }
        }
    }
}
