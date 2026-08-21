using HarmonyLib;
using UnityEngine;

namespace DialogUIRework;

public class DialogUIReworkHooks
{
    [HarmonyPatch(typeof(PrefabControls), nameof(PrefabControls.Awake))]
    public class PrefabControls_Awake
    {
        public static void Postfix()
        {
            if (DialogUIReworkPlugin.TabbedDialogUI == null)
            {
                DialogUIReworkPlugin.TabbedDialogUI = new TabbedDialogUI();
                Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<TabNavigationComponent>();
            }
            
            DialogUIReworkPlugin.TabbedDialogUI.CreateDialogUI();
        }
    }
    
    [HarmonyPatch(typeof(InputController), nameof(InputController.SetMouseInputMode))]
    public class InputController_SetMouseInputMode
    {
        public static void Postfix()
        {
            DialogUIReworkPlugin.TabbedDialogUI?.UpdateControlGlyphs();
        }
    }

    [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.Update))]
    public class InteractionController_Update
    {
        public static void Prefix()
        {
            if (DialogUIReworkPlugin.TabbedDialogUI == null)
                return;

            var opts = DialogUIReworkPlugin.TabbedDialogUI.GetTabDialogOptions();

            if (opts == null)
                return;

            InteractionController.Instance.dialogOptions = opts;
        }

        public static void Postfix()
        {
            if (DialogUIReworkPlugin.TabbedDialogUI == null)
                return;

            var opts = DialogUIReworkPlugin.TabbedDialogUI.GetTabDialogOptions("All");

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
            if (DialogUIReworkPlugin.TabbedDialogUI == null)
                return;

            if (!val)
            {
                DialogUIReworkPlugin.TabbedDialogUI.ClearDialogOptions();
            }
        }
    }

    [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.RefreshDialogOptions))]
    public class InteractionController_RefreshDialogOptions
    {
        public static void Postfix()
        {
            if(PrefabControls.Instance.dialogOptionContainer.childCount > 0)
                DialogUIReworkPlugin.TabbedDialogUI.ModifyDialogContainer();
        }
    }

    [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.SetDialogSelection))]
    public class InteractionController_SetDialogSelection
    {
        public static bool Prefix(InteractionController __instance, int newVal)
        {
            if (DialogUIReworkPlugin.TabbedDialogUI == null ||
                DialogUIReworkPlugin.TabbedDialogUI.GetTabDialogOptions() == null)
                return true;

            var currentDialog = DialogUIReworkPlugin.TabbedDialogUI.GetTabDialogOptions();

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
    }
}