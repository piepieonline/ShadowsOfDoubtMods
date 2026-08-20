using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DialogUIRework;

public class TabbedDialogUI
{
    private GameObject tabContainer;
    private GameObject contentContainer;
    private GameObject leftArrow;
    private GameObject rightArrow;
    private GameObject tabSpacerLeft;
    private GameObject tabSpacerRight;
    private string controlGlyphFormat = """<sprite="desktop" name="Keyboard Key">""";

    private Dictionary<string, Il2CppSystem.Collections.Generic.List<DialogButtonController>> categorizedOptions =
        new Dictionary<string, Il2CppSystem.Collections.Generic.List<DialogButtonController>>();

    private Dictionary<string, GameObject> tabButtons = new Dictionary<string, GameObject>();
    private List<string> categoryOrder = new List<string>();
    private string currentCategory;
    private int currentOptionIndex = 0;

    private ColorBlock buttonColors = new ColorBlock()
    {
        normalColor = new Color(0.2519f, 0.2118f, 0.5283f, 1),
        highlightedColor = new Color(0.2519f, 0.2118f, 0.5283f, 1),
        pressedColor = new Color(0.2519f, 0.2118f, 0.5283f, 1),
        selectedColor = new Color(0.364705882f, 0.305882353f, 0.760784314f, 1)
    };

    public static void AddIDs(string category, params string[] messageIds)
    {
        if (!DialogCategories.CategoryIDs.ContainsKey(category))
            DialogCategories.CategoryIDs[category] = new List<string>();
        DialogCategories.CategoryIDs[category].AddRange(messageIds);
    }

    public static void AddKeywords(string category, params string[] keywords)
    {
        if (!DialogCategories.CategoryKeywords.ContainsKey(category))
            DialogCategories.CategoryKeywords[category] = new List<string>();
        DialogCategories.CategoryKeywords[category].AddRange(keywords);
    }

    public void CreateDialogUI()
    {
        if (tabContainer != null)
            return;
        
        var dialogContainer = PrefabControls.Instance.dialogOptionContainer.gameObject;
        
        PrefabControls.Instance.dialogOptionContainer.parent.Find("Header").localPosition +=
            new Vector3(0, 45, 0);
        PrefabControls.Instance.dialogOptionContainer.parent.Find("Border").gameObject.SetActive(false);
        var nav =
            PrefabControls.Instance.dialogOptionContainer.gameObject.AddComponent<TabNavigationComponent>();
        nav.DialogUI = DialogUIReworkPlugin.TabbedDialogUI;
        
        tabContainer = new GameObject("TabContainer");
        tabContainer.transform.SetParent(dialogContainer.transform.parent, false);
        tabContainer.transform.SetSiblingIndex(2);

        var horizontalLayout = tabContainer.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = false;
        horizontalLayout.spacing = 5f;
        horizontalLayout.padding = new RectOffset(10, 10, 10, 10);
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;

        var rectTransform = tabContainer.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(0.5f, 1);
        rectTransform.sizeDelta = new Vector2(0, 60);
        rectTransform.anchoredPosition = new Vector2(0, 0);

        var templateImage = PrefabControls.Instance.dialogRect.GetComponentInChildren<Image>();
        var bg = tabContainer.AddComponent<Image>();
        bg.color = templateImage.color;
        bg.sprite = templateImage.sprite;
        bg.type = templateImage.type;
        bg.fillMethod = templateImage.fillMethod;
        bg.fillCenter = templateImage.fillCenter;
        bg.fillClockwise = templateImage.fillClockwise;
        bg.color = templateImage.color;

        leftArrow = new GameObject("LeftArrow");
        leftArrow.transform.SetParent(tabContainer.transform, false);
        var lArrowText = leftArrow.AddComponent<TMPro.TextMeshProUGUI>();
        lArrowText.text = controlGlyphFormat;
        lArrowText.fontSize = 16;
        lArrowText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        lArrowText.margin = new Vector4(20, 0, 0, 0);
        var lArrowLayout = leftArrow.AddComponent<LayoutElement>();
        lArrowLayout.minWidth = 50;

        tabSpacerLeft = new GameObject("TabSpacerLeft");
        tabSpacerLeft.transform.SetParent(tabContainer.transform, false);
        tabSpacerLeft.AddComponent<LayoutElement>().flexibleWidth = 1;

        tabSpacerRight = new GameObject("TabSpacerRight");
        tabSpacerRight.transform.SetParent(tabContainer.transform, false);
        tabSpacerRight.AddComponent<LayoutElement>().flexibleWidth = 1;

        rightArrow = new GameObject("RightArrow");
        rightArrow.transform.SetParent(tabContainer.transform, false);
        var rArrowText = rightArrow.AddComponent<TMPro.TextMeshProUGUI>();
        rArrowText.text = controlGlyphFormat;
        rArrowText.fontSize = 16;
        rArrowText.alignment = TMPro.TextAlignmentOptions.MidlineRight;
        rArrowText.margin = new Vector4(0, 0, 15, 0);
        var rArrowLayout = rightArrow.AddComponent<LayoutElement>();
        rArrowLayout.minWidth = 45;

        DialogUIReworkPlugin._enableControlGlyphs.SettingChanged += (sender, args) => UpdateControlGlyphs();
        DialogUIReworkPlugin._enableWASDNavigation.SettingChanged += (sender, args) => UpdateControlGlyphs();
        UpdateControlGlyphs();
    }

