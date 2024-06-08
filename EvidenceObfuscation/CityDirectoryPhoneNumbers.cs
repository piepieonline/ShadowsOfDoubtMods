using EvidenceObfuscation;
using HarmonyLib;
using System.Collections.Generic;

namespace EvidenceLinkModifiers
{
    [HarmonyPatch(typeof(CityData), nameof(CityData.CreateCityDirectory))]
    public class CityData_CreateCityDirectory
    {
        public static void Postfix(CityData __instance)
        {
            if (!EvidenceObfuscationPlugin.ModifyCityDirectory.Value)
            {
                return;
            }

            EvidenceObfuscationPlugin.PluginLogger.LogInfo($"Started updating city directory");
            var currentLinkToPhoneNumber = new Dictionary<string, int>();

            foreach (Company company in CityData.Instance.companyDirectory)
            {
                if (GetPhoneEntry(company?.address, out var phoneEntryLink))
                {
                    if (company?.name != null)
                        currentLinkToPhoneNumber[company.name] = phoneEntryLink;
                }
            }
            foreach (NewAddress newAddress in CityData.Instance.addressDirectory)
            {
                if (GetPhoneEntry(newAddress, out var phoneEntryLink))
                {
                    currentLinkToPhoneNumber[newAddress.name] = phoneEntryLink;
                }
            }

            foreach (Citizen citizen in CityData.Instance.citizenDirectory)
            {
                if(ChapterController.Instance.chapterScript)
                {
                    ChapterIntro chapterIntro = ((dynamic)ChapterController.Instance.chapterScript).Cast<ChapterIntro>();
                    if (Toolbox.Instance.IsStoryMissionActive(out var _, out var _) && (chapterIntro?.kidnapper?.humanID == citizen?.humanID))
                    {
                        // Skip the tutorial character to prevent breaking it
                        continue;
                    }
                }

                if (GetPhoneEntry(citizen?.home, out var phoneEntryLink))
                {
                    var eviList = new Il2CppSystem.Collections.Generic.List<Evidence.DataKey>();
                    eviList.Add(Evidence.DataKey.initialedName);
                    eviList.Add(Evidence.DataKey.telephoneNumber);

                    if (EvidenceObfuscationPlugin.ShowAddressInCitizenCard.Value)
                    {
                        eviList.Add(Evidence.DataKey.address);
                    }

                    var combinedLink = Strings.AddOrGetLink(citizen.evidenceEntry, eviList);

                    currentLinkToPhoneNumber[citizen.GetInitialledName()] = combinedLink.id;
                }
            }

            var keys = new List<int>();

            foreach (var item in __instance.cityDirText)
            {
                keys.Add(item.Key);
            }

            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                __instance.cityDirText[key] = System.Text.RegularExpressions.Regex.Replace(__instance.cityDirText[key], "<link=(\\d+)>(.*)</link>", (match) =>
                {
                    // var existingId = int.Parse(match.Groups[1].Value);
                    var existingId = match.Groups[2].Value;
                    if (currentLinkToPhoneNumber.ContainsKey(existingId))
                    {
                        // PluginLogger.LogInfo($"EvidenceObfuscationPlugin: Updating link from {existingId} to {currentLinkToPhoneNumber[existingId]}");
                        return $"<link={currentLinkToPhoneNumber[existingId]}>{match.Groups[2].Value}</link>";
                    }
                    return match.Groups[0].Value;
                });
            }

            EvidenceObfuscationPlugin.PluginLogger.LogInfo($"Updated city directory");
        }

        private static bool GetPhoneEntry(NewAddress phoneAddress, out int evidenceLink)
        {
            Strings.LinkData phoneEntry = null;
            evidenceLink = -1;
            if (phoneAddress?.telephones?.Count > 0)
            {
                phoneEntry = Strings.AddOrGetLink(phoneAddress.telephones[0].telephoneEntry);
                if (phoneEntry != null)
                {
                    evidenceLink = phoneEntry.id;
                }
            }
            return phoneEntry != null && evidenceLink != -1;
        }
    }

    [HarmonyPatch(typeof(ChapterController), nameof(ChapterController.LoadPart), new System.Type[] { typeof(int), typeof(bool), typeof(bool) })]
    public class ChapterController_LoadPart
    {
        public static void Postfix()
        {
            if(Toolbox.Instance.IsStoryMissionActive(out var _, out var _))
            {
                CityData.Instance.CreateCityDirectory();
            }
        }
    }
}