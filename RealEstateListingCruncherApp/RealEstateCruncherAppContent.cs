using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using UniverseLib;
using static ComputerOSMultiSelect;

namespace RealEstateListingCruncherApp
{

    public class RealEstateCruncherAppContent : CruncherAppContent
    {
        public ComputerOSMultiSelect list;
        public CruncherForSaleContent forSaleController;
        public static Dictionary<string, Interactable> optionTextToSaleNote = new Dictionary<string, Interactable>();

        ChangePage changePageDelegate;


        // This one seems to be retired?
        public override void Setup(ComputerController cc)
        {
            base.controller = cc;
            DoSetup();
        }

        public override void OnSetup()
        {
            DoSetup();
        }

        private void DoSetup()
        {
            GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.name == "Exit").FirstOrDefault().onClick.AddListener(() => controller.OnAppExit());
            list = GetComponentInChildren<ComputerOSMultiSelect>();

            changePageDelegate = new System.Action(UpdateSearch);
            list.OnChangePage += changePageDelegate;

            forSaleController = transform.Find("ApartmentSaleContent").gameObject.AddComponent<CruncherForSaleContent>();

            UpdateSearch();
        }
        private void OnDestroy()
        {
            list.OnChangePage -= changePageDelegate;
        }

        public override void PrintButton()
        {
        }

        public void UpdateSearch()
        {
            var newOptions = new Il2CppSystem.Collections.Generic.List<ComputerOSMultiSelect.OSMultiOption>();

            optionTextToSaleNote.Clear();
            foreach (var add in GameplayController.Instance.forSale)
            {
                // Sometimes the sale note is null, which is a base game bug (Related to the tutorial?)
                if (add.saleNote == null) continue;

                var lastOption = new ComputerOSMultiSelect.OSMultiOption() { text = add.name };
                // var lastOption = new AddressOption() { text = add.name, address = add }; // Not working :(
                optionTextToSaleNote[lastOption.text] = add.saleNote;
                newOptions.Add(lastOption);
            }

            list.UpdateElements(newOptions);
            list.usePages = newOptions.Count > list.maxPerPage;

            foreach (var selectionElement in GetComponentsInChildren<ComputerOSMultiSelectElement>(true))
            {
                selectionElement.button.onClick.AddListener(() =>
                {
                    forSaleController.UpdateContent(optionTextToSaleNote[selectionElement.elementText.text]);
                });
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
                salesDataText = transform.Find("Sales Data Text").GetComponent<TMPro.TextMeshProUGUI>();
                descriptionText = transform.Find("Description").GetComponent<TMPro.TextMeshProUGUI>();
                purchaseText = transform.Find("PurchaseButton/Text").GetComponent<TMPro.TextMeshProUGUI>();
                previewImage = transform.Find("Photo/RawImage").GetComponent<RawImage>();
                purchaseButton = transform.Find("PurchaseButton").GetComponent<Button>();
            }

            void Start()
            {
                if (interactable == null)
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
                purchaseText.text = Strings.Get("evidence.generic", "Purchase") + " " + CityControls._instance.cityCurrency + interactable.forSale.GetPrice(false).ToString();

                previewImage.texture = interactable.forSale.evidenceEntry.GetPhoto(Toolbox.Instance.allDataKeys);

                purchaseButton.onClick.AddListener(() =>
                {
                    if (GameplayController._instance.money >= interactable.forSale.GetPrice(false))
                    {
                        GameplayController._instance.AddMoney(-interactable.forSale.GetPrice(false), true, "Property purchase");
                        PlayerApartmentController._instance.BuyNewResidence(interactable.forSale.residence);
                        GetComponentInParent<RealEstateCruncherAppContent>().UpdateSearch();
                    }
                });

                purchaseButton.enabled = GameplayController._instance.money >= interactable.forSale.GetPrice(false);
            }
        }
    }
}