    private void UpdateControlGlyphs()
    {
        leftArrow.SetActive(DialogUIReworkPlugin._enableControlGlyphs.Value);
        rightArrow.SetActive(DialogUIReworkPlugin._enableControlGlyphs.Value);

        leftArrow.GetComponent<TMPro.TextMeshProUGUI>().text = controlGlyphFormat + (DialogUIReworkPlugin._enableWASDNavigation.Value ? "A" : "←");
        rightArrow.GetComponent<TMPro.TextMeshProUGUI>().text = controlGlyphFormat + (DialogUIReworkPlugin._enableWASDNavigation.Value ? "D" : "→");
    }

    public void ModifyDialogContainer()
    {
        CategorizeDialogOptions(PrefabControls.Instance.dialogOptionContainer.gameObject);
        CreateCategorizedUI(PrefabControls.Instance.dialogOptionContainer.gameObject);
    }

    private void CategorizeDialogOptions(GameObject dialogContainer)
    {
        foreach (var category in DialogCategories.CategoryKeywords.Keys)
        {
            categorizedOptions[category] = new Il2CppSystem.Collections.Generic.List<DialogButtonController>();
        }

        for (int i = 0; i < dialogContainer.transform.childCount; i++)
        {
            var option = dialogContainer.transform.GetChild(i).gameObject.GetComponent<DialogButtonController>();
            string category = DetermineCategory(option);
            categorizedOptions[category].Add(option);
            categorizedOptions["All"].Add(option);

            option.gameObject.SetActive(false);
            option.SetSelectable(false);
        }

        var emptyCategories = categorizedOptions.Where(kvp => kvp.Value.Count == 0).Select(kvp => kvp.Key).ToList();
        foreach (var empty in emptyCategories)
        {
            categorizedOptions.Remove(empty);
        }
    }

    private string DetermineCategory(DialogButtonController option)
    {
        string optionId = GetOptionId(option);
        string optionText = GetOptionText(option).ToLower();

        if (!string.IsNullOrEmpty(optionId))
        {
            foreach (var kvp in DialogCategories.CategoryIDs)
            {
                if (kvp.Value.Contains(optionId))
                {
                    // DialogUIReworkPlugin.PluginLogger.LogInfo($"Placed {optionText} by ID ({optionId})");
                    return kvp.Key;
                }
            }
        }

        if (string.IsNullOrEmpty(optionText))
        {
            DialogUIReworkPlugin.PluginLogger.LogWarning($"Placed {optionText} ({optionId}) by default");
            return "Other";
        }

        foreach (var kvp in DialogCategories.CategoryKeywords)
        {
            if (kvp.Key == "Other") continue; // Skip Other, it's the default

            foreach (var keyword in kvp.Value)
            {
                if (optionText.Contains(keyword.ToLower()))
                {
                    DialogUIReworkPlugin.PluginLogger.LogWarning($"Placed {optionText} ({optionId}) by keyword ({keyword})");
                    return kvp.Key;
                }
            }
        }

        DialogUIReworkPlugin.PluginLogger.LogWarning($"Placed {optionText} ({optionId}) by default");
        return "Other";
    }

