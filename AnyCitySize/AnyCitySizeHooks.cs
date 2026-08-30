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
            const int borderTiles = 2;
            const int minimumTiles = 1;

            const float heightSpacing = 10f;
            const float stepWidth = 34f;
            const float iconSize = 20f;
            const float controlSpacing = 4f;

            const string editFallback = "...";

            static int selectedX, selectedY;

            static SizeRow mainMenuRow;
            static SizeRow cityEditorRow;

            static int lastStepFrame = -1;

            static Sprite editIconSprite;
            static Material editIconMaterial;
            static Color editIconColor = Color.white;

            static PopupMessageController.LeftButton popupLeftCallbackCache;
            static PopupMessageController.RightButton popupRightCallbackCache;

            class SizeRow
            {
                public TMPro.TextMeshProUGUI width;
                public TMPro.TextMeshProUGUI height;
            }

            public static bool MainMenuInitialised => mainMenuRow != null;
            public static bool CityEditorInitialised => cityEditorRow != null;

            public static void InitialiseMainMenu()
            {
                if (mainMenuRow != null) return;

                var menuCanvas = GameObject.Find("MenuCanvas");
                if (menuCanvas == null) return;

                var componentsRoot = menuCanvas.transform.Find("MainMenu/GenerateCityPanel/GenerateNewCityComponents");
                if (componentsRoot == null) return;

                var inputTemplate = componentsRoot.Find("CityNameInput");
                var sizeDropdown = componentsRoot.Find("SizeDropdown");
                if (inputTemplate == null || sizeDropdown == null) return;

                AnyCitySizePlugin.Logger.LogInfo("Modifying the Generate City menu");

                mainMenuRow = ReplaceSizeDropdown(inputTemplate.gameObject, sizeDropdown, true);
                UpdateMenuText();
            }

            public static void InitialiseCityEditor(PrototypeDebugPanel panel)
            {
                if (cityEditorRow != null) return;
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

                cityEditorRow = ReplaceSizeDropdown(inputTemplate.gameObject, sizeDropdown, false);
                UpdateMenuText();
            }

            static SizeRow ReplaceSizeDropdown(GameObject inputTemplate, Transform sizeDropdown, bool showLabel)
            {
                var row = GameObject.Instantiate(inputTemplate);
                row.name = "AnyCitySizeInput";
                row.SetActive(true);

                var label = row.transform.Find("LabelText");
                if (label != null)
                {
                    label.GetComponent<TMPro.TextMeshProUGUI>().SetText("Size");
                    label.gameObject.SetActive(showLabel);
                }

                // Remove the randomize button
                var buttonArea = row.transform.Find("ButtonArea");
                if (buttonArea != null) buttonArea.gameObject.SetActive(false);

                var templateButton = row.GetComponentInChildren<Button>();
                if (templateButton == null) return null;

                var templateText = FindButtonText(templateButton);
                var textTemplate = label != null ? label.gameObject : (templateText != null ? templateText.gameObject : null);
                if (textTemplate == null) return null;

                var buttonTemplate = GameObject.Instantiate(templateButton.gameObject);
                buttonTemplate.SetActive(false);
                TrimToLabel(buttonTemplate);

                // Every control is a copy of the template button, so the original is left as an inert backdrop
                templateButton.onClick = new Button.ButtonClickedEvent();
                if (templateText != null) templateText.gameObject.SetActive(false);
                if (templateButton.gameObject != row) templateButton.gameObject.SetActive(false);

                LayOutRowHorizontally(row.transform);

                var controls = AddControlsArea(row.transform);

                CacheEditIcon(sizeDropdown.parent);

                var sizeRow = new SizeRow();
                sizeRow.width = AddValueText(controls, textTemplate);
                AddStepButton(controls, buttonTemplate, "-", (Action)DecreaseWidth);
                AddStepButton(controls, buttonTemplate, "+", (Action)IncreaseWidth);
                AddSpacer(controls, heightSpacing);
                sizeRow.height = AddValueText(controls, textTemplate);
                AddStepButton(controls, buttonTemplate, "-", (Action)DecreaseHeight);
                AddStepButton(controls, buttonTemplate, "+", (Action)IncreaseHeight);

                var editButton = AddStepButton(controls, buttonTemplate, editFallback, (Action)OpenSizePopup);
                ApplyIcon(editButton);

                GameObject.DestroyImmediate(buttonTemplate);

                sizeDropdown.gameObject.SetActive(false);

                row.transform.SetParent(sizeDropdown.parent, true);
                row.transform.SetSiblingIndex(sizeDropdown.GetSiblingIndex());

                var rowRect = row.GetComponent<RectTransform>();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);

                MakeButtonsSquare(controls);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);

                return sizeRow;
            }

            // The label keeps the row's own anchoring, which lines it up with the menu's other labels for free
            static void LayOutRowHorizontally(Transform row)
            {
                for (int i = 0; i < row.childCount; i++)
                {
                    var child = row.GetChild(i);

                    var element = child.GetComponent<LayoutElement>();
                    if (element == null) element = child.gameObject.AddComponent<LayoutElement>();
                    element.ignoreLayout = true;
                }

                ApplyHorizontalLayout(row.gameObject, TextAnchor.MiddleLeft, 0f);
            }

            // Filling the row and packing to the right leaves the slack in front of the width, and the buttons on the gutter
            static Transform AddControlsArea(Transform row)
            {
                var controls = new GameObject("AnyCitySizeControls");
                controls.layer = row.gameObject.layer;
                controls.AddComponent<RectTransform>();
                controls.transform.SetParent(row, false);

                ApplyHorizontalLayout(controls, TextAnchor.MiddleRight, controlSpacing);

                var element = controls.AddComponent<LayoutElement>();
                element.minWidth = 0f;
                element.preferredWidth = 0f;
                element.flexibleWidth = 1f;

                return controls.transform;
            }

            static void ApplyHorizontalLayout(GameObject target, TextAnchor alignment, float spacing)
            {
                var layout = target.GetComponent<HorizontalLayoutGroup>();
                if (layout == null) layout = target.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = alignment;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                layout.spacing = spacing;
            }

            static TMPro.TextMeshProUGUI AddValueText(Transform row, GameObject textTemplate)
            {
                var value = GameObject.Instantiate(textTemplate, row);
                value.name = "AnyCitySizeValue";
                value.SetActive(true);

                var autoText = value.GetComponent<MenuAutoTextController>();
                if (autoText != null) GameObject.DestroyImmediate(autoText);

                var text = value.GetComponent<TMPro.TextMeshProUGUI>();
                text.alignment = TMPro.TextAlignmentOptions.Left;
                text.enableWordWrapping = false;

                // A narrow panel shrinks the text rather than letting it run under the buttons
                text.enableAutoSizing = true;
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = 8f;

                SetElementWidth(value, -1f, -1f);

                return text;
            }

            static GameObject AddStepButton(Transform row, GameObject buttonTemplate, string buttonLabel, Action onClick)
            {
                var stepButton = GameObject.Instantiate(buttonTemplate, row);
                stepButton.name = "AnyCitySizeStep";
                stepButton.SetActive(true);

                var button = stepButton.GetComponent<Button>();

                var controller = button.GetComponent<ButtonController>();
                if (controller != null)
                {
                    controller.useAutomaticText = false;
                    controller.button = button;
                    controller.SetInteractable(true);
                }

                var text = FindButtonText(button);
                if (text != null)
                {
                    text.gameObject.SetActive(true);
                    text.alignment = TMPro.TextAlignmentOptions.Center;
                    text.SetText(buttonLabel);
                }

                // Instantiate copies the template's persistent listeners, which would open the city name popup as well
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(onClick);

                SetElementWidth(stepButton, stepWidth, stepWidth);

                return stepButton;
            }

            // Only the main menu has the custom seed button whose pencil this borrows, so the city editor reuses what it found
            static void CacheEditIcon(Transform componentsRoot)
            {
                if (editIconSprite != null) return;

                var customShareCode = FindDescendant(componentsRoot.root, "CustomShareCode");
                if (customShareCode == null) return;

                var controller = customShareCode.GetComponent<ButtonController>();
                var source = controller != null && controller.icon != null ? controller.icon : null;
                if (source == null)
                {
                    var iconObject = FindDescendant(customShareCode, "Icon");
                    if (iconObject != null) source = iconObject.GetComponent<Image>();
                }
                if (source == null || source.sprite == null) return;

                editIconSprite = source.sprite;
                editIconMaterial = source.material;
                editIconColor = source.color;
            }

            static Transform FindDescendant(Transform root, string name)
            {
                foreach (var descendant in root.GetComponentsInChildren<Transform>(true))
                {
                    if (descendant.name == name) return descendant;
                }

                return null;
            }

            static void ApplyIcon(GameObject stepButton)
            {
                if (editIconSprite == null) return;

                var text = FindButtonText(stepButton.GetComponent<Button>());
                if (text != null) text.SetText(string.Empty);

                var iconObject = new GameObject("AnyCitySizeIcon");
                iconObject.layer = stepButton.layer;

                var iconRect = iconObject.AddComponent<RectTransform>();
                iconObject.transform.SetParent(stepButton.transform, false);
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);

                var icon = iconObject.AddComponent<Image>();
                icon.sprite = editIconSprite;
                icon.material = editIconMaterial;
                icon.color = editIconColor;
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                iconObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }

            // A copy only needs its own label, so any nested text or button of the template's is dropped
            static void TrimToLabel(GameObject buttonTemplate)
            {
                var keep = FindButtonText(buttonTemplate.GetComponent<Button>());

                for (int i = 0; i < buttonTemplate.transform.childCount; i++)
                {
                    var child = buttonTemplate.transform.GetChild(i);
                    if (keep != null && (child == keep.transform || keep.transform.IsChildOf(child))) continue;
                    if (child.GetComponentInChildren<TMPro.TextMeshProUGUI>(true) == null && child.GetComponentInChildren<Button>(true) == null) continue;

                    child.gameObject.SetActive(false);
                }
            }

            static TMPro.TextMeshProUGUI FindButtonText(Button button)
            {
                if (button == null) return null;

                var controller = button.GetComponent<ButtonController>();
                if (controller != null && controller.text != null) return controller.text;

                return button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            }

            static void AddSpacer(Transform controls, float width)
            {
                var spacer = new GameObject("AnyCitySizeSpacer");
                spacer.layer = controls.gameObject.layer;
                spacer.AddComponent<RectTransform>();
                spacer.transform.SetParent(controls, false);

                SetElementWidth(spacer, width, width);
            }

            // A button is only as tall as the row lets it be, so its own laid out height is what squares it off
            static void MakeButtonsSquare(Transform controls)
            {
                for (int i = 0; i < controls.childCount; i++)
                {
                    var child = controls.GetChild(i);
                    if (child.GetComponent<Button>() == null) continue;

                    var size = child.GetComponent<RectTransform>().rect.height;
                    if (size <= 0f) size = stepWidth;

                    SetElementWidth(child.gameObject, size, size);
                }
            }

            static void SetElementWidth(GameObject target, float preferred, float minimum)
            {
                var element = target.GetComponent<LayoutElement>();
                if (element == null) element = target.AddComponent<LayoutElement>();
                element.ignoreLayout = false;
                element.minWidth = minimum;
                element.preferredWidth = preferred;
                element.flexibleWidth = 0f;
            }

            static void DecreaseWidth() => StepSize(-1, 0);
            static void IncreaseWidth() => StepSize(1, 0);
            static void DecreaseHeight() => StepSize(0, -1);
            static void IncreaseHeight() => StepSize(0, 1);

            static void StepSize(int stepX, int stepY)
            {
                // Both Button and ButtonController invoke onClick, so a listener added at runtime fires twice per press
                if (lastStepFrame == Time.frameCount) return;
                lastStepFrame = Time.frameCount;

                var smallest = minimumTiles + borderTiles;
                RestartSafeController.Instance.cityX = Mathf.Max(smallest, RestartSafeController.Instance.cityX + stepX);
                RestartSafeController.Instance.cityY = Mathf.Max(smallest, RestartSafeController.Instance.cityY + stepY);

                UpdateMenuText();
            }

            static void OpenSizePopup()
            {
                if (lastStepFrame == Time.frameCount) return;
                lastStepFrame = Time.frameCount;

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
                    inputFieldDefault: $"{RestartSafeController.Instance.cityX - borderTiles}x{RestartSafeController.Instance.cityY - borderTiles}",
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

                RestartSafeController.Instance.cityX = Mathf.Max(minimumTiles, newSize[0]) + borderTiles;
                RestartSafeController.Instance.cityY = Mathf.Max(minimumTiles, newSize[1]) + borderTiles;

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

                SetRowText(mainMenuRow);
                SetRowText(cityEditorRow);
            }

            static void SetRowText(SizeRow row)
            {
                if (row == null) return;

                row.width.SetText($"Width: {selectedX - borderTiles}");
                row.height.SetText($"Height: {selectedY - borderTiles}");
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
