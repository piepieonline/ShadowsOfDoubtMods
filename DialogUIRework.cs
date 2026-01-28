    [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.RefreshDialogOptions))]
    public class InteractionController_RefreshDialogOptions
    {
        public static CategorizedDialogUI CategorizedUI;
        private static bool _inRefreshDialogOptions;

        public static void Postfix()
        {
            if (_inRefreshDialogOptions)
                return;

            _inRefreshDialogOptions = true;
            try
            {
                DebugModPlugin.PluginLogger.LogInfo("----------------- Refreshing dialog options -----------------");
                // PrefabControls.Instance.dialogOption, PrefabControls.Instance.dialogOptionContainer

    private GameObject _lastDialogContainer;
    private int _lastChildCount = -1;
    private bool _isBuilding;

                if (CategorizedUI == null)
                {
                    CategorizedUI = new CategorizedDialogUI();
                    PrefabControls.Instance.dialogOptionContainer.parent.Find("Header").localPosition += new Vector3(0, 45, 0);
                    PrefabControls.Instance.dialogOptionContainer.parent.Find("Border").gameObject.SetActive(false);
                    Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<TabNavigationComponent>();
                    var nav = PrefabControls.Instance.dialogOptionContainer.gameObject.AddComponent<TabNavigationComponent>();
                    nav.dialogUI = CategorizedUI;
                }
                CategorizedUI.ModifyDialogContainer(PrefabControls.Instance.dialogOptionContainer.gameObject);
            }
            finally
            {
                _inRefreshDialogOptions = false;
            }
        }
    }

    public void EnsureFreshOptions(GameObject dialogContainer)
    {
        if (_isBuilding)
            return;

        if (dialogContainer == null)
            return;

        // If container changed or child count changed, our cached references are likely stale.
        if (!ReferenceEquals(_lastDialogContainer, dialogContainer) ||
            _lastChildCount != dialogContainer.transform.childCount ||
            categorizedOptions == null ||
            HasNullOptionReference())
        {
            ModifyDialogContainer(dialogContainer);

            // If current category vanished after rebuild, fall back safely.
            if (currentCategory == null || categoryOrder == null || !categoryOrder.Contains(currentCategory))
                SwitchToTab("All");
        }
    }

    private bool HasNullOptionReference()
    {
        if (categorizedOptions == null)
            return true;

        foreach (var kvp in categorizedOptions)
        {
            var list = kvp.Value;
            if (list == null)
                return true;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                    return true;
            }
        }
        return false;
    }
