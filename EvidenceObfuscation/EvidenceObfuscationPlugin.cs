using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EvidenceObfuscation
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class EvidenceObfuscationPlugin : BaseUnityPlugin
#elif IL2CPP
    public class EvidenceObfuscationPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        public static ConfigEntry<bool> ModifyCityDirectory;
        public static ConfigEntry<bool> ShowAddressInCitizenCard;

        public static ConfigEntry<bool> EmployeeRecord_Printed_FingerprintsRemoved;
        public static ConfigEntry<bool> EmployeeRecord_Filling_FingerprintsRemoved;

        public static ConfigEntry<bool> GovDatabase_FingerprintsRemoved;

        public static ConfigEntry<bool> MurderWeapon_EntryWoundRemoval;
        public static ConfigEntry<bool> MurderWeapon_GunTypeRemoved;
        public static ConfigEntry<bool> MurderWeapon_MeleeTypeRemoved;

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            ModifyCityDirectory = Config.Bind("City Directory", "Should the directory be modified to be phone numbers instead of addresses?", true);
            ShowAddressInCitizenCard = Config.Bind("City Directory", "Should the directory entry for citizens also include their address?", false);

            EmployeeRecord_Printed_FingerprintsRemoved = Config.Bind("Employee Record", "Should fingerprints be removed from employee records printed from a cruncher?", false);
            EmployeeRecord_Filling_FingerprintsRemoved = Config.Bind("Employee Record", "Should fingerprints be removed from employee records found in a filling cabinet?", false);

            GovDatabase_FingerprintsRemoved = Config.Bind("Government Database", "Should fingerprints be removed from the government database?", false);

            MurderWeapon_EntryWoundRemoval = Config.Bind("Murder Weapon", "Should entry wound evidence be removed?", false, "Recommended true, as these pieces of evidence are kind of broken anyway");
            MurderWeapon_GunTypeRemoved = Config.Bind("Murder Weapon", "Should the gun type be removed from the inspection of the body?", false);
            MurderWeapon_MeleeTypeRemoved = Config.Bind("Murder Weapon", "Should the melee weapon type be removed from the inspection of the body?", false);

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }

        public static void RemoveMessageMatching(DDSSaveClasses.DDSTreeSave tree, string name)
        {
            for (int i = tree.messages.Count - 1; i >= 0; i--)
            {
                if (tree.messages[i].elementName == name)
                {
                    tree.messages.RemoveAt(i);
                }
            }
        }
    }
}
