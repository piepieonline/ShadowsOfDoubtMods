using System.IO;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Cpp2IL.Core.Extensions;

using static Descriptors;
using System.Linq;
using UniverseLib;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

/*
    * Not using prefix shortcircuits even when possible, to try and be more survivable across updates
*/

namespace CitizenImporter
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class CitizenImporterPlugin : BaseUnityPlugin
#elif IL2CPP
    public class CitizenImporterPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        // People from file loading
        class HumanOverride
        {
            public int id;
            public static int idCounter;
            public static Dictionary<int, HumanOverride> createdHumans;
            public int gameId = -1;

            public string firstName;
            public string lastName;
            public string casualName;

            public string dayOfBirth;
            public string monthOfBirth;
            public string age;

            public string gender;
            public string attractedTo;

            public string ethnicity1;
            public string ethnicity2;

            public string height;
            public string buildType;
            public string hairColour;
            public string hairColourExact = "";
            public string hairType;
            public string eyeColour;

            public string glasses;
            public string beard;
            public string moustache;

            public string partner;

            public HumanOverride()
            {
                id = ++idCounter;
                createdHumans[id] = this;
            }

            // Human line numbers, excluding the heading row
            public static void ResetIdCounter()
            {
                idCounter = 1;
                createdHumans = new Dictionary<int, HumanOverride>();
            }
        }

        static Dictionary<Human.Gender, List<HumanOverride>> loadedHumanOverrides = new Dictionary<Human.Gender, List<HumanOverride>>()
        {
            { Human.Gender.male, new List<HumanOverride>() },
            { Human.Gender.female, new List<HumanOverride>() },
            { Human.Gender.nonBinary, new List<HumanOverride>() }
        };
        static Dictionary<int, (Human, HumanOverride)> overridenHumans = new Dictionary<int, (Human, HumanOverride)>();

        public static float seriousRelationshipsRatioCache;

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }

        static bool ReloadCitizensFromFile()
        {
            seriousRelationshipsRatioCache = SocialStatistics.Instance.seriousRelationshipsRatio;

            var path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "citizens.csv");
            if (!File.Exists(path))
            {
                PluginLogger.LogError("Citizen file not found, no citizen overrides will be loaded. See https://docs.google.com/spreadsheets/d/11OvB572ezm2j-iO2rd89XHInrTNXey83eLdw2VJcujE/edit#gid=1379962932");
                return false;
            }

            string[] citizensCSV = File.ReadAllLines(path);

            loadedHumanOverrides[Human.Gender.male].Clear();
            loadedHumanOverrides[Human.Gender.female].Clear();
            loadedHumanOverrides[Human.Gender.nonBinary].Clear();

            HumanOverride.ResetIdCounter();

            // Header
            for (int i = 1; i < citizensCSV.Length; i++)
            {
                string[] vals = citizensCSV[i].Split(",");
                var newHumanOverride = new HumanOverride()
                {
                    firstName = vals.ElementAtOrDefault(0),
                    lastName = vals.ElementAtOrDefault(1),
                    casualName = vals.ElementAtOrDefault(2),
                    dayOfBirth = vals.ElementAtOrDefault(3),
                    monthOfBirth = vals.ElementAtOrDefault(4),
                    age = vals.ElementAtOrDefault(5),
                    gender = vals.ElementAtOrDefault(6),
                    attractedTo = vals.ElementAtOrDefault(7),
                    ethnicity1 = vals.ElementAtOrDefault(8),
                    ethnicity2 = vals.ElementAtOrDefault(9),
                    height = vals.ElementAtOrDefault(10),
                    buildType = vals.ElementAtOrDefault(11),
                    hairColour = vals.ElementAtOrDefault(12),
                    hairType = vals.ElementAtOrDefault(13),
                    eyeColour = vals.ElementAtOrDefault(14),
                    glasses = vals.ElementAtOrDefault(15),
                    beard = vals.ElementAtOrDefault(16),
                    moustache = vals.ElementAtOrDefault(17),
                    partner = vals.ElementAtOrDefault(18)
                };

                // Don't use non-binary here, because it's just too rare - citizens likely won't generate 
                Human.Gender genderToAssign = (Human.Gender)Random.Range(0, 2);
                if (newHumanOverride.gender.Length != 0)
                {
                    if (!System.Enum.TryParse<Human.Gender>(newHumanOverride.gender, true, out genderToAssign))
                    {
                        LogParseFailure(null, newHumanOverride, "gender");
                    }
                }

                loadedHumanOverrides[genderToAssign].Add(newHumanOverride);
            }

            // Probably don't need to shuffle the order, the game seems to create citizens in a fairly random order?

            if (loadedHumanOverrides.Aggregate(0, (total, tuple) => total + tuple.Value.Count) == 0)
            {
                PluginLogger.LogError("Citizen file found, but no citizen overrides loaded. No overrides will be applied.");
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(CitizenCreator), "Populate")]
        public class CitizenCreator_Populate
        {
            public static void Prefix()
            {
                PluginLogger.LogInfo("Reloading citizen overrides");
                Creator_SetComplete.citizensLoadedFromFile = ReloadCitizensFromFile();
                Creator_SetComplete.unusedPartnersCount = 0;
            }
        }

        [HarmonyPatch(typeof(Creator), "SetComplete")]
        public class Creator_SetComplete
        {
            public static bool citizensLoadedFromFile = false;
            public static int unusedPartnersCount = 0;

            public static int directSpawnedCitCount = 0;
            public static int partnerSpawnedCitCount = 0;

            public static void Postfix(Creator __instance)
            {
                if (citizensLoadedFromFile && __instance.GetActualType() == typeof(CitizenCreator))
                {
                    PluginLogger.LogWarning($"Citizen overrides applied. {loadedHumanOverrides.Aggregate(0, (total, tuple) => total + tuple.Value.Count) + unusedPartnersCount} overrides were not used ({Creator_SetComplete.unusedPartnersCount} were unused partners)");
                    /*
                    foreach (var keyValue in overridenHumans)
                    {
                        var h = keyValue.Value.Item1;
                        PluginLogger.LogInfo($"Citizen override: {h.citizenName} ({h.gender}, attracted to {h.attractedTo.ToArray().Join()}). {(h.partner != null ? ("Partner: " + h.partner.citizenName) : "")}");
                    }

                    PluginLogger.LogWarning($"Citizens not attracted to partners:");

                    foreach(var cit in CityData.Instance.citizenDirectory)
                    {
                        if (cit.partner != null)
                        {
                            if (!cit.attractedTo.Contains(cit.partner.gender))
                            {
                                PluginLogger.LogInfo($"{cit.citizenName} ({cit.gender}, attracted to {cit.attractedTo.ToArray().Join()}) is not attracted to partner {cit.partner.citizenName} ({cit.partner.gender})");
                            }
                        }
                    }
                    */

                    foreach (var cit in CityData.Instance.citizenDirectory)
                    {
                        if (cit.partner != null)
                        {
                            partnerSpawnedCitCount++;
                        }
                        else if(cit.home != null)
                        {
                            directSpawnedCitCount++;
                        }
                    }

                    PluginLogger.LogInfo($"{partnerSpawnedCitCount} have partners, {directSpawnedCitCount} do not");
                }
            }
        }

        static void LogParseFailure(Human? human, HumanOverride humanOverride, string field)
        {
            PluginLogger.LogWarning($"Human {human?.humanID} (Override name: {humanOverride.firstName} {humanOverride.lastName}) failed to parse {field}");
        }

        // Partner is set after this, random dice roll.. How can we force them to spawn if requested?
        [HarmonyPatch(typeof(Human), "SetSexualityAndGender")]
        public class Human_SetSexualityAndGender
        {
            public static void Postfix(ref Human __instance)
            {
                if (SessionData.Instance.isFloorEdit || CityConstructor.Instance.generateNew)
                {
                    if (loadedHumanOverrides.Count > 0)
                    {
                        var nextSuitableHuman = 0;
                        for (; nextSuitableHuman < loadedHumanOverrides[__instance.gender].Count; nextSuitableHuman++)
                        {
                            var testingHuman = loadedHumanOverrides[__instance.gender][nextSuitableHuman];

                            // Make sure that 'partner' relationships are one-way, to prevent loops
                            if (int.TryParse(testingHuman.partner, out int partnerId))
                            {
                                if (HumanOverride.createdHumans[partnerId].gameId != -1)
                                {
                                    // If we already created this person's partner, they can't spawn
                                    loadedHumanOverrides[__instance.gender].RemoveAt(nextSuitableHuman);
                                    Creator_SetComplete.unusedPartnersCount++;
                                    continue;
                                }
                            }

                            if(HumanOverride.createdHumans.ContainsKey(partnerId) && System.Enum.TryParse(HumanOverride.createdHumans[partnerId].gender, true, out Human.Gender partnerGender))
                            {
                                // Our partner has a fixed gender
                                if (__instance.attractedTo.Contains(partnerGender))
                                {
                                    __instance.attractedTo.Clear();
                                    __instance.attractedTo.Add(partnerGender);
                                    break;
                                }
                            }
                            else if (System.Enum.TryParse(testingHuman.attractedTo, true, out Human.Gender attractedTo))
                            {
                                // We have an assigned attracted to, make sure it matches
                                if (__instance.attractedTo.Contains(attractedTo))
                                {
                                    __instance.attractedTo.Clear();
                                    __instance.attractedTo.Add(attractedTo);
                                    break;
                                }
                            }
                            else
                            {
                                // If we don't have an assigned attracted to value, we can use this person regardless
                                break;
                            }
                        }

                        if (nextSuitableHuman < loadedHumanOverrides[__instance.gender].Count)
                        {
                            overridenHumans[__instance.humanID] = (__instance, loadedHumanOverrides[__instance.gender].RemoveAndReturn(nextSuitableHuman));
                            overridenHumans[__instance.humanID].Item2.gameId = __instance.humanID;
                            // Nonbinary, non specified, will cause us issues later, so remove it
                            if (__instance.attractedTo.Count > 1)
                                __instance.attractedTo.Remove(Human.Gender.nonBinary);

                            if (overridenHumans[__instance.humanID].Item2.partner != "")
                            {
                                SocialStatistics.Instance.seriousRelationshipsRatio = 1;
                            }
                            else if (overridenHumans[__instance.humanID].Item2.attractedTo == "none")
                            {
                                SocialStatistics.Instance.seriousRelationshipsRatio = 0;
                            }

                            PluginLogger.LogInfo($"Setting override: {overridenHumans[__instance.humanID].Item2.firstName} {overridenHumans[__instance.humanID].Item2.lastName} ({overridenHumans[__instance.humanID].Item2.id}) will replace {__instance.humanID}");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Human), "GenerateSuitableGenderAndSexualityForParnter")]
        public class Human_GenerateSuitableGenderAndSexualityForParnter
        {
            public static void Postfix(Human __instance, Citizen newPartner)
            {
                SocialStatistics.Instance.seriousRelationshipsRatio = seriousRelationshipsRatioCache;
                if (SessionData.Instance.isFloorEdit || CityConstructor.Instance.generateNew)
                {
                    if (overridenHumans.ContainsKey(newPartner.humanID) && int.TryParse(overridenHumans[newPartner.humanID].Item2.partner, out var partnerHumanId))
                    {
                        int partnerId = -1;
                        Human.Gender foundAttractedTo = __instance.gender;
                        foreach (var gender in new Human.Gender[] { Human.Gender.male, Human.Gender.female, Human.Gender.nonBinary })
                        {
                            partnerId = loadedHumanOverrides[gender].FindIndex(ho => ho.id == partnerHumanId);
                            foundAttractedTo = gender;
                            if (partnerId > -1) break;
                        }

                        if (partnerId >= 0)
                        {
                            var partnerOverride = loadedHumanOverrides[foundAttractedTo].RemoveAndReturn(partnerId);
                            if (partnerOverride.gender == "" || (System.Enum.TryParse(partnerOverride.gender, true, out Human.Gender partnerGender) && newPartner.attractedTo.Contains(partnerGender)))
                            {
                                overridenHumans[__instance.humanID] = (__instance, partnerOverride);
                                overridenHumans[__instance.humanID].Item2.gameId = __instance.humanID;
                                PluginLogger.LogInfo($"Setting override: {overridenHumans[__instance.humanID].Item2.firstName} {overridenHumans[__instance.humanID].Item2.lastName} will replace {__instance.humanID} as a partner of {newPartner.humanID}");
                            }
                            else
                            {
                                PluginLogger.LogInfo($"Invalid partner for {partnerHumanId} (Gender)");// , needed {} found {})");
                            }
                        }
                        else
                        {
                            PluginLogger.LogInfo($"Invalid partner for {partnerHumanId}");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Human), "CalculateAge")]
        public class Human_CalculateAge
        {
            public static void Postfix(Human __instance)
            {
                if (overridenHumans.ContainsKey(__instance.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.humanID].Item2;
                    if (
                        int.TryParse(overrideHuman.dayOfBirth, out int dayOfBirth) && dayOfBirth > 0 && dayOfBirth <= 31 &&
                        int.TryParse(overrideHuman.monthOfBirth, out int monthOfBirth) && monthOfBirth > 0 && monthOfBirth <= 12 &&
                        int.TryParse(overrideHuman.age, out int age)
                    )
                    {
                        int yearOfBirth = (SessionData.Instance.publicYear - age) + 1;
                        if (monthOfBirth > SessionData.Instance.monthInt)
                        {
                            yearOfBirth--;
                        }
                        else if (monthOfBirth == SessionData.Instance.monthInt)
                        {
                            if (dayOfBirth > SessionData.Instance.dayInt)
                            {
                                yearOfBirth--;
                            }
                        }

                        __instance.birthday = monthOfBirth.ToString() + "/" + dayOfBirth.ToString() + "/" + yearOfBirth.ToString();
                    }
                }
            }
        }

        // We can't easily set height, so intecept the random roll. Capture and replace the seed when set by GenerateBuild (the previous call in the function)
        [HarmonyPatch(typeof(Toolbox), "RandomRangeWeightedSeedContained")]
        public class Toolbox_RandomRangeWeightedSeedContained
        {
            public static void Postfix(ref string inputSeed, ref float __result)
            {
                if (inputSeed.StartsWith("piecitizenoverride"))
                {
                    var vals = inputSeed.Split('_');
                    inputSeed = vals[2];
                    __result = float.Parse(vals[1]);
                }
            }
        }

        // If ethnicity is set only once, set it twice (otherwise the game might roll a random secondary)
        [HarmonyPatch(typeof(Descriptors), "GenerateEthnicity")]
        public class Descriptors_GenerateEthnicity
        {
            static Dictionary<EthnicGroup, SocialStatistics.EthnicityStats> ethStats;

            public static void Prefix(Descriptors __instance)
            {
                if (ethStats == null)
                {
                    ethStats = new Dictionary<EthnicGroup, SocialStatistics.EthnicityStats>();
                    foreach (var ethStat in SocialStatistics.Instance.ethnicityStats)
                    {
                        ethStats.Add(ethStat.group, ethStat);
                    }
                }

                if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;
                    if (overrideHuman.ethnicity1 != null && overrideHuman.ethnicity1 != "")
                    {
                        if (overrideHuman.ethnicity2 == null || overrideHuman.ethnicity2 == "") overrideHuman.ethnicity2 = overrideHuman.ethnicity1;

                        if (!System.Enum.TryParse<EthnicGroup>(overrideHuman.ethnicity1, true, out var eth1))
                        {
                            LogParseFailure(__instance.citizen, overrideHuman, "ethnicity1");
                            return;
                        }
                        if (!System.Enum.TryParse<EthnicGroup>(overrideHuman.ethnicity2, true, out var eth2))
                        {
                            LogParseFailure(__instance.citizen, overrideHuman, "ethnicity2");
                            return;
                        }

                        __instance.ethnicities.Add(new EthnicitySetting()
                        {
                            group = eth1,
                            ratio = 1f,
                            stats = ethStats[eth1]
                        });

                        __instance.ethnicities.Add(new EthnicitySetting()
                        {
                            group = eth2,
                            ratio = 1f,
                            stats = ethStats[eth2]
                        });
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Descriptors), "GenerateEyes")]
        public class Descriptors_GenerateEyes
        {
            public static void Postfix(Descriptors __instance)
            {
                if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;
                    if (overrideHuman.eyeColour != null && overrideHuman.eyeColour != "")
                    {
                        if (System.Enum.TryParse<EyeColour>(overrideHuman.eyeColour, true, out var setEyeColour))
                        {
                            __instance.eyeColour = setEyeColour;
                        }
                        else
                        {
                            LogParseFailure(__instance.citizen, overrideHuman, "eyeColour");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Descriptors), "GenerateNameAndSkinColour")]
        public class Descriptors_GenerateNameAndSkinColour
        {
            public static void Postfix(Descriptors __instance)
            {
                if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;
                    __instance.citizen.firstName = overrideHuman.firstName.Length > 0 ? overrideHuman.firstName : __instance.citizen.firstName;
                    __instance.citizen.surName = overrideHuman.lastName.Length > 0 ? overrideHuman.lastName : __instance.citizen.surName;
                    __instance.citizen.casualName = overrideHuman.casualName.Length > 0 ? overrideHuman.casualName : __instance.citizen.firstName;
                    __instance.citizen.citizenName = __instance.citizen.firstName + " " + __instance.citizen.surName;
                    __instance.citizen.gameObject.name = __instance.citizen.citizenName;
                    __instance.citizen.name = __instance.citizen.citizenName;
                }
            }
        }

        [HarmonyPatch(typeof(Descriptors), "GenerateHair")]
        public class Descriptors_GenerateHair
        {
            public static void Postfix(Descriptors __instance)
            {
                if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;
                    if (overrideHuman.hairColour != "")
                    {
                        if (System.Enum.TryParse<HairColour>(overrideHuman.hairColour, true, out var setHairColour))
                        {
                            __instance.hairColourCategory = setHairColour;

                            if (overrideHuman.hairColourExact != "")
                            {
                                if (ColorUtility.TryParseHtmlString(overrideHuman.hairColourExact, out var setExactHairColour))
                                {
                                    __instance.hairColour = setExactHairColour;
                                }
                                else
                                {
                                    LogParseFailure(__instance.citizen, overrideHuman, "exactHairColour");
                                }
                            }
                            else
                            {
                                foreach (var hairColourSetting in SocialStatistics.Instance.hairColourSettings)
                                {
                                    if (hairColourSetting.colour == __instance.hairColourCategory)
                                    {
                                        var seed = __instance.citizen.seed;
                                        __instance.hairColour = Color.Lerp(hairColourSetting.hairColourRange1, hairColourSetting.hairColourRange2, Toolbox.Instance.GetPsuedoRandomNumberContained(0.0f, 1f, ref seed));
                                        __instance.citizen.seed = seed;
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            LogParseFailure(__instance.citizen, overrideHuman, "hairColour");
                        }
                    }

                    if (overrideHuman.hairType != "")
                    {
                        if (System.Enum.TryParse<HairStyle>(overrideHuman.hairType, true, out var setHairStyle))
                        {
                            __instance.hairType = setHairStyle;
                        }
                        else
                        {
                            LogParseFailure(__instance.citizen, overrideHuman, "hairType");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Descriptors), "GenerateBuild")]
        public class Descriptors_GenerateBuild
        {
            public static void Postfix(Descriptors __instance)
            {
                if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;
                    if (overrideHuman.buildType != "")
                    {
                        if (System.Enum.TryParse<BuildType>(overrideHuman.buildType, true, out var setValue))
                        {
                            __instance.build = setValue;
                        }
                        else
                        {
                            LogParseFailure(__instance.citizen, overrideHuman, "buildType");
                        }
                    }

                    // Setup the height override if set
                    if (overrideHuman.height != "" && float.TryParse(overrideHuman.height, out var height))
                    {
                        __instance.citizen.seed = $"piecitizenoverride_{height}_{__instance.citizen.seed}";
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Descriptors), "GenerateFacialFeatures")]
        public class Descriptors_GenerateFacialFeatures
        {
            enum OverrideFeature
            {
                None,
                On,
                Off
            }

            public static void Postfix(Descriptors __instance)
            {
                // TODO: Disabled, not working
                return;

                if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;

                    var glasses = OverrideFeature.None;
                    var beard = OverrideFeature.None;
                    var moustache = OverrideFeature.None;

                    if (overrideHuman.glasses != null && overrideHuman.glasses != "")
                    {
                        if (overrideHuman.glasses == "true") glasses = OverrideFeature.On;
                        else if (overrideHuman.glasses == "false") glasses = OverrideFeature.Off;
                    }

                    if (overrideHuman.beard != null && overrideHuman.beard != "")
                    {
                        if (overrideHuman.beard == "true") beard = OverrideFeature.On;
                        else if (overrideHuman.beard == "false") beard = OverrideFeature.Off;
                    }

                    if (overrideHuman.moustache != null && overrideHuman.moustache != "")
                    {
                        if (overrideHuman.moustache == "true") moustache = OverrideFeature.On;
                        else if (overrideHuman.moustache == "false") moustache = OverrideFeature.Off;
                    }

                    for (int i = __instance.facialFeatures.Count - 1; i >= 0; i--)
                    {
                        if (__instance.facialFeatures[i].feature == FacialFeature.glasses)
                        {
                            if (glasses == OverrideFeature.On) glasses = OverrideFeature.None;
                            else if (glasses == OverrideFeature.Off) __instance.facialFeatures.RemoveAt(i);
                        }
                        else if (__instance.facialFeatures[i].feature == FacialFeature.beard)
                        {
                            if (beard == OverrideFeature.On) beard = OverrideFeature.None;
                            else if (beard == OverrideFeature.Off) __instance.facialFeatures.RemoveAt(i);
                        }
                        else if (__instance.facialFeatures[i].feature == FacialFeature.moustache)
                        {
                            if (moustache == OverrideFeature.On) moustache = OverrideFeature.None;
                            else if (moustache == OverrideFeature.Off) __instance.facialFeatures.RemoveAt(i);
                        }
                    }

                    if (glasses == OverrideFeature.On) __instance.facialFeatures.Add(new FacialFeaturesSetting() { feature = FacialFeature.glasses, id = 0 });
                    if (beard == OverrideFeature.On) __instance.facialFeatures.Add(new FacialFeaturesSetting() { feature = FacialFeature.beard, id = 0 });
                    if (moustache == OverrideFeature.On) __instance.facialFeatures.Add(new FacialFeaturesSetting() { feature = FacialFeature.moustache, id = 0 });
                }
            }
        }
    }
}