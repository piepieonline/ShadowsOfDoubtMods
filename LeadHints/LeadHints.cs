using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;

using System.Text.RegularExpressions;


#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace LeadHints
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class LeadHintsPlugin : BaseUnityPlugin
#elif IL2CPP
    public class LeadHintsPlugin : BasePlugin
#endif
    {
        public static ConfigEntry<bool> Enabled;

        public static ManualLogSource PluginLogger;

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif
            // Plugin startup logic

            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required)");

            if (Enabled.Value)
            {
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();
                PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
            }
        }

        [HarmonyPatch(typeof(ObjectivesContentController), nameof(ObjectivesContentController.UpdateJobDetails))]
        public class ObjectivesContentController_UpdateJobDetails
        {
            public static bool Prefix(ObjectivesContentController __instance)
            {
                // Pretty much a reimplementation of the current method

                if (__instance.job == null || !__instance.job.knowHandInLocation)
                {
                    __instance.jobDetails.text = CalculateJobText(__instance.job); // Strings.Get("ui.interface", "Persue current objectives detailed in the job note to acquire more details about the job...");
                    __instance.jobDetails.ForceMeshUpdate(false, false);
                    __instance.pageRect.sizeDelta = new Vector2(__instance.pageRect.sizeDelta.x, Mathf.Max(__instance.jobDetails.GetPreferredValues().y + 32f, 466f));
                    __instance.pageRect.anchoredPosition = new Vector2(0.0f, __instance.pageRect.sizeDelta.y * -0.5f);
                    return false;
                }

                return true;
            }
        }

        public static string ConvertDataKeyToName(Evidence.DataKey lead, string prefix = "")
        {
            string stringLead = System.Enum.GetName<Evidence.DataKey>(lead);
            // Some basic transformations for default responses
            // Remove 'random'
            stringLead = stringLead.Replace("random", "");
            // Turn camel case into spaces
            stringLead = Regex.Replace(stringLead, "(\\B[A-Z](?!\\B[A-Z]))", " $1");
            // Upper case the first letter
            stringLead = char.ToUpper(stringLead[0]) + stringLead.Substring(1);

            switch (lead)
            {
                case Evidence.DataKey.shoeSizeEstimate:
                    stringLead = "Shoe Size (Estimate)";
                    break;
                case Evidence.DataKey.heightEstimate:
                    stringLead = "Height (Estimate)";
                    break;
            }

            if (prefix != "" && stringLead == "Name")
            {
                return prefix;
            }
            else if (prefix != "")
            {
                return prefix + " " + stringLead;
            }
            else
            {
                return stringLead;
            }
        }

        public static string CalculateJobText(SideJob job)
        {
            SortedSet<string> perpDetail = new SortedSet<string>();
            Dictionary<string, SortedSet<string>> otherDetails = new Dictionary<string, SortedSet<string>>();

            otherDetails.Add("Paramour", new SortedSet<string>());
            otherDetails.Add("Poster", new SortedSet<string>());
            otherDetails.Add("Other", new SortedSet<string>());

            foreach (var lead in job.leadKeys)
            {
                perpDetail.Add(ConvertDataKeyToName(lead));
            }

            foreach (var lead in job.preset.informationAcquisitionLeads)
            {
                foreach (var l in lead.keys)
                {
                    switch (lead.leadEvidence)
                    {
                        // Purp
                        case JobPreset.LeadEvidence.purp:
                            perpDetail.Add(ConvertDataKeyToName(l)); break;
                        case JobPreset.LeadEvidence.purpsHome:
                            perpDetail.Add(ConvertDataKeyToName(l, "Home")); break;
                        case JobPreset.LeadEvidence.purpsWorkplace:
                            perpDetail.Add(ConvertDataKeyToName(l, "Work")); break;
                        case JobPreset.LeadEvidence.purpsBuilding:
                            perpDetail.Add(ConvertDataKeyToName(l, "Home Building")); break;
                        case JobPreset.LeadEvidence.purpsTelephone:
                            // Skip, should be covered in leadKeys
                            break;
                        case JobPreset.LeadEvidence.purpsWorkplaceBuilding:
                            perpDetail.Add(ConvertDataKeyToName(l, "Work Building")); break;

                        // Paramour
                        case JobPreset.LeadEvidence.purpsParamour:
                            perpDetail.Add(ConvertDataKeyToName(l)); break;
                        case JobPreset.LeadEvidence.purpsParamourBuilding:
                            otherDetails["Paramour"].Add(ConvertDataKeyToName(l, "Home Building")); break;
                        case JobPreset.LeadEvidence.purpsParamourHome:
                            otherDetails["Paramour"].Add(ConvertDataKeyToName(l, "Home")); break;
                        case JobPreset.LeadEvidence.purpsParamourTelephone:
                            break;
                        case JobPreset.LeadEvidence.purpsParamourWorkplace:
                            otherDetails["Paramour"].Add(ConvertDataKeyToName(l, "Work")); break;
                        case JobPreset.LeadEvidence.purpsParamourWorkplaceBuilding:
                            otherDetails["Paramour"].Add(ConvertDataKeyToName(l, "Work Building")); break;

                        // Poster
                        case JobPreset.LeadEvidence.poster:
                            otherDetails["Poster"].Add(ConvertDataKeyToName(l)); break;

                        // Unknown?
                        default:
                            otherDetails["Other"].Add(ConvertDataKeyToName(l));
                            break;
                    }
                }
            }


            var leadsText = "\nLeads:\n";

            if (perpDetail.Count > 0)
            {
                leadsText += "Purp details:\n";
            }

            foreach (var lead in perpDetail)
            {
                leadsText += "\t" + lead + "\n";
            }

            foreach (var key in otherDetails.Keys)
            {
                if (otherDetails[key].Count == 0)
                    continue;

                leadsText += key + "\n";

                foreach (var lead in otherDetails[key])
                {
                    leadsText += "\t" + lead + "\n";
                }
            }

            leadsText = leadsText + "\n";

            return leadsText;
        }
    }
}