    private string GetOptionId(DialogButtonController option)
    {
        return option.option.preset.msgID;
    }

    private string GetOptionText(DialogButtonController option)
    {
        var tmpText = option.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        return tmpText != null ? tmpText.text : "";
    }

    private void CreateCategorizedUI(GameObject dialogContainer)
    {
        CreateTabs();

        var categoryToUse = currentCategory;
        if (String.IsNullOrEmpty(currentCategory))
        {
            if (PrefabControls.Instance.dialogOptionContainer.childCount > 0)
            {
                categoryToUse = DetermineCategory(PrefabControls.Instance.dialogOptionContainer.GetChild(0).GetComponent<DialogButtonController>());
            }
            else
            {
                categoryToUse = categorizedOptions.Keys.First();
            }
        }

        ShowCategory(categoryToUse);
    }

    private void CreateTabs()
    {
        categoryOrder.Clear();
        foreach (var category in categorizedOptions.Keys)
        {
            if ((!DialogUIReworkPlugin._enableAllTab.Value || categorizedOptions.Count == 2) && category == "All")
                continue;
            CreateTab(category);
        }
    }

    private void CreateTab(string category)
    {
        if (string.IsNullOrEmpty(category))
            return;

        if (tabButtons.ContainsKey(category) && tabButtons[category] != null)
        {
            GameObject.Destroy(tabButtons[category]);
        }

        categoryOrder.Add(category);

        GameObject tab = new GameObject($"Tab_{category}");
        tab.transform.SetParent(tabContainer.transform, false);
        // Insert before the spacer (which is second-to-last, before rightArrow)
        if (tabSpacerRight != null)
            tab.transform.SetSiblingIndex(tabSpacerRight.transform.GetSiblingIndex());

        var button = tab.AddComponent<Button>();

        var templateImage = PrefabControls.Instance.dialogOption.GetComponentInChildren<Image>();
        var image = tab.AddComponent<Image>();
        image.color = templateImage.color;
        image.sprite = templateImage.sprite;
        image.type = templateImage.type;
        image.fillMethod = templateImage.fillMethod;
        image.fillCenter = templateImage.fillCenter;
        image.fillClockwise = templateImage.fillClockwise;

        button.colors = buttonColors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tab.transform, false);

        // Copying settings from the dialogButton template
        var templateTMPPro = PrefabControls.Instance.dialogOption.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        TMPro.TextMeshProUGUI tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmpText.text = $"{category} ({categorizedOptions[category].Count})";
        tmpText.font = templateTMPPro.font;
        tmpText.fontSize = 14; // templateTMPPro.fontSize; // Smaller font works better visually
        tmpText.color = templateTMPPro.color;
        tmpText.alignment = TMPro.TextAlignmentOptions.Center;

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        var tabRect = tab.GetComponent<RectTransform>();
        tabRect.sizeDelta = new Vector2(100, 30);

        var layoutElement = tab.AddComponent<LayoutElement>();
        layoutElement.minWidth = 120;
        layoutElement.preferredWidth = 160;
        layoutElement.flexibleWidth = 0;
        layoutElement.minHeight = 40;

