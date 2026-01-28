using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DebugMod;

// TODO: Overflowing lists show as overflowing by default on changing tab
// TODO: Doesn't handle tabbing when in the middle of dialog

public class DialogUIRework
{
    [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.Update))]
    public class InteractionController_Update
    {
        public static void Prefix()
        {
            if (InteractionController_RefreshDialogOptions.CategorizedUI == null)
                return;

            var opts = InteractionController_RefreshDialogOptions.CategorizedUI.GetTabDialogOptions();

            if (opts == null)
                return;

            InteractionController.Instance.dialogOptions = opts;
        }

        public static void Postfix()
        {
            if (InteractionController_RefreshDialogOptions.CategorizedUI == null)
                return;

            var opts = InteractionController_RefreshDialogOptions.CategorizedUI.GetTabDialogOptions("All");

            if (opts == null)
                return;

            InteractionController.Instance.dialogOptions = opts;
        }
    }


    [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.SetDialog))]
    public class InteractionController_SetDialog
    {
        public static void Prefix(bool val)
        {
            if (InteractionController_RefreshDialogOptions.CategorizedUI == null)
                return;

            if (!val)
            {
                InteractionController_RefreshDialogOptions.CategorizedUI.ClearDialogOptions();
            }

            InteractionController_RefreshDialogOptions.CategorizedUI.SwitchToTab("All");
        }
    }

    [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.RefreshDialogOptions))]
    public class InteractionController_RefreshDialogOptions
    {
        public static CategorizedDialogUI CategorizedUI;

        public static void Postfix()
        {
            // DebugModPlugin.PluginLogger.LogInfo("----------------- Refreshing dialog options -----------------");

            if (CategorizedUI == null)
            {
                CategorizedUI = new CategorizedDialogUI();
                PrefabControls.Instance.dialogOptionContainer.parent.Find("Header").localPosition +=
                    new Vector3(0, 45, 0);
                PrefabControls.Instance.dialogOptionContainer.parent.Find("Border").gameObject.SetActive(false);
                Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<TabNavigationComponent>();
                var nav =
                    PrefabControls.Instance.dialogOptionContainer.gameObject.AddComponent<TabNavigationComponent>();
                nav.dialogUI = CategorizedUI;
            }

            CategorizedUI.ModifyDialogContainer(PrefabControls.Instance.dialogOptionContainer.gameObject);
        }
    }

    [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.SetDialogSelection))]
    public class InteractionController_SetDialogSelection
    {
        public static bool Prefix(InteractionController __instance, int newVal)
        {
            if (InteractionController_RefreshDialogOptions.CategorizedUI == null ||
                InteractionController_RefreshDialogOptions.CategorizedUI.GetTabDialogOptions() == null)
                return true;

            var currentDialog = InteractionController_RefreshDialogOptions.CategorizedUI.GetTabDialogOptions();

            __instance.dialogSelection = Mathf.Clamp(newVal, 0, currentDialog.Count - 1);
            __instance.moreOptionsScrollUpArrow.gameObject.SetActive(false);
            __instance.moreOptionsScrollDownArrow.gameObject.SetActive(false);
            PrefabControls.Instance.dialogOptionContainer.anchoredPosition = new Vector2(
                PrefabControls.Instance.dialogOptionContainer.anchoredPosition.x,
                (float)(-58 + Mathf.Max(__instance.dialogSelection - 3, 0) * 52));
            for (int i = 0; i < currentDialog.Count; i++)
            {
                DialogButtonController dialogButtonController = currentDialog[i];

                if (dialogButtonController == null)
                {
                    // InteractionController.Instance.RefreshDialogOptions();
                    return false;
                }

                if (dialogButtonController.rect.position.y >= __instance.moreOptionsScrollUpArrow.position.y)
                {
                    dialogButtonController.gameObject.SetActive(false);
                    __instance.moreOptionsScrollUpArrow.gameObject.SetActive(true);
                }
                else if (dialogButtonController.rect.position.y <= __instance.moreOptionsScrollDownArrow.position.y)
                {
                    dialogButtonController.gameObject.SetActive(false);
                    __instance.moreOptionsScrollDownArrow.gameObject.SetActive(true);
                }
                else
                {
                    dialogButtonController.gameObject.SetActive(true);
                }
            }

            return false;
        }

        /*
        public static void Prefix(InteractionController __instance, ref Il2CppSystem.Collections.Generic.List<DialogButtonController> __state)
        {
            __state = __instance.dialogOptions;
            if(InteractionController_RefreshDialogOptions.CategorizedUI != null)
                __instance.dialogOptions = InteractionController_RefreshDialogOptions.CategorizedUI.GetTabDialogOptions();
        }

        public static void Postfix(InteractionController __instance, Il2CppSystem.Collections.Generic.List<DialogButtonController> __state)
        {
            __instance.dialogOptions = __state;
        }
        */
    }
}

