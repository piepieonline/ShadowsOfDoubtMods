using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;

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
                    if (company?.address?.telephones.Count > 0)
                    {
                        currentLinkToPhoneNumber[company.name] = Strings.AddOrGetLink(company.address.telephones[0].telephoneEntry).id;
                    }
                }
                foreach (NewAddress newAddress in CityData.Instance.addressDirectory)
                {
                    if (newAddress?.telephones.Count > 0)
                    {
                        currentLinkToPhoneNumber[newAddress.name] = Strings.AddOrGetLink(newAddress.telephones[0].telephoneEntry).id;
                    }
                }
                foreach (Citizen citizen in CityData.Instance.citizenDirectory)
                {
                    if (citizen.home?.telephones.Count > 0)
                    {
                        currentLinkToPhoneNumber[citizen.GetInitialledName()] = Strings.AddOrGetLink(citizen.home.telephones[0].telephoneEntry).id;
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
        }
    }
}