        tabButtons[category] = tab;
    }

    public void ClearDialogOptions()
    {
        if (categorizedOptions.ContainsKey("All"))
        {
            for (int i = categorizedOptions["All"].Count - 1; i >= 0; i--)
            {
                GameObject.Destroy(categorizedOptions["All"][i].gameObject);
            }
        }

        categorizedOptions.Clear();

        foreach (var kvp in tabButtons)
        {
            if (kvp.Value != null)
            {
                GameObject.Destroy(kvp.Value);
            }
        }
        tabButtons.Clear();
        categoryOrder.Clear();

        currentCategory = "";
    }

    private void ShowCategory(string category)
    {
        if (InteractionController.Instance.talkingTo != null &&
            InteractionController.Instance.talkingTo.speechController != null &&
            InteractionController.Instance.talkingTo.speechController.speechActive)
        {
            return;
        }

        currentCategory = String.IsNullOrEmpty(category) ? "All" : category;

        foreach (var kvp in categorizedOptions)
        {
            foreach (var option in kvp.Value)
            {
                option.gameObject.SetActive(false);
                option.SetSelectable(false);
            }
        }

        PrefabControls.Instance.dialogOptionContainer.anchoredPosition = new Vector2(0, -58);

        if (categorizedOptions.ContainsKey(category))
        {
            var offset = 0;
            foreach (var option in categorizedOptions[category])
            {
                option.gameObject.SetActive(true);
                option.SetSelectable(true);
                option.rect.anchoredPosition = new Vector2(0f, offset);
                offset -= 52;
            }
        }

        UpdateTabVisuals();
        if (InteractionController.Instance != null)
            InteractionController.Instance.SetDialogSelection(0);
    }

    private void UpdateTabVisuals()
    {
        foreach (var kvp in tabButtons)
        {
            var image = kvp.Value.GetComponent<Image>();
            if (kvp.Key == currentCategory)
            {
                image.color = buttonColors.selectedColor;
            }
            else
            {
                image.color = buttonColors.normalColor;
            }
        }
    }

    public void SwitchToPreviousTab()
    {
        if (categoryOrder == null || categoryOrder.Count <= 1)
            return;

        int currentIndex = categoryOrder.IndexOf(currentCategory);
        if (currentIndex < 0)
            return;

        int previousIndex = (currentIndex - 1 + categoryOrder.Count) % categoryOrder.Count;
        ShowCategory(categoryOrder[previousIndex]);
    }

    public void SwitchToNextTab()
    {
        if (categoryOrder == null || categoryOrder.Count <= 1)
            return;

        int currentIndex = categoryOrder.IndexOf(currentCategory);
        if (currentIndex < 0)
            return;

        int nextIndex = (currentIndex + 1) % categoryOrder.Count;
        ShowCategory(categoryOrder[nextIndex]);
    }

    public void SwitchToTab(string category)
    {
        ShowCategory(category);
    }

    public Il2CppSystem.Collections.Generic.List<DialogButtonController> GetTabDialogOptions(
        string categoryOverride = null)
    {
        var cat = categoryOverride == null ? currentCategory : categoryOverride;
        if (categorizedOptions == null || cat == null || !categorizedOptions.ContainsKey(cat))
            return null;

        // TODO this better

        for (int i = categorizedOptions[cat].Count - 1; i >= 0; i--)
        {
            var option = categorizedOptions[cat][i];
            if (option == null || option.gameObject == null)
                categorizedOptions[cat].RemoveAt(i);
        }

        return categorizedOptions[cat];
    }
}

public class TabNavigationComponent : MonoBehaviour
{
    internal TabbedDialogUI DialogUI;

    void Update()
    {
        if (DialogUI == null || !InteractionController.Instance.dialogMode)
            return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) ||
            (DialogUIReworkPlugin._enableWASDNavigation.Value && Input.GetKeyDown(KeyCode.A)))
        {
            DialogUI.SwitchToPreviousTab();
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) ||
            (DialogUIReworkPlugin._enableWASDNavigation.Value && Input.GetKeyDown(KeyCode.D)))
        {
            DialogUI.SwitchToNextTab();
        }

        if (DialogUIReworkPlugin._enableWASDNavigation.Value)
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                InteractionController.Instance.SetDialogSelection(InteractionController.Instance.dialogSelection - 1);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                InteractionController.Instance.SetDialogSelection(InteractionController.Instance.dialogSelection + 1);
            }
        }
    }
}