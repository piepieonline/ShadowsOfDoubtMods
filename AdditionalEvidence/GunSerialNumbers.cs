using AssetBundleLoader;
using Cpp2IL.Core.Api;
using DDSScriptExtensions;
using HarmonyLib;
using SOD.Common;
using SOD.Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniverseLib;
using static InteractableController;

namespace AdditionalEvidence
{
    /*
     * TODO: Replace ID system - either hashed value, or fingerprint type a/b/c system (or partial prints system)
     * 
     * Should murderers drop guns so the player can sometimes get serials off the dropped weapons?
     * - Override MurderController.WeaponDisposal() to put it in a bin?
     * 
     * Bullet head stamps
     * - TODO: Add a sync disk to find the answer yourself? Maybe more generally useful for various evidence types
     * - Add a cost? Would require a custom dialog option etc
     * 
     * Add a book (like a sales ledger) to the enforcers office, where someone is tracking which serial numbers go to which arms dealer
     */

    internal class GunSerialNumbers
    {
        public static Dictionary<int, Interactable> SpawnedBulletsToGunIdMap = new Dictionary<int, Interactable>();
        public static Interactable ItemGivenToHuman;

        private static uint _citySeedCache;
        private static Dictionary<string, Dictionary<int, string>> interactableIdCache = new Dictionary<string, Dictionary<int, string>>();

        public static void Init()
        {
            interactableIdCache["HeadStamp"] = new Dictionary<int, string>();
            interactableIdCache["WeaponSerial"] = new Dictionary<int, string>();
        }

        public static void Load(dynamic loadedData)
        {
            _citySeedCache = Lib.SaveGame.GetUniqueNumber(CityData.Instance.seed);

            interactableIdCache["HeadStamp"] = new Dictionary<int, string>();
            interactableIdCache["WeaponSerial"] = new Dictionary<int, string>();

            SpawnedBulletsToGunIdMap.Clear();

            Dictionary<int, int> loadedBullets = new Dictionary<int, int>();

            try
            {
                loadedBullets = loadedData["MappedBullets"].ToObject<Dictionary<int, int>>();
            }
            catch(Exception ex)
            {
                Log($"Unable to load mapped bullets from save", BepInEx.Logging.LogLevel.Error);
                Log(ex, BepInEx.Logging.LogLevel.Error);
            }

            foreach (var pair in loadedBullets)
            {
                try
                {
                    SpawnedBulletsToGunIdMap[pair.Key] = CityData.Instance.interactableDirectory.Where(item => item.id == (int)pair.Value).First();
                }
                catch(Exception ex)
                {
                    Log($"Unable to load bullet {pair.Key} from save", BepInEx.Logging.LogLevel.Error);
                    Log(ex, BepInEx.Logging.LogLevel.Error);
                }
            }
        }

        public static object Save()
        {
            var output = new Dictionary<string, Dictionary<int, int>>();
            output["MappedBullets"] = new Dictionary<int, int>();

            foreach(var pair in SpawnedBulletsToGunIdMap)
            {
                output["MappedBullets"].Add(pair.Key, pair.Value.id);
            }

            return output;
        }
        
        static string GetGunSerialNumber(object inputObject)
        {
            Interactable gunInteractable = inputObject.TryCast<Interactable>();
            if (gunInteractable == null)
            {
                if(inputObject.TryCast<Human>() != null)
                {
                    gunInteractable = ItemGivenToHuman;
                }
            }

            if (gunInteractable == null)
            {
                Log($"Unknown object passed to GetGunSerialNumber - {inputObject.ToTypedString()}", BepInEx.Logging.LogLevel.Error);
                return "ERROR";
            }

            return IsInteractableAGun(gunInteractable) ? GetInteractableCode("WeaponSerial", gunInteractable.id) : "";
        }
        
        static string GetGunHeadStamp(object inputObject)
        {
            Interactable interactable = inputObject.TryCast<Interactable>();
            if (interactable == null)
            {
                if (inputObject.TryCast<Human>() != null)
                {
                    interactable = ItemGivenToHuman;
                }
            }

            // If we scanned a bullet, get the gun interactable
            if (SpawnedBulletsToGunIdMap.ContainsKey(interactable.id))
            {
                interactable = SpawnedBulletsToGunIdMap[interactable.id];
            }

            if (interactable == null)
            {
                Log($"Unknown object passed to GetGunHeadStamp - {inputObject.ToTypedString()}", BepInEx.Logging.LogLevel.Error);
                return "ERROR";
            }

            return IsInteractableAGun(interactable) ? GetInteractableCode("HeadStamp", interactable.id, 1) : "";
        }

