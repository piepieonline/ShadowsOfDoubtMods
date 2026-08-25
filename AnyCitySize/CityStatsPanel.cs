using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AnyCitySize
{
    internal static class CityStatsPanel
    {
        const float rowHeight = 24f;
        const float rowFontSize = 18f;
        const float topInset = 160f;
        const float bottomInset = 120f;
        const float extraLeftInset = 16f;

        const string panelTitle = "City Stats";
        const string openButtonText = "Statistics";
        const string advancedSettingsButtonName = "OpenButton";
        const string advancedSettingsPanelName = "AdvancedSettings";
        const string loadingProgressName = "LoadingProgress";
        const float buttonRowSpacing = 4f;
        const float fallbackButtonHeight = 30f;

        static readonly string[] bedLayouts = { "Apartment", "HotelRoom" };
        static readonly string[] hiddenLayouts = { "rooftop", "lobby", "yard", "path", "outside", "atrium", "bathroom", "streetfrontage", "park", "powerroom" };

        static GameObject statsPanel;
        static int lastToggleFrame = -1;
        static GameObject advancedSettingsPanel;
        static ButtonController advancedSettingsButton;
        static RectTransform rowContainer;
        static GameObject rowTemplate;

        public static void Initialise(PrototypeDebugPanel panel)
        {
            if (rowContainer != null) return;
            if (panel.citySizeDropdownController == null || panel.seedText == null) return;

            var sourceComponents = panel.citySizeDropdownController.transform.parent;
            if (sourceComponents == null) return;

            var sourcePanel = sourceComponents;
            while (sourcePanel.parent != null && sourcePanel.parent != panel.transform)
            {
                sourcePanel = sourcePanel.parent;
            }
            if (sourcePanel.parent == null) return;

            AnyCitySizePlugin.Logger.LogInfo("Adding the city stats panel");

            rowTemplate = panel.seedText.gameObject;

            var componentsPath = new List<int>();
            for (var step = sourceComponents; step != sourcePanel; step = step.parent)
            {
                componentsPath.Insert(0, step.GetSiblingIndex());
            }

            statsPanel = GameObject.Instantiate(sourcePanel.gameObject, sourcePanel.parent);
            statsPanel.name = "AnyCitySizeStatsPanel";
            statsPanel.SetActive(true);

            MirrorHorizontally(sourcePanel.GetComponent<RectTransform>(), statsPanel.GetComponent<RectTransform>());

            var container = statsPanel.transform;
            foreach (var siblingIndex in componentsPath)
            {
                container = container.GetChild(siblingIndex);
            }
            container.name = "StatRows";

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                GameObject.DestroyImmediate(container.GetChild(i).gameObject);
            }

            // The title is driven from the string tables, so the controller has to go before it can be set
            foreach (var autoText in statsPanel.GetComponentsInChildren<MenuAutoTextController>(true))
            {
                var title = autoText.GetComponent<TMPro.TextMeshProUGUI>();
                GameObject.DestroyImmediate(autoText);
                if (title != null) title.SetText(panelTitle);
            }

            // The panel sizes its own button rows, so the layout has to take that over for plain text rows
            var layout = container.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;

            rowContainer = container.GetComponent<RectTransform>();
            StretchBetweenBars(rowContainer);

            Refresh();

            AddOpenButton(panel, sourceComponents);
        }

        static void AddOpenButton(PrototypeDebugPanel panel, Transform sourceComponents)
        {
            var phase = sourceComponents.Find("Phase");
            if (phase == null || panel.buildingSwapButton == null) return;

            var openButton = GameObject.Instantiate(panel.buildingSwapButton.gameObject, phase);
            openButton.name = "AnyCitySizeStatsButton";
            openButton.SetActive(true);

            var openButtonController = openButton.GetComponent<ButtonController>();
            openButtonController.useAutomaticText = false;
            openButtonController.SetInteractable(true);
            if (openButtonController.text != null) openButtonController.text.SetText(openButtonText);

            var button = openButton.GetComponent<Button>();
            if (button == null) button = openButton.GetComponentInChildren<Button>();

            // The controller invokes whichever button it holds a reference to, which has to be the one being listened to
            openButtonController.button = button;

            // Instantiate copies the template's persistent listeners, which would swap the selected tile's building as well
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((Action)Toggle);

            ShareRowWithAdvancedSettings(phase, openButton);
        }

        // ExtraCityEdit puts its Advanced Settings button in the same place, so split the row between the two
        static void ShareRowWithAdvancedSettings(Transform phase, GameObject openButton)
        {
            var advancedSettings = phase.Find(advancedSettingsButtonName);
            if (advancedSettings == null)
            {
                // ExtraCityEdit makes room for its own button by hiding the progress bar, so without it we do the same
                var loadingProgress = phase.Find(loadingProgressName);
                if (loadingProgress != null) loadingProgress.gameObject.SetActive(false);

                openButton.transform.SetSiblingIndex(0);
                return;
            }

            advancedSettingsButton = advancedSettings.GetComponent<ButtonController>();
            advancedSettingsPanel = statsPanel.transform.parent.Find(advancedSettingsPanelName)?.gameObject;
            if (advancedSettingsButton != null && advancedSettingsButton.button != null)
            {
                advancedSettingsButton.button.onClick.AddListener((Action)Hide);
            }

            var advancedSettingsRect = advancedSettings.GetComponent<RectTransform>();
            var buttonHeight = advancedSettingsRect.rect.height > 0f ? advancedSettingsRect.rect.height : fallbackButtonHeight;

            var row = new GameObject("AnyCitySizeButtonRow");
            var rowRect = row.AddComponent<RectTransform>();
            row.transform.SetParent(phase, false);
            row.transform.SetSiblingIndex(advancedSettings.GetSiblingIndex());

            rowRect.anchorMin = advancedSettingsRect.anchorMin;
            rowRect.anchorMax = advancedSettingsRect.anchorMax;
            rowRect.pivot = advancedSettingsRect.pivot;
            rowRect.sizeDelta = advancedSettingsRect.sizeDelta;
            rowRect.anchoredPosition = advancedSettingsRect.anchoredPosition;

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            rowLayout.spacing = buttonRowSpacing;

            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = buttonHeight;
            rowElement.minHeight = buttonHeight;

            advancedSettings.SetParent(row.transform, false);
            openButton.transform.SetParent(row.transform, false);

            SplitRowEvenly(advancedSettingsRect);
            SplitRowEvenly(openButton.GetComponent<RectTransform>());
        }

        public static void Toggle()
        {
            if (statsPanel == null) return;

            // Both Button and ButtonController invoke onClick, so a listener added at runtime fires twice per press
            if (lastToggleFrame == Time.frameCount) return;
            lastToggleFrame = Time.frameCount;

            var showing = !statsPanel.activeSelf;
            statsPanel.SetActive(showing);

            if (!showing) return;

            HideAdvancedSettings();
            Refresh();
        }

        static void Hide()
        {
            if (statsPanel != null) statsPanel.SetActive(false);
        }

        // ExtraCityEdit only re-enables its own button from the panel's close button, so it has to be restored here
        static void HideAdvancedSettings()
        {
            if (advancedSettingsPanel == null || !advancedSettingsPanel.activeSelf) return;

            advancedSettingsPanel.SetActive(false);
            if (advancedSettingsButton != null) advancedSettingsButton.SetInteractable(true);
        }

        static void SplitRowEvenly(RectTransform button)
        {
            var element = button.GetComponent<LayoutElement>();
            if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 0f;
            element.minWidth = 0f;
            element.flexibleWidth = 1f;
        }

        static void StretchBetweenBars(RectTransform container)
        {
            var horizontalMin = container.offsetMin.x;
            var horizontalMax = container.offsetMax.x;

            container.anchorMin = new Vector2(container.anchorMin.x, 0f);
            container.anchorMax = new Vector2(container.anchorMax.x, 1f);
            container.pivot = new Vector2(container.pivot.x, 1f);
            container.offsetMin = new Vector2(horizontalMin + extraLeftInset, bottomInset);
            container.offsetMax = new Vector2(horizontalMax, -topInset);
        }

        public static void Refresh()
        {
            if (rowContainer == null || !statsPanel.activeSelf) return;

            for (int i = rowContainer.childCount - 1; i >= 0; i--)
            {
                GameObject.DestroyImmediate(rowContainer.GetChild(i).gameObject);
            }

            foreach (var row in GatherStats())
            {
                AddRow(row);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rowContainer);
        }

        static void MirrorHorizontally(RectTransform source, RectTransform target)
        {
            target.anchorMin = new Vector2(1f - source.anchorMax.x, source.anchorMin.y);
            target.anchorMax = new Vector2(1f - source.anchorMin.x, source.anchorMax.y);
            target.pivot = new Vector2(1f - source.pivot.x, source.pivot.y);
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = new Vector2(-source.anchoredPosition.x, source.anchoredPosition.y);
        }

        static void AddRow(string text)
        {
            var row = GameObject.Instantiate(rowTemplate, rowContainer);
            row.name = "StatRow";
            row.SetActive(true);

            var rowText = row.GetComponent<TMPro.TextMeshProUGUI>();
            rowText.alignment = TMPro.TextAlignmentOptions.Left;
            rowText.enableWordWrapping = false;
            rowText.enableAutoSizing = false;
            rowText.fontSize = rowFontSize;
            rowText.SetText(text);

            var layoutElement = row.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = rowHeight;
        }

        static List<string> GatherStats()
        {
            var rows = new List<string>();

            var cityData = CityData.Instance;
            if (cityData == null)
            {
                rows.Add("No city generated");
                return rows;
            }

            var cityX = RestartSafeController.Instance.cityX;
            var cityY = RestartSafeController.Instance.cityY;

            var layoutCounts = EstimateAddressLayouts();
            if (layoutCounts.Count == 0)
            {
                rows.Add("No buildings generated");
                return rows;
            }

            var estHomelessPopulation = Mathf.FloorToInt(cityX * cityY * CityControls.Instance.homelessMultiplier *
                                                 cityData.populationMultiplier);
            var beds = new Range();
            foreach (var bedLayout in bedLayouts)
            {
                if (layoutCounts.TryGetValue(bedLayout, out var bedLayoutCount)) beds.Add(bedLayoutCount, 1);
            }

            var estHomedPopulation = beds * (1 + SocialStatistics.Instance.seriousRelationshipsRatio);

            rows.Add($"Est. homed citizens: {estHomedPopulation}");
            rows.Add($"Est. homeless citizens: {estHomelessPopulation}");
            rows.Add($"Est. total citizens: {estHomedPopulation + estHomelessPopulation}");

            rows.Add(string.Empty);
            rows.Add("Address layouts:");
            foreach (var layoutCount in layoutCounts.Where(entry => !IsHidden(entry.Key)).OrderByDescending(entry => entry.Value.max))
            {
                rows.Add($"  {layoutCount.Key}: {layoutCount.Value}");
            }

            return rows;
        }

        static bool IsHidden(string layoutName)
        {
            var lowercaseName = layoutName.ToLowerInvariant();
            return hiddenLayouts.Any(hiddenLayout => lowercaseName.Contains(hiddenLayout));
        }

        // Interiors are only built during generateBlueprints, which the city editor stops short of, so the
        // address layouts each building will produce have to be read back out of its preset's floor plans
        static Dictionary<string, Range> EstimateAddressLayouts()
        {
            var layoutCounts = new Dictionary<string, Range>();

            // buildingDirectory is never pruned, so a tile that has had its building swapped out is counted
            // twice; the tiles themselves always point at what is currently standing on them
            var cityTiles = UnityEngine.Object.FindObjectOfType<CityBoundaryAndTiles>(true);
            if (cityTiles == null) return layoutCounts;

            foreach (var cityTile in cityTiles.cityTiles)
            {
                var building = cityTile.Value.building;
                if (building == null || building.isInaccessible || building.preset == null) continue;

                foreach (var floorSetting in building.preset.floorLayouts)
                {
                    AccumulateFloorSetting(floorSetting, layoutCounts);
                }

                foreach (var basementSetting in building.preset.basementLayouts)
                {
                    AccumulateFloorSetting(basementSetting, layoutCounts);
                }
            }

            return layoutCounts;
        }

        static void AccumulateFloorSetting(BuildingPreset.InteriorFloorSetting floorSetting, Dictionary<string, Range> layoutCounts)
        {
            if (floorSetting.floorsWithThisSetting <= 0) return;

            var variants = new List<Dictionary<string, int>>();
            foreach (var blueprint in floorSetting.blueprints)
            {
                if (blueprint == null) continue;

                FloorSaveData floorData = null;
                if (!CityData.Instance.floorData.TryGetValue(blueprint.name, out floorData) || floorData == null) continue;

                var variant = new Dictionary<string, int>();
                foreach (var address in floorData.a_d)
                {
                    if (string.IsNullOrEmpty(address.p_n)) continue;

                    variant.TryGetValue(address.p_n, out int count);
                    variant[address.p_n] = count + 1;
                }
                variants.Add(variant);
            }

            if (variants.Count == 0) return;

            foreach (var layoutName in variants.SelectMany(variant => variant.Keys).Distinct())
            {
                var acrossVariants = new Range();
                foreach (var variant in variants)
                {
                    variant.TryGetValue(layoutName, out int count);
                    acrossVariants.Widen(count);
                }

                if (!layoutCounts.TryGetValue(layoutName, out var layoutCount))
                {
                    layoutCount = new Range();
                    layoutCounts[layoutName] = layoutCount;
                }
                layoutCount.Add(acrossVariants, floorSetting.floorsWithThisSetting);
            }
        }

        class Range
        {
            public int min;
            public int max;

            bool widened;

            public static Range operator +(Range a, int b) => new Range() { min = a.min + b, max = a.max + b };
            public static Range operator -(Range a, int b) => new Range() { min = a.min - b, max = a.max - b };
            public static Range operator *(Range a, float b) => new Range() { min = Mathf.RoundToInt(a.min * b), max = Mathf.RoundToInt(a.max * b) };
            public static Range operator /(Range a, float b) => new Range() { min = Mathf.RoundToInt(a.min / b), max = Mathf.RoundToInt(a.max / b) };
            
            public void Widen(int value)
            {
                min = widened ? Mathf.Min(min, value) : value;
                max = widened ? Mathf.Max(max, value) : value;
                widened = true;
            }

            public void Add(Range other, int multiplier)
            {
                min += other.min * multiplier;
                max += other.max * multiplier;
                widened = true;
            }

            public override string ToString()
            {
                return min == max ? min.ToString() : $"{min} - {max}";
            }
        }
    }
}
