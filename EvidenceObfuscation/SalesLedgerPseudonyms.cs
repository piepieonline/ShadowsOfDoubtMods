using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using EvidenceObfuscation;

namespace EvidenceLinkModifiers
{
    [HarmonyPatch(typeof(SalesLedgerEntryController), nameof(SalesLedgerEntryController.Setup))]
    public class SalesLedgerEntryController_Setup
    {
        public static bool Prefix(SalesLedgerEntryController __instance, SalesLedgerContentController newSalesLedger, Company.SalesRecord newRecord)
        {
            if(!EvidenceObfuscationPlugin.ModifySalesLedgers.Value)
            {
                return true;
            }

            // Copied and modified from the base game method
            __instance.salesLedger = newSalesLedger;
            __instance.salesRecord = newRecord;

            // This is the modification of the base game method
            CreateEvidenceLink(__instance);

            __instance.descriptionText.text = "";
            float y = 38f;
            Company company = __instance.salesRecord.GetCompany();
            for (int index = 0; index < __instance.salesRecord.items.Count; ++index)
            {
                InteractablePreset interactablePreset = Toolbox.Instance.GetInteractablePreset(__instance.salesRecord.items[index]);
                float input = 0.0f;
                if (company.prices.ContainsKey(interactablePreset))
                    input = company.prices[interactablePreset];
                TextMeshProUGUI descriptionText = __instance.descriptionText;
                descriptionText.text = descriptionText.text + "<align=\"left\">" + Strings.Get("evidence.names", interactablePreset.name) + "  <align=\"right\">" + CityControls.Instance.cityCurrency + Toolbox.Instance.RoundToPlaces(input, 2).ToString();
                __instance.descriptionText.text += "\n";
                __instance.descriptionText.rectTransform.sizeDelta = new Vector2(__instance.descriptionText.rectTransform.sizeDelta.x, __instance.descriptionText.rectTransform.sizeDelta.y + 20f);
                y += 20f;
            }

            __instance.rect.sizeDelta = new Vector2(__instance.rect.sizeDelta.x, y);
            __instance.timeText.text = "<link=" + Strings.AddOrGetLink((Evidence)EvidenceCreator.Instance.GetTimeEvidence(__instance.salesRecord.time, __instance.salesRecord.time, parentID: __instance.salesLedger.parentWindow.passedEvidence.evID)).id.ToString() + ">" + SessionData.Instance.ShortDateString(__instance.salesRecord.time, false) + " " + SessionData.Instance.GameTimeToClock24String(__instance.salesRecord.time, false) + "</link>";
            __instance.priceText.text = CityControls.Instance.cityCurrency + Toolbox.Instance.RoundToPlaces(__instance.salesRecord.cost, 2).ToString();

            return false;
        }

        private static void CreateEvidenceLink(SalesLedgerEntryController __instance)
        {
            var punter = __instance.salesRecord.GetPunter();
            var evidenceLinkList = new Il2CppSystem.Collections.Generic.List<Evidence.DataKey>();

            var saleEvidenceTypeKey = __instance.salesRecord.punterID % 4;

            switch(saleEvidenceTypeKey)
            {
                case 1:
                    evidenceLinkList.Add(Evidence.DataKey.initials);
                    __instance.nameText.text = "<link=" + Strings.AddOrGetLink(punter.evidenceEntry, evidenceLinkList).id.ToString() + ">" + punter.GetInitials() + "</link>";
                    break;
                case 2:
                    evidenceLinkList.Add(Evidence.DataKey.work);
                    __instance.nameText.text = "<link=" + Strings.AddOrGetLink(punter.evidenceEntry, evidenceLinkList).id.ToString() + ">" + punter.job.employer.name + "</link>";
                    break;
                case 3:
                    evidenceLinkList.Add(Evidence.DataKey.firstName);
                    __instance.nameText.text = "<link=" + Strings.AddOrGetLink(punter.evidenceEntry, evidenceLinkList).id.ToString() + ">" + punter.GetFirstName() + "</link>";
                    break;
                default:
                    // Standard option
                    evidenceLinkList.Add(Evidence.DataKey.initialedName);
                    __instance.nameText.text = "<link=" + Strings.AddOrGetLink(punter.evidenceEntry, evidenceLinkList).id.ToString() + ">" + punter.GetInitialledName() + "</link>";
                    break;
            }

        }
    }
}