        private static string GetInteractableCode(string interactableType, int interactableId, int offset = 0)
        {
            if (!interactableIdCache[interactableType].ContainsKey(interactableId))
            {
                string code = "";

                uint lettersPerValue = 6;
                for (uint i = 0; i < lettersPerValue; i++)
                {
                    code += GetRandomCharacter((uint)interactableId, i + (lettersPerValue * (uint)offset));
                }
                
                interactableIdCache[interactableType][interactableId] = code;
            }

            return interactableIdCache[interactableType][interactableId];
        }
        
        // Stolen from partial prints
        private static char GetRandomCharacter(uint interactableId, uint letterIndex)
        {
            // We're going to use XORShift here to avoid allocating a System.Random every time we need a letter. It's basically just bitwise math and prime numbers that create random looking numbers.
            const uint PRIME_1 = 2654435761;
            const uint PRIME_2 = 1629267613;
            const uint PRIME_3 = 334214467;
            const char FIRST_LETTER = 'A';

            // Hash the numbers together using bitwise stuff and XOR rather than instantiating random number generators, for speed.
            uint hash = _citySeedCache;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;

            hash += PRIME_1 * interactableId;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;

            hash += PRIME_2 * letterIndex;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;

            hash *= PRIME_3;

            // Map the hash to the A-Z range.
            return (char)(FIRST_LETTER + (hash % 26));
        }

