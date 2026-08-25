using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AnyCitySize
{
    internal class AnyCitySizeHooks
    {
        static class CitySizeInput
        {
            static int selectedX, selectedY;

            static TMPro.TextMeshProUGUI mainMenuLabel;
            static TMPro.TextMeshProUGUI cityEditorLabel;

            static PopupMessageController.LeftButton popupLeftCallbackCache;
            static PopupMessageController.RightButton popupRightCallbackCache;

            public static bool MainMenuInitialised => mainMenuLabel != null;
            public static bool CityEditorInitialised => cityEditorLabel != null;

            public static void InitialiseMainMenu()
            {
                if (mainMenuLabel != null) return;

                var menuCanvas = GameObject.Find("MenuCanvas");
                if (menuCanvas == null) return;

                var componentsRoot = menuCanvas.transform.Find("MainMenu/GenerateCityPanel/GenerateNewCityComponents");
                if (componentsRoot == null) return;

                var inputTemplate = componentsRoot.Find("CityNameInput");
                var sizeDropdown = componentsRoot.Find("SizeDropdown");
                if (inputTemplate == null || sizeDropdown == null) return;

                AnyCitySizePlugin.Logger.LogInfo("Modifying the Generate City menu");

                mainMenuLabel = ReplaceSizeDropdown(inputTemplate.gameObject, sizeDropdown);
                UpdateMenuText();
            }

            public static void InitialiseCityEditor(PrototypeDebugPanel panel)
            {
                if (cityEditorLabel != null) return;
                if (panel.citySizeDropdownController == null || panel.cityNameInputButton == null) return;

                var sizeDropdown = panel.citySizeDropdownController.transform;
                var componentsRoot = sizeDropdown.parent;

                var inputTemplate = panel.cityNameInputButton.transform;
                while (inputTemplate.parent != null && inputTemplate.parent != componentsRoot)
                {
                    inputTemplate = inputTemplate.parent;
                }
                if (inputTemplate.parent == null) return;

                AnyCitySizePlugin.Logger.LogInfo("Modifying the City Editor panel");

                cityEditorLabel = ReplaceSizeDropdown(inputTemplate.gameObject, sizeDropdown);
                UpdateMenuText();
            }

            static TMPro.TextMeshProUGUI ReplaceSizeDropdown(GameObject inputTemplate, Transform sizeDropdown)
            {
                var newInputBox = GameObject.Instantiate(inputTemplate);
                newInputBox.name = "AnyCitySizeInput";
                newInputBox.SetActive(true);

                var labelText = newInputBox.transform.Find("LabelText");
                if (labelText != null) labelText.GetComponent<TMPro.TextMeshProUGUI>().SetText("Size");

                // Remove the randomize button
                var buttonArea = newInputBox.transform.Find("ButtonArea");
                if (buttonArea != null) buttonArea.gameObject.SetActive(false);

                var newInputBoxButton = newInputBox.GetComponentInChildren<Button>();
                // Instantiate copies the template's persistent listeners, which would open the city name popup as well
                newInputBoxButton.onClick = new Button.ButtonClickedEvent();
                newInputBoxButton.onClick.AddListener((Action)OpenSizePopup);

                var newInputBoxButtonController = newInputBoxButton.GetComponent<ButtonController>();
                if (newInputBoxButtonController != null) newInputBoxButtonController.useAutomaticText = false;

                sizeDropdown.gameObject.SetActive(false);

                newInputBox.transform.SetParent(sizeDropdown.parent, true);
                newInputBox.transform.SetSiblingIndex(sizeDropdown.GetSiblingIndex());

                return newInputBoxButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            }

            static void OpenSizePopup()
            {
                popupLeftCallbackCache = PopupMessageController.Instance.OnLeftButton;
                popupRightCallbackCache = PopupMessageController.Instance.OnRightButton;

                PopupMessageController.Instance.OnLeftButton = (PopupMessageController.LeftButton)HandlePopupCancel;
                PopupMessageController.Instance.OnRightButton = (PopupMessageController.RightButton)HandlePopupSubmit;
                PopupMessageController.Instance.PopupMessage(
                    "citySize",
                    true,
                    true,
                    RButton: "Confirm",
                    enableInputField: true,
                    inputFieldDefault: $"{RestartSafeController.Instance.cityX - 2}x{RestartSafeController.Instance.cityY - 2}",
                    mainTextPreWrittenOverride: "Enter the city size, as WIDTHxHEIGHT.");

                PopupMessageController.Instance.titleText.SetText("City Size");
            }

            static void HandlePopupSubmit()
            {
                string enteredValue = PopupMessageController.Instance.inputField.text;
                RestorePopupCallbacks();

                if ((new System.Text.RegularExpressions.Regex("^\\d+x\\d+$")).Match(enteredValue).Length == 0) return;
                AnyCitySizePlugin.Logger.LogInfo($"Setting value to: {enteredValue}");

                var newSize = enteredValue.Split("x").Select(value => int.Parse(value)).ToList();

                RestartSafeController.Instance.cityX = 2 + newSize[0];
                RestartSafeController.Instance.cityY = 2 + newSize[1];

                UpdateMenuText();
            }

            static void HandlePopupCancel()
            {
                RestorePopupCallbacks();
            }

            static void RestorePopupCallbacks()
            {
                PopupMessageController.Instance.OnLeftButton = popupLeftCallbackCache;
                PopupMessageController.Instance.OnRightButton = popupRightCallbackCache;
            }

            public static void UpdateMenuText()
            {
                selectedX = RestartSafeController.Instance.cityX;
                selectedY = RestartSafeController.Instance.cityY;

                var sizeText = $"Width: {selectedX - 2} Height: {selectedY - 2}";
                if (mainMenuLabel != null) mainMenuLabel.SetText(sizeText);
                if (cityEditorLabel != null) cityEditorLabel.SetText(sizeText);
            }

            public static bool SizeChangedExternally()
            {
                return RestartSafeController.Instance.cityX != selectedX || RestartSafeController.Instance.cityY != selectedY;
            }

            public static void RestoreSelectedSize()
            {
                RestartSafeController.Instance.cityX = selectedX;
                RestartSafeController.Instance.cityY = selectedY;
                UpdateMenuText();
            }
        }

        [HarmonyPatch(typeof(MainMenuController), "Start")]
        public class MainMenuController_Start
        {
            public static void Postfix()
            {
                CitySizeInput.InitialiseMainMenu();
            }
        }

        // Fallback, in case the sizes get out of sync somehow
        [HarmonyPatch(typeof(MainMenuController), "Update")]
        public class MainMenuController_Update
        {
            public static void Postfix(MainMenuController __instance)
            {
                if (__instance.mainMenuActive && CitySizeInput.MainMenuInitialised && CitySizeInput.SizeChangedExternally())
                {
                    CitySizeInput.UpdateMenuText();
                }
            }
        }

        // When we update the other options, we need to persist the size
        [HarmonyPatch(typeof(MainMenuController), "OnChangeCityGenerationOption")]
        public class MainMenuController_OnChangeCityGenerationOption
        {
            public static void Postfix()
            {
                if (CitySizeInput.MainMenuInitialised && CitySizeInput.SizeChangedExternally())
                {
                    CitySizeInput.RestoreSelectedSize();
                }
            }
        }

        [HarmonyPatch(typeof(PrototypeDebugPanel), "OnEnable")]
        public class PrototypeDebugPanel_OnEnable
        {
            public static void Postfix(PrototypeDebugPanel __instance)
            {
                CitySizeInput.InitialiseCityEditor(__instance);
                CityStatsPanel.Initialise(__instance);
                CityStatsPanel.Refresh();
            }
        }

        // Fallback, in case the sizes get out of sync somehow
        [HarmonyPatch(typeof(PrototypeDebugPanel), "Update")]
        public class PrototypeDebugPanel_Update
        {
            public static void Postfix()
            {
                if (CitySizeInput.CityEditorInitialised && CitySizeInput.SizeChangedExternally())
                {
                    CitySizeInput.UpdateMenuText();
                }
            }
        }

        // When we update the other options, we need to persist the size
        [HarmonyPatch(typeof(PrototypeDebugPanel), "OnChangeCityGenerationOption")]
        public class PrototypeDebugPanel_OnChangeCityGenerationOption
        {
            public static void Postfix()
            {
                if (CitySizeInput.CityEditorInitialised && CitySizeInput.SizeChangedExternally())
                {
                    CitySizeInput.RestoreSelectedSize();
                }
            }
        }

        [HarmonyPatch(typeof(CityEditorController), "OnHaltOnEndOfLoadState")]
        public class CityEditorController_OnHaltOnEndOfLoadState
        {
            public static void Postfix()
            {
                CityStatsPanel.Refresh();
            }
        }

        [HarmonyPatch(typeof(CityEditorController), "ClearCurrentCityEditorData")]
        public class CityEditorController_ClearCurrentCityEditorData
        {
            public static void Postfix()
            {
                CityStatsPanel.Refresh();
            }
        }

        // Swapping a tile's building changes which floor plans the city will be built from
        [HarmonyPatch(typeof(CityEditorBuildingEdit), "OnChangeBuildingType")]
        public class CityEditorBuildingEdit_OnChangeBuildingType
        {
            public static void Postfix()
            {
                CityStatsPanel.Refresh();
            }
        }
    }
}
