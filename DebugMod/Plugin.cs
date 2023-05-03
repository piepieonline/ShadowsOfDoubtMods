using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using DebugMod;
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Globalization;
using Il2CppSystem.Runtime.CompilerServices;
using Il2CppSystem.Text.RegularExpressions;
using MonoMod.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using static DebugMod.Hooks;

namespace DebugMod
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static ManualLogSource Logger;

        public static UniverseLib.AssetBundle customAssets;
        public static UnityEngine.Object[] allCustomAssets;

        public override void Load()
        {
            return;

            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{PluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is patched!");

            ClassInjector.RegisterTypeInIl2Cpp<CustomCruncherApp>();
            ClassInjector.RegisterTypeInIl2Cpp<CustomCruncherApp.CruncherForSaleContent>();
            ClassInjector.RegisterTypeInIl2Cpp<AddressOption>();

            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} has added custom types!");

            Plugin.Logger.LogInfo("Loading custom asset bundle start");

            Plugin.customAssets = UniverseLib.AssetBundle.LoadFromFile("D:\\UnityDev\\ShadowsOfDoubtModding\\Assets\\AssetBundles\\debugmodassets");
            // Plugin.customAssets = UniverseLib.AssetBundle.LoadFromFile("E:\\SteamLibrary\\steamapps\\common\\Shadows of Doubt\\Shadows of Doubt_Data\\debugmodassets");
            if (Plugin.customAssets == null)
            {
                Plugin.Logger.LogInfo("Failed to load AssetBundle!");
            }

            Plugin.allCustomAssets = Plugin.customAssets.LoadAllAssets();


            Plugin.Logger.LogInfo("Loading custom asset bundle complete");
        }

        public static void Assets()
        {
            var bedroom = Resources.FindObjectsOfTypeAll<RoomTypeFilter>().Where(roomType => roomType.name == "LivingRoom").FirstOrDefault();
            Plugin.Logger.LogInfo($"1 {bedroom}");
            var apartmentAddressPreset = Resources.FindObjectsOfTypeAll<AddressPreset>().Where(roomType => roomType.name == "Apartment").FirstOrDefault();
            Plugin.Logger.LogInfo($"2 {apartmentAddressPreset}");
            var phoneBoothFurniturePreset = Resources.FindObjectsOfTypeAll<FurniturePreset>().Where(furniturePreset => furniturePreset.name == "TelephoneBooth").FirstOrDefault();
            Plugin.Logger.LogInfo($"3 {phoneBoothFurniturePreset}");
            var tvFurnitureClass = Resources.FindObjectsOfTypeAll<FurnitureClass>().Where(furnitureClass => furnitureClass.name == "1x1Television").FirstOrDefault();
            Plugin.Logger.LogInfo($"4 {phoneBoothFurniturePreset}");

            phoneBoothFurniturePreset.allowedInAddressesOfType.Add(apartmentAddressPreset);
            phoneBoothFurniturePreset.allowedRoomFilters.Add(bedroom);
            phoneBoothFurniturePreset.classes.Add(tvFurnitureClass);

            Plugin.Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} has custom assets!");
        }

        public static T FindByTypeAndName<T>(string name) where T : UnityEngine.Object
        {
            if (!typeof(UnityEngine.GameObject).IsAssignableFrom(typeof(T)))
                throw new System.ArgumentException();

            return Resources.FindObjectsOfTypeAll<T>().Where(obj => obj.name == name).FirstOrDefault();
        }

        public static Company FindNearestThatSells(string itemName)
        {
            // return Toolbox.Instance.FindNearestThatSells(FindByTypeAndName<InteractablePreset>(itemName), Resources.FindObjectsOfTypeAll<Player>()[0].currentGameLocation);
            return Toolbox.Instance.FindNearestThatSells(Resources.FindObjectsOfTypeAll<InteractablePreset>().Where(obj => obj.name == itemName).FirstOrDefault(), Resources.FindObjectsOfTypeAll<Player>()[0].currentGameLocation);
        }

        public static void CreateTestCube()
        {
            GameObject.Instantiate(customAssets.LoadAsset<GameObject>("TestCube"), Player.Instance.transform.position, Quaternion.identity);
        }
    }
}

public class CustomCruncherApp : CruncherAppContent
{
    public static CruncherAppPreset myPreset;

    public ComputerOSMultiSelect list;
    public CruncherForSaleContent forSaleController;
    public static Dictionary<string, Interactable> optionTextToSaleNote = new Dictionary<string, Interactable>();

    public override void Setup(ComputerController cc)
    {
        Plugin.Logger.LogInfo($"Custom Cruncher Setup");
        base.controller = cc;

        DoSetup();
    }

    public override void OnSetup()
    {
        Plugin.Logger.LogInfo($"Custom Cruncher OnSetup");

        DoSetup();

        Plugin.Logger.LogInfo($"cc: {controller}");
    }