        public static bool IsInteractableAGun(Interactable interactable)
        {
            switch (interactable.preset?.weapon?.type)
            {
                case MurderWeaponPreset.WeaponType.handgun:
                case MurderWeaponPreset.WeaponType.rifle:
                case MurderWeaponPreset.WeaponType.shotgun:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsInteractableACasing(Interactable interactable)
        {
            return interactable?.preset?.spawnEvidence?.presetName == "BulletCasing";
        }

        public static Evidence GetOrCreateEvidence(string evidenceKey, string evidencePresetName, Interactable interactable)
        {
            var weaponStampBulletEvidenceKey = evidenceKey + interactable.id;

            Evidence createdEvidence;
            if (GameplayController.Instance.evidenceDictionary.ContainsKey(weaponStampBulletEvidenceKey))
            {
                createdEvidence = GameplayController.Instance.evidenceDictionary[weaponStampBulletEvidenceKey];
            }
            else
            {
                var passedObjects = new Il2CppSystem.Collections.Generic.List<Il2CppSystem.Object>();
                passedObjects.Add(interactable);
                createdEvidence = EvidenceCreator.Instance.CreateEvidence(evidencePresetName, weaponStampBulletEvidenceKey, passedObjects: passedObjects);
                createdEvidence.AddFactLink(EvidenceCreator.Instance.CreateFact("FC_WeaponSerial", createdEvidence, interactable.evidence), Evidence.DataKey.name, true);
            }

            return createdEvidence;
        }

        private static void CreateBulletStampMatch(Evidence gun, Evidence bullet)
        {
            return;
            // TODO: Broken
            // - Only create one mapping between each bullet and gun
            // - Only show for known evidence

            Log($"Adding fact between stamps");
            // TODO: Config, matches label
            gun.AddFactLink(EvidenceCreator.Instance.CreateFact(
                "FC_WeaponSerial",
                gun,
                bullet
            ), Evidence.DataKey.name, false);
        }

        static void Log(object data, BepInEx.Logging.LogLevel logLevel = BepInEx.Logging.LogLevel.Info)
        {
            if (AdditionalEvidencePlugin.GunForensics_DebugLogging.Value || logLevel <= BepInEx.Logging.LogLevel.Error)
                AdditionalEvidencePlugin.PluginLogger.Log(logLevel, data);
        }

        [HarmonyPatch(typeof(CityData), "CreateSingletons")]
        internal class CityData_CreateSingletons
        {
            // Make sure the DDS system can show the new evidence
            public static void Postfix()
            {
                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["GetGunSerialNumber"] = (System.Func<Interactable, string>)GetGunSerialNumber;
                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["GetGunHeadStamp"] = (System.Func<Interactable, string>)GetGunHeadStamp;
            }
        }

        [HarmonyPatch(typeof(BioScreenController), nameof(BioScreenController.OnScanComplete))]
        public class BioScreenController_OnScanComplete
        {
            // When an gun or bullet is scanned in inventory, create and show the evidence
            public static void Prefix(BioScreenController __instance, Interactable scanCompleteOn)
            {
                if (IsInteractableAGun(scanCompleteOn))
                {
                    Log($"Scan complete. {scanCompleteOn.preset.name} is a {System.Enum.GetName<MurderWeaponPreset.WeaponType>(scanCompleteOn.preset.weapon.type)}");

                    var gunSerialEvidence = GetOrCreateEvidence("EP_GunSerialNumber|", "EP_GunSerialNumber", scanCompleteOn);
                    var gunStampEvidence = GetOrCreateEvidence("EP_GunHeadStamp|", "EP_GunHeadStamp", scanCompleteOn);

                    // This is horrific for performance, but it only runs after a gun is scanned, so it's rare
                    /*
                    foreach(var pair in SpawnedBulletsToGunIdMap)
                    {
                        if (pair.Value.id == scanCompleteOn.id)
                        { 
                            var gunBulletStampEvidence = GetOrCreateEvidence(
                                "EP_GunBulletHeadStamp|",
                                "EP_GunHeadStamp",
                                CityData.Instance.interactableDirectory.Where(item => item.id == (int)pair.Key).First()
                            );
                            CreateBulletStampMatch(gunStampEvidence, gunBulletStampEvidence);
                        }
                    }
                    */

                    InterfaceController.Instance.SpawnWindow(gunSerialEvidence, passedInteractable: scanCompleteOn);

                    if (AdditionalEvidencePlugin.GunForensics_ScanGunForHeadPrint.Value)
                        InterfaceController.Instance.SpawnWindow(gunStampEvidence, passedInteractable: scanCompleteOn);
                }
                // Check if it was a bullet that was scanned
                else if(IsInteractableACasing(scanCompleteOn))
                {
                    if(SpawnedBulletsToGunIdMap.ContainsKey(scanCompleteOn.id))
                    {
                        Log($"Scan complete. {scanCompleteOn.preset.name} is a bullet casing and is mapped to {SpawnedBulletsToGunIdMap[scanCompleteOn.id].id}");

                        var gunStampEvidence = GetOrCreateEvidence("EP_GunHeadStamp|", "EP_GunHeadStamp", SpawnedBulletsToGunIdMap[scanCompleteOn.id]);
                        var gunBulletStampEvidence = GetOrCreateEvidence("EP_GunBulletHeadStamp|", "EP_GunHeadStamp", scanCompleteOn);

                        // CreateBulletStampMatch(gunStampEvidence, gunBulletStampEvidence);

                        InterfaceController.Instance.SpawnWindow(gunBulletStampEvidence, passedInteractable: scanCompleteOn);
                    }
                    else
                    {
                        Log($"Unmapped bullet casing {scanCompleteOn.id}!", BepInEx.Logging.LogLevel.Error);
                    }
                }
            }
        }
        
        [HarmonyPatch(typeof(InteractableCreator), nameof(InteractableCreator.CreateWorldInteractable))]
        public class InteractableCreator_CreateWorldInteractable
        {
            // Track spawned shell casings, associate them with the gun they were fired from
            // Doesn't seem to be any vanilla link between gun and bullet, so we look for the created item and the weapon the NPC is holding at the time
            public static void Postfix(Interactable __result, InteractablePreset preset, Human belongsTo)
            {
                if(
                    __result != null &&
                    IsInteractableACasing(__result) &&
                    // Ensure the firing AI is holding a weapon
                    belongsTo?.ai?.currentWeapon != null
                )
                {
                    // Make sure the current weapon is actually a gun
                    if(IsInteractableAGun(belongsTo.ai.currentWeapon))
                    {
                        SpawnedBulletsToGunIdMap[__result.id] = belongsTo.ai.currentWeapon;
                        Log($"Weapon fired. Bullet {__result.id} mapped to gun {belongsTo.ai.currentWeapon.id}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Interactable), nameof(Interactable.Delete))]
        public class Interactable_Delete
        {
            // When shell casings are removed from the world, remove them from our save
            public static void Postfix(Interactable __instance)
            {
                if (SpawnedBulletsToGunIdMap.ContainsKey(__instance.id))
                {
                    Log($"Removing bullet {__instance.id} from the spawnedBulletsMap", BepInEx.Logging.LogLevel.Warning);
                    SpawnedBulletsToGunIdMap.Remove(__instance.id);
                }
            }
        }
    }
}