public class TabNavigationComponent : MonoBehaviour
{
    public CategorizedDialogUI dialogUI;

    void Update()
    {
        if (dialogUI == null)
            return;

        // Q or Left Arrow - previous tab
        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            dialogUI.SwitchToPreviousTab();
        }

        // E or Right Arrow - next tab
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            dialogUI.SwitchToNextTab();
        }
    }
}

public class CategorizedDialogUI
{
    private GameObject tabContainer;
    private GameObject contentContainer;
    private Dictionary<string, Il2CppSystem.Collections.Generic.List<DialogButtonController>> categorizedOptions;
    private Dictionary<string, GameObject> tabButtons;
    private string currentCategory;
    private int currentOptionIndex = 0;
    private List<string> categoryOrder;

    public void ModifyDialogContainer(GameObject dialogContainer)
    {
        if (dialogContainer == null)
        {
            DebugModPlugin.PluginLogger.LogWarning("Dialog container is null!");
            return;
        }

        // Initialize collections
        categorizedOptions = new Dictionary<string, Il2CppSystem.Collections.Generic.List<DialogButtonController>>();
        tabButtons = new Dictionary<string, GameObject>();
        categoryOrder = new List<string>();

        // Collect and categorize all dialog options
        CategorizeDialogOptions(dialogContainer);

        // Create the new UI structure
        CreateCategorizedUI(dialogContainer);
    }

    private void CategorizeDialogOptions(GameObject dialogContainer)
    {
        // Initialize category lists
        foreach (var category in CategoryKeywords.Keys)
        {
            categorizedOptions[category] = new Il2CppSystem.Collections.Generic.List<DialogButtonController>();
        }

        // Get all child dialog options
        for (int i = 0; i < dialogContainer.transform.childCount; i++)
        {
            var option = dialogContainer.transform.GetChild(i).gameObject.GetComponent<DialogButtonController>();
            string category = DetermineCategory(option);
            categorizedOptions[category].Add(option);
            categorizedOptions["All"].Add(option);

            // Keep option in hierarchy but disable it initially
            option.gameObject.SetActive(false);
            option.SetSelectable(false);
        }

        // Remove empty categories
        var emptyCategories = categorizedOptions.Where(kvp => kvp.Value.Count == 0).Select(kvp => kvp.Key).ToList();
        foreach (var empty in emptyCategories)
        {
            categorizedOptions.Remove(empty);
        }

        // If we have an "Other" category but it's the only one, we don't need tabs
        if (categorizedOptions.Count == 2)
        {
            // Just show all options without tabs
            foreach (var option in categorizedOptions["All"])
            {
                option.gameObject.SetActive(true);
            }

            return;
        }
    }

    private string DetermineCategory(DialogButtonController option)
    {
        string optionId = GetOptionId(option);
        string optionText = GetOptionText(option).ToLower();

        if (!string.IsNullOrEmpty(optionId))
        {
            foreach (var kvp in CategoryIDs)
            {
                if (kvp.Value.Contains(optionId))
                {
                    // DebugModPlugin.PluginLogger.LogInfo($"Placed {optionText} by ID ({optionId})");
                    return kvp.Key;
                }
            }
        }

        if (string.IsNullOrEmpty(optionText))
        {
            DebugModPlugin.PluginLogger.LogWarning($"Placed {optionText} by default");
            return "Other";
        }

        foreach (var kvp in CategoryKeywords)
        {
            if (kvp.Key == "Other") continue; // Skip Other, it's the default

            foreach (var keyword in kvp.Value)
            {
                if (optionText.Contains(keyword.ToLower()))
                {
                    DebugModPlugin.PluginLogger.LogWarning($"Placed {optionText} by keyword ({keyword})");
                    return kvp.Key;
                }
            }
        }

        DebugModPlugin.PluginLogger.LogWarning($"Placed {optionText} by default");
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
        if (categorizedOptions.Count <= 2)
            return; // No need for tabs if only one category

        // Create tab container at the top
        CreateTabContainer(dialogContainer);

        // Create tabs for each category
        CreateTabs();

        // Show the first category by default
        var firstCategory = String.IsNullOrEmpty(currentCategory) ? categorizedOptions.Keys.First() : currentCategory;
        ShowCategory(firstCategory);
    }