    private void DoSetup()
    {
        /*
        var exitButtonGO = transform.Find("ExitButton").gameObject;
        var exitButton = exitButtonGO.GetComponent<UnityEngine.UI.Button>();
        exitButtonGO.AddComponent<ComputerOSUIComponent>().button = exitButton;
        exitButton.onClick
        */
        GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.name == "Exit").FirstOrDefault().onClick.AddListener(() => controller.OnAppExit());
        list = GetComponentInChildren<ComputerOSMultiSelect>();
        forSaleController = transform.Find("ApartmentSaleContent").gameObject.AddComponent<CruncherForSaleContent>();

        UpdateSearch();
    }

    public override void PrintButton()
    {  
        Plugin.Logger.LogInfo($"Custom Cruncher PrintButton");
    }

    public void UpdateSearch()
    {
        Plugin.Logger.LogInfo($"CustomDatabaseApp UpdateSearch");

        var newOptions = new Il2CppSystem.Collections.Generic.List<ComputerOSMultiSelect.OSMultiOption>();

        optionTextToSaleNote.Clear();
        foreach (var add in GameplayController.Instance.forSale)
        {
            var lastOption = new ComputerOSMultiSelect.OSMultiOption() { text = add.name };
            // var lastOption = new AddressOption() { text = add.name, address = add }; // Not working :(
            optionTextToSaleNote[lastOption.text] = add.saleNote;
            newOptions.Add(lastOption);
        }

        list.UpdateElements(newOptions);
        list.usePages = newOptions.Count > list.maxPerPage;

        foreach (var selectionElement in GetComponentsInChildren<ComputerOSMultiSelectElement>())
        {
            selectionElement.button.onClick.AddListener(() =>
            {
                forSaleController.UpdateContent(optionTextToSaleNote[selectionElement.elementText.text]);
            });
        }
    }

    public static void OpenCustomApp()
    {
        var cruncher = Resources.FindObjectsOfTypeAll<ComputerController>().Where(res => res.playerControlled).FirstOrDefault();
        var desktop = cruncher.GetComponentInChildren<DesktopApp>();
        if (cruncher != null)
        {
            if(desktop != null)
            {
                desktop.OnDesktopAppSelect(myPreset);
            }
            else
            {
                cruncher.SetComputerApp(myPreset, true);
            }
        }
    }

    public class CruncherForSaleContent : MonoBehaviour
    {
        TMPro.TextMeshProUGUI salesDataText;
        TMPro.TextMeshProUGUI descriptionText;
        TMPro.TextMeshProUGUI purchaseText;
        RawImage previewImage;
        Button purchaseButton;

        Interactable interactable;

        void Awake()
        {
            Plugin.Logger.LogInfo($"CustomDatabaseApp UpdateSearch");
            salesDataText = transform.Find("Sales Data Text").GetComponent<TMPro.TextMeshProUGUI>();
            descriptionText = transform.Find("Description").GetComponent<TMPro.TextMeshProUGUI>();
            purchaseText = transform.Find("PurchaseButton/Text").GetComponent<TMPro.TextMeshProUGUI>();
            previewImage = transform.Find("Photo/RawImage").GetComponent<RawImage>();
            purchaseButton = transform.Find("PurchaseButton").GetComponent<Button>();
        }

        void Start()
        {
            if(interactable == null)
            {
                gameObject.SetActive(false);
            }
        }

        public void UpdateContent(Interactable interactable)
        {
            this.interactable = interactable;

            purchaseButton.onClick.RemoveAllListeners();

            if (interactable == null || interactable.forSale == false || interactable.forSale.thisAsAddress == null)
            {
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(true);
            }

            salesDataText.text = Strings.GetTextForComponent("600d4a18-7306-4871-a68e-e7764ae62f81", interactable, linkSetting: Strings.LinkSetting.forceNoLinks);
            descriptionText.text = Strings.GetTextForComponent("3651e904-22e5-4093-9660-e59140ea6176", interactable, dataKeys: Toolbox.Instance.allDataKeys);
            purchaseText.text = Strings.Get("evidence.generic", "Purchase") + " " + CityControls._instance.cityCurrency + Number.FormatInt32(interactable.forSale.GetPrice(false), null, NumberFormatInfo.CurrentInfo);

            previewImage.texture = interactable.forSale.evidenceEntry.GetPhoto(Toolbox.Instance.allDataKeys);

            purchaseButton.onClick.AddListener(() =>
            {
                if(GameplayController._instance.money >= interactable.forSale.GetPrice(false))
                {
                    GameplayController._instance.AddMoney(-interactable.forSale.GetPrice(false), true, "Property purchase");
                    PlayerApartmentController._instance.BuyNewResidence(interactable.forSale.residence);
                    GetComponentInParent<CustomCruncherApp>().UpdateSearch();
                }
            });

            purchaseButton.enabled = GameplayController._instance.money >= interactable.forSale.GetPrice(false);
        }
    }
}