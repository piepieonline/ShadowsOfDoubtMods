using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Configuration;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace CityDirectoryPhoneNumbers
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class CityDirectoryPhoneNumbersPlugin : BaseUnityPlugin
#elif IL2CPP
    public class CityDirectoryPhoneNumbersPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        private static ConfigEntry<bool> ShowAddressInCitizenCard;

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
            ShowAddressInCitizenCard = Config.Bind("General", "Should the directory entry for citizens also include their address?", false);

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }

        [HarmonyPatch(typeof(CityData), nameof(CityData.CreateCityDirectory))]
        public class CityData_CreateCityDirectory
        {
            public static void Postfix(CityData __instance)
            {
                PluginLogger.LogInfo($"CityDirectoryPhoneNumbersPlugin: Started updating city directory");
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

                        if (ShowAddressInCitizenCard.Value)
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
                            // PluginLogger.LogInfo($"CityDirectoryPhoneNumbersPlugin: Updating link from {existingId} to {currentLinkToPhoneNumber[existingId]}");
                            return $"<link={currentLinkToPhoneNumber[existingId]}>{match.Groups[2].Value}</link>";
                        }
                        return match.Groups[0].Value;
                    });
                }

                PluginLogger.LogInfo($"CityDirectoryPhoneNumbersPlugin: Updated city directory");
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
}