    private void CreateTabContainer(GameObject dialogContainer)
    {
        // Check if tab container already exists
        Transform existingTab = dialogContainer.transform.parent.Find("TabContainer");
        if (existingTab != null)
        {
            // Destroy existing tab container to recreate it fresh
            UnityEngine.Object.Destroy(existingTab.gameObject);
        }

        tabContainer = new GameObject("TabContainer");
        tabContainer.transform.SetParent(dialogContainer.transform.parent, false);
        tabContainer.transform.SetSiblingIndex(2);

        // Add layout components
        var horizontalLayout = tabContainer.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = false;
        horizontalLayout.spacing = 5f;
        horizontalLayout.padding = new RectOffset(5, 5, 5, 5);
        horizontalLayout.childAlignment = TextAnchor.MiddleLeft;

        // Setup RectTransform
        var rectTransform = tabContainer.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(0.5f, 1);
        rectTransform.sizeDelta = new Vector2(0, 40);
        rectTransform.anchoredPosition = new Vector2(0, 0);

        // Add a background (optional, adjust color to match game style)
        var bg = tabContainer.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.1f, 0.3f, 0.8f); // Dark purple background
    }

    private void CreateTabs()
    {
        foreach (var category in categorizedOptions.Keys)
        {
            categoryOrder.Add(category);
            CreateTab(category);
        }
    }

    private void CreateTab(string category)
    {
        // Create tab button
        GameObject tab = new GameObject($"Tab_{category}");
        tab.transform.SetParent(tabContainer.transform, false);

        // Add button component
        var button = tab.AddComponent<Button>();

        // Add image for visual feedback
        var image = tab.AddComponent<Image>();
        image.color = new Color(0.4f, 0.2f, 0.5f, 0.6f); // Inactive tab color

        // Setup button colors
        var colors = button.colors;
        colors.normalColor = new Color(0.4f, 0.2f, 0.5f, 0.6f);
        colors.highlightedColor = new Color(0.5f, 0.3f, 0.6f, 0.8f);
        colors.pressedColor = new Color(0.6f, 0.4f, 0.7f, 1f);
        colors.selectedColor = new Color(0.5f, 0.3f, 0.6f, 1f);
        button.colors = colors;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tab.transform, false);

        TMPro.TextMeshProUGUI tmpText = null;

        tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmpText.text = $"{category} ({categorizedOptions[category].Count})";
        tmpText.fontSize = 14;
        tmpText.color = Color.white;
        tmpText.alignment = TMPro.TextAlignmentOptions.Center;

        // Setup text RectTransform to fill the button
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        // Setup tab RectTransform
        var tabRect = tab.GetComponent<RectTransform>();
        tabRect.sizeDelta = new Vector2(100, 30);

        // Add layout element for proper sizing
        var layoutElement = tab.AddComponent<LayoutElement>();
        layoutElement.minWidth = 80;
        layoutElement.preferredWidth = 120;
        layoutElement.flexibleWidth = 0;
        layoutElement.minHeight = 30;

        // Store reference
        tabButtons[category] = tab;
    }

    public void ClearDialogOptions()
    {
        categorizedOptions.Clear();
        currentCategory = "";
    }

    private void ShowCategory(string category)
    {
        currentCategory = String.IsNullOrEmpty(category) ? "All" : category;

        // Hide all options
        foreach (var kvp in categorizedOptions)
        {
            foreach (var option in kvp.Value)
            {
                option.gameObject.SetActive(false);
                option.SetSelectable(false);
            }
        }

        PrefabControls.Instance.dialogOptionContainer.anchoredPosition = new Vector2(0, -58);
        if (InteractionController.Instance != null)
            InteractionController.Instance.SetDialogSelection(0);
        // Show options for current category
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

        // Update tab button visuals
        UpdateTabVisuals();
    }

    private void UpdateTabVisuals()
    {
        foreach (var kvp in tabButtons)
        {
            var image = kvp.Value.GetComponent<Image>();
            if (kvp.Key == currentCategory)
            {
                // Active tab - brighter color
                image.color = new Color(0.6f, 0.4f, 0.7f, 1f);
            }
            else
            {
                // Inactive tab - darker color
                image.color = new Color(0.4f, 0.2f, 0.5f, 0.6f);
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

        // Move to previous tab, wrap around to end if at beginning
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

        // Move to next tab, wrap around to beginning if at end
        int nextIndex = (currentIndex + 1) % categoryOrder.Count;
        ShowCategory(categoryOrder[nextIndex]);
    }

    public void SwitchToTab(string category)
    {
        if (!String.IsNullOrEmpty(category) && categoryOrder.Contains(category))
            ShowCategory(category);
        else
            ShowCategory("All");
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

    private static Dictionary<string, List<string>> CategoryIDs = new Dictionary<string, List<string>>
    {
        ["Dialogue"] = new List<string>
        {
            "ac8eec11-4039-463e-858d-94aa2a1b0cf5", // |City.time.timeofday|, what's your name?
            "4f98dbb8-20c0-49b2-8c18-058a7e24f5a2", // I'll give you money for your name.
            "873a7c42-f8c3-49ee-8ce7-3c0dabc2f33a", // How are you doing?
            "5782e38b-a47d-434a-9b67-df366e0840a5", // Goodbye
            "71947512-010c-491d-9460-b7d78b688ee7", // Got any spare change?
            "af3b919b-8f69-406f-84b8-cf1b290e4575", // Warn |story.notewriter.name| about the killer
            "f1c27270-248d-4021-a5b6-3c2c30bae2b9", // I know the password...
            "703eca0b-dc96-4d29-9cf5-d87e52d34049", // I'm a member of the FPOWA. Need a place to crash
            "9b1506ab-d6a0-4ec7-bf74-d8ad21c2d064", // You look like you need a can of Starch Kola. Put some life into it!
        },
        ["Investigation"] = new List<string>
        {
            "8aaa446c-f38e-4654-a4b9-0c37716ba910", // Have you seen or heard anything unusual?
            "c9e93baa-e090-4242-b1ee-33f50271edd9", // Do you know this person?
            "8d0e1aa9-2a97-4b41-81e9-209053393577", // What do you think about the killer?
            "e77a3e32-6d2e-47b2-900a-b78d30164307", // Could you provide your fingerprints for an investigation?
            "d6c5d3de-b4f0-4d01-99e6-ed0df0fb0d69", // How much for a tour of this place?
            "268053d5-2a13-4ae7-8583-77e1aa6680a8", // Health inspector. I need to survey your premises.
            "270fb046-e03e-4f2d-82be-d5e47de6c8e7", // Can I come in and take a look around?
            "2e04903a-54f4-49cc-9ba4-02657562f5c5", // I'll make it worth your while if you let me look around the place...
            "83035934-50cc-49ab-9611-67a6b5c1631d", // Is this |sidejob.random.casualname|?
            "f94eae0b-f691-4577-9d20-fe9cf6ef0593", // |sidejob.random.casualname|? Here about the job...
            "7fd14a69-af7d-47fb-be81-e97671fb9001", // I have information about the affair...
            "f6e73fc7-16b1-423f-82e8-2df62b277103", // Is this your |sidejob.stolenitem.name|?
            "cd4a4551-d2bd-45f4-b467-202e081379ef", // I'm here about the stolen |sidejob.stolenitem.name|, can I take a look around?
            "8dc675bb-ce7f-4758-aea2-84b0c75b827f", // Here about that lost item...
            "7eb7b547-ac14-4529-869a-8d57ba750c45", // I'm looking for a hotel guest...
            "e7e04bd1-ac47-4123-bb75-0d1106eaedba", // Here about the shoes...
            "e1783c63-bcb9-483a-888d-c4e995e7af2b", // Ask about kidnapping...
            "487788e6-1bf9-462e-b163-e137d2371182" // Where is your den?
        },
        ["Transaction"] = new List<string>
        {
            "2ec57d6d-3034-44cc-ba37-7876b97014b6", // I'd like to buy something.
            "39b5d41c-cec7-4696-95c1-b1153b90b40b", // What are your opening hours?
            "41e93330-78ba-4914-9456-adaa162f1f71", // How much for the door codes to this place?
            "f6e0d635-0977-4da2-b5f7-29c691ccf663", // Here's money for the door codes.
            "96d190f6-556c-4bd3-a079-756fc5e0eba2", // Here's the money for a guest pass.
            "d03fcf7c-f0d4-4f0b-88df-0a316632262e", // I'm interested in borrowing some money...
            "a4be688f-5f1d-4e55-8d55-2d6860394a64", // Here's |city.currency||receiver.job.employer.loansharkpayment| towards my debt
            "ae8f7278-6424-4e5a-8544-1355c4e9dcdc", // I agree to the borrowing terms, I want the |city.currency||receiver.job.employer.loansharkloan|
            "dd194ff8-0889-41d0-a974-9128bde320e0", // Forget it
            "77672a96-b943-4d17-8e13-08f0e442acc8", // What rooms do you have available?
            "5ad482ca-1356-47ef-baf5-9adc13edf25c", // I'll take a room for |city.hotelcostlower| crows a night
            "da1e6fab-1a06-4b45-90cc-6d2c3bc883f8", // I'll take a suite for |city.hotelcostupper| crows a night
            "a93ee4da-f436-4115-9c1b-1588a80e7c64", // I'd like to pay my bill...
            "4bdc6c80-c929-4e40-ac2e-70516ea4a4ec", // I can't pay my bill...
            "30221a22-824d-4c6d-9783-43b6b6676586", // I'd like to check out of my room
            "af9962a1-0526-460b-8fb0-00cee66ebe19", // Which number is my room again?
            "4d3fbf2c-94e1-45de-8a55-94484b2bb153", // Do you have a briefcase I can purchase?
            "cb900896-5345-4ad3-a5f7-48e274314e57" // Give item...
        },
        ["Confrontation"] = new List<string>
        {
            "88000f4b-c693-49fd-a020-f48d6c7825b9", // Pay them
            "370b5d7c-7607-4530-9e72-59aa09a6b205", // Not a chance
            "7d3db84f-467c-4dec-90b2-218fa74ee392", // I'm arresting you on suspicion of murder
            "f4fe690f-dc37-4df8-b315-d4878cd96048", // Sure, I'm listening...
            "95aeeabc-0b53-436c-85ab-234571cd42cd", // No way. I will find you, and I will catch you
            "7edb78fa-d11a-4939-b461-211cbf368bb9" // Why did you commit murder?
        },
        ["Other"] = new List<string>
        {
            "354023a2-5c75-4d66-a831-23cfec0199c4", // Pay medical fees and completely recover...
            "17bba270-1793-400d-a464-53edef9adc30" // Attempt escape without paying medical fees...
        }
    };

    private static readonly Dictionary<string, string[]> CategoryKeywords = new Dictionary<string, string[]>
    {
        {
            "Dialogue",
            new string[]
            {
                "name", "hello", "hi", "goodbye", "bye", "how are you", "doing", "greet", "chat", "talk", "spare",
                "give"
            }
        },
        {
            "Investigation",
            new string[]
            {
                "seen", "heard", "know", "unusual", "fingerprint", "tour", "inspect", "survey", "look around",
                "investigate", "information", "about", "looking for", "where is", "find", "passcode"
            }
        },
        {
            "Transaction",
            new string[]
            {
                "buy", "purchase", "sell", "money", "pay", "cost", "price", "hours", "borrow", "loan", "debt", "room",
                "bill", "check out", "item", "trade"
            }
        },
        {
            "Confrontation",
            new string[]
            {
                "arrest", "murder", "suspect", "threat", "no way", "refuse", "confront", "accuse", "why did", "catch"
            }
        },
        { "Other", new string[] { "medical", "recover", "escape", "attempt" } },
        { "All", new string[] { } },
    };
}