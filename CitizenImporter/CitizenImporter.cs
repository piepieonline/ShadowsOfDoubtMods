using System.IO;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Cpp2IL.Core.Extensions;

using UniverseLib;

using static Descriptors;
using SOD.Common.Extensions;
using System.Text.RegularExpressions;
using Cpp2IL.Core.Api;




#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

/*
    * Not using prefix shortcircuits even when possible, to try and be more survivable across updates
    * 
    * TODO:
        - Ages wrong
        - Test glasses again
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
        public static List<MatchCollection> testMatches = new List<MatchCollection>();

        public static ManualLogSource PluginLogger;

        public static int failedToLoadCount = 0;

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

            public string[] traits;
            public string[] notTraits;

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

        static HumanOverride playerOverride = null;

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
            failedToLoadCount = 0;

            // Header
            for (int i = 1; i < citizensCSV.Length; i++)
            {
                var regexMatches = Regex.Matches(citizensCSV[i], """(?:^|,)(?=[^"]|(")?)"?((?(1)(?:[^"]|"")*|[^,"]*))"?(?=,|$)""");
                testMatches.Add(regexMatches);

                HumanOverride newHumanOverride;

                int expectedColCount = 18;
                if(regexMatches.Count != expectedColCount)
                {
                    PluginLogger.LogError($"Failed to read line {i + 1} from citizens.csv. Expected {expectedColCount} columns, found {regexMatches.Count}");
                    failedToLoadCount++;
                    continue;
                }

                try
                {
                    newHumanOverride = new HumanOverride()
                    {
                        firstName = regexMatches[0].Groups[2].Value,
                        lastName = regexMatches[1].Groups[2].Value,
                        casualName = regexMatches[2].Groups[2].Value,
                        dayOfBirth = regexMatches[3].Groups[2].Value,
                        monthOfBirth = regexMatches[4].Groups[2].Value,
                        age = regexMatches[5].Groups[2].Value,
                        gender = regexMatches[6].Groups[2].Value,
                        attractedTo = regexMatches[7].Groups[2].Value,
                        ethnicity1 = regexMatches[8].Groups[2].Value,
                        ethnicity2 = regexMatches[9].Groups[2].Value,
                        height = regexMatches[10].Groups[2].Value,
                        buildType = regexMatches[11].Groups[2].Value,
                        hairColour = regexMatches[12].Groups[2].Value,
                        hairType = regexMatches[13].Groups[2].Value,
                        eyeColour = regexMatches[14].Groups[2].Value,
                        traits = regexMatches[15].Groups[2].Value.Split(",").Select(trait => trait.Trim().ToLowerInvariant()).Where(trait => trait != "").ToArray(),
                        notTraits = regexMatches[16].Groups[2].Value.Split(",").Select(trait => trait.Trim().ToLowerInvariant()).Where(trait => trait != "").ToArray(),
                        partner = regexMatches[17].Groups[2].Value
                    };
                }
                catch (System.ArgumentOutOfRangeException ex)
                {
                    PluginLogger.LogError($"Failed to read line {i + 1} from citizens.csv");
                    failedToLoadCount++;
                    continue;
                }


                // Mutually exclusive traits
                if (newHumanOverride.traits.Contains("affliction-farsighted") && !newHumanOverride.notTraits.Contains("affliction-shortsighted")) newHumanOverride.notTraits.AddItem("affliction-shortsighted");
                if (newHumanOverride.traits.Contains("affliction-shortsighted") && !newHumanOverride.notTraits.Contains("affliction-farsighted")) newHumanOverride.notTraits.AddItem("affliction-farsighted");
                
                // Don't use non-binary here, because it's just too rare - citizens likely won't generate 
                Human.Gender genderToAssign = (Human.Gender)Random.Range(0, 2);
                if (newHumanOverride.gender.Length != 0)
                {
                    if (!System.Enum.TryParse<Human.Gender>(newHumanOverride.gender, true, out genderToAssign))
                    {
                        LogParseFailure(null, newHumanOverride, "gender");
                    }
                }

                if (
                    newHumanOverride.firstName.Trim().ToUpper() == "PLAYER" &&
                    newHumanOverride.lastName.Trim().ToUpper() == "PLAYER"
                )
                {
                    playerOverride = newHumanOverride;
                }
                else
                {
                    loadedHumanOverrides[genderToAssign].Add(newHumanOverride);
                }
            }

            // Probably don't need to shuffle the order, the game seems to create citizens in a fairly random order?

            if (loadedHumanOverrides.Aggregate(0, (total, tuple) => total + tuple.Value.Count) == 0)
            {
                PluginLogger.LogError("Citizen file found, but no citizen overrides loaded. No overrides will be applied.");
                return false;
            }
            return true;
        }

        public static void DebugPrintHuman(Human human)
        {
            PluginLogger.LogInfo($"");

            PluginLogger.LogInfo($"Name: {human.name}");

            foreach (var t in human.characterTraits)
                PluginLogger.LogInfo($"\tTrait: {t.name}");

            PluginLogger.LogInfo($"");

            foreach(var e in human.descriptors.ethnicities)
                PluginLogger.LogInfo($"\tEth: {e.group}");
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

                    if(failedToLoadCount > 0)
                    {
                        PluginLogger.LogError($"{failedToLoadCount} citizens failed to load. Scroll up to see any errors.");
                    }

                    TestOverriddenHumans();

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

        static void TestOverriddenHumans()
        {
            PluginLogger.LogInfo("Checking overrides for inconsistencies...");

            foreach (var keyValue in overridenHumans)
            {
                var human = CityData.Instance.citizenDictionary[keyValue.Key];
                var humanOverride = keyValue.Value.Item2;
                
                if(humanOverride.firstName != "" && human.firstName != humanOverride.firstName) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'firstName'");
                if(humanOverride.lastName != "" && human.surName != humanOverride.lastName) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'surName'");
                if(humanOverride.casualName != "" && human.casualName != humanOverride.casualName) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'casualName'");

                if(humanOverride.dayOfBirth != "" && human.birthday.Split("/")[0] != humanOverride.dayOfBirth) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'dayOfBirth'");
                if(humanOverride.monthOfBirth != "" && human.birthday.Split("/")[1] != humanOverride.monthOfBirth) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'monthOfBirth'");
                if(humanOverride.age != "" && human.GetAge().ToString() != humanOverride.age) PluginLogger.LogInfo($"Citizen override: {human.citizenName} doesn't match 'age'");

                if (humanOverride.gender != "" && human.gender.ToString().ToLower() != humanOverride.gender) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'gender'");
                if (human.partner != null && humanOverride.attractedTo != "" && (!humanOverride.attractedTo.Contains(human.partner.gender.ToString().ToLower()))) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'attractedTo'");
                
                if (humanOverride.height != "" && human.descriptors.heightCM.ToString() != humanOverride.height) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'height'");
                if (humanOverride.buildType != "" && human.descriptors.build.ToString() != humanOverride.buildType) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'buildType'");
                if (humanOverride.hairColour != "" && human.descriptors.hairColourCategory.ToString() != humanOverride.hairColour) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'hairColour' ({human.descriptors.hairColourCategory.ToString()})");
                if (humanOverride.hairType != "" && human.descriptors.hairType.ToString() != humanOverride.hairType) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'hairType'");
                if (humanOverride.eyeColour != "" && human.descriptors.eyeColour.ToString() != humanOverride.eyeColour) PluginLogger.LogWarning($"Citizen override: {human.citizenName} doesn't match 'eyeColour'");

                foreach(var overrideTrait in humanOverride.traits)
                {
                    if (human.characterTraits.Where(trait => trait.trait.presetName.ToLowerInvariant() == overrideTrait).Count() != 1) PluginLogger.LogWarning($"Citizen override: {human.citizenName} missing required trait {overrideTrait}");
                }

                foreach (var overrideNotTrait in humanOverride.notTraits)
                {
                    if (human.characterTraits.Where(trait => trait.trait.presetName.ToLowerInvariant() == overrideNotTrait).Count() != 0) PluginLogger.LogWarning($"Citizen override: {human.citizenName} has banned trait {overrideNotTrait}");
                }
            }
        }

        // Player overrides
        [HarmonyPatch(typeof(Player), nameof(Player.PrepForStart))]
        public class Player_PrepForStart
        {
            public static void Prefix(Player __instance)
            {
                if(playerOverride != null)
                {
                    overridenHumans[__instance.humanID] = (__instance, playerOverride);
                    PluginLogger.LogInfo($"Player appearence set");
                }
            }
        }

        // Partner is set after this, random dice roll.. How can we force them to spawn if requested?
        [HarmonyPatch(typeof(Human), "SetSexualityAndGender")]
        public class Human_SetSexualityAndGender
        {
            public static void Postfix(ref Human __instance)
            {
                // if (SessionData.Instance.isFloorEdit || CityConstructor.Instance.generateNew)
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

                            // PluginLogger.LogWarning($"Citizen override: {testingHuman.firstName} {testingHuman.lastName} is ");
                            if (HumanOverride.createdHumans.ContainsKey(partnerId) && System.Enum.TryParse(HumanOverride.createdHumans[partnerId].gender, true, out Human.Gender partnerGender))
                            {
                                // Our partner has a fixed gender and already exists
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
            public static bool Prefix(Human __instance, Citizen newPartner)
            {
                SocialStatistics.Instance.seriousRelationshipsRatio = seriousRelationshipsRatioCache;

                if (overridenHumans.ContainsKey(newPartner.humanID) && int.TryParse(overridenHumans[newPartner.humanID].Item2.partner, out var partnerHumanId))
                {
                    if (SessionData.Instance.isFloorEdit || CityConstructor.Instance.generateNew)
                    {
                        __instance.humanID = Human.assignID;
                        ++Human.assignID;
                        __instance.seed = Toolbox.Instance.SeedRand(0, 999999999).ToString();
                    }

                    if (overridenHumans[newPartner.humanID].Item2.gender != overridenHumans[newPartner.humanID].Item2.attractedTo)
                    {
                        __instance.sexuality = 1;
                        __instance.homosexuality = 0;
                    }
                    else
                    {
                        __instance.sexuality = 0;
                        __instance.homosexuality = 1;
                    }

                    if(__instance.homosexuality > 0.5f)
                    {
                        __instance.gender = newPartner.gender;
                        __instance.genderScale = newPartner.genderScale;
                    }
                    else
                    {
                        if (newPartner.gender == Human.Gender.male)
                        {
                            __instance.gender = Human.Gender.female;
                            __instance.genderScale = 0;
                        }
                        else
                        {
                            __instance.gender = Human.Gender.male;
                            __instance.genderScale = 1;
                        }
                    }
                    
                    switch(__instance.gender)
                    {
                        case Human.Gender.male:
                            __instance.AddCharacterTrait(SocialStatistics.Instance.maleTrait);
                            break;
                        case Human.Gender.female:
                            __instance.AddCharacterTrait(SocialStatistics.Instance.femaleTrait);
                            break;
                        case Human.Gender.nonBinary:
                            __instance.AddCharacterTrait(SocialStatistics.Instance.nbTrait);
                            break;
                    }

                    __instance.SetBirthGender();

                    int partnerId = -1;
                    Human.Gender foundAttractedTo = __instance.gender;
                    foreach (var gender in new Human.Gender[] { Human.Gender.male, Human.Gender.female, Human.Gender.nonBinary })
                    {
                        partnerId = loadedHumanOverrides[gender].FindIndex(ho => ho.id == partnerHumanId);
                        foundAttractedTo = gender;
                        if (partnerId > -1) break;
                    }

                    var partnerOverride = loadedHumanOverrides[foundAttractedTo].RemoveAndReturn(partnerId);
                    overridenHumans[__instance.humanID] = (__instance, partnerOverride);

                    PluginLogger.LogInfo($"Setting override: {overridenHumans[__instance.humanID].Item2.firstName} {overridenHumans[__instance.humanID].Item2.lastName} ({overridenHumans[__instance.humanID].Item2.id}) will replace {__instance.humanID} as a partner of {newPartner.humanID}");

                    return false;
                }

                return true;
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

                    int dayOfBirth = int.TryParse(overrideHuman.dayOfBirth, out dayOfBirth) && dayOfBirth > 0 && dayOfBirth <= 31 ? dayOfBirth : int.Parse(__instance.birthday.Split("/")[0]);
                    int monthOfBirth = int.TryParse(overrideHuman.monthOfBirth, out monthOfBirth) && monthOfBirth > 0 && monthOfBirth <= 12 ? monthOfBirth : int.Parse(__instance.birthday.Split("/")[1]);
                    int yearOfBirth = int.Parse(__instance.birthday.Split("/")[2]);

                    __instance.birthday = dayOfBirth.ToString() + "/" + monthOfBirth.ToString() + "/" + yearOfBirth.ToString();

                    if (int.TryParse(overrideHuman.age, out int age))
                    {
                        if(__instance.GetAge() != age)
                        {
                            yearOfBirth -= (age - __instance.GetAge());
                            __instance.birthday = dayOfBirth.ToString() + "/" + monthOfBirth.ToString() + "/" + yearOfBirth.ToString();
                        }
                    }
                    
                }
            }
        }

        // Can't patch the constructor, so patch the child methods instead
        // Patches to override social stats and reset them after generating
        public class Descriptors_ConstructorCalls
        {
            static Vector2 cachedStats_height;
            static float cachedStates_heightAvg;
            static int cachedStates_menWithBeards;
            static int cachedStates_menWithMoustaches;
            static int cachedStates_glassesRatio;

            [HarmonyPatch(typeof(Descriptors), "GenerateEthnicity")]
            public class Descriptors_ConstructorFirst_GenerateEthnicity
            {
                static void Prefix(Descriptors __instance)
                {
                    // For those values we can't directly change, change the social stats after caching
                    cachedStats_height = SocialStatistics.Instance.heightMinMax;
                    cachedStates_heightAvg = SocialStatistics.Instance.averageHeight;
                    /*
                    // These don't actually work - the game actually uses traits for this, not these statistics 
                    cachedStates_menWithBeards = SocialStatistics.Instance.menWithBeards;
                    cachedStates_menWithMoustaches = SocialStatistics.Instance.menWithMoustaches;
                    cachedStates_glassesRatio = SocialStatistics.Instance.glassesRatio;
                    */

                    if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                    {
                        var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;

                        // Setup the height override if set
                        if (overrideHuman.height != "" && float.TryParse(overrideHuman.height, out var height))
                        {
                            SocialStatistics.Instance.heightMinMax = new Vector2(height, height);
                            SocialStatistics.Instance.averageHeight = height;
                        }

                        /*
                        if (overrideHuman.beard == "true") SocialStatistics.Instance.menWithBeards = 100; else if (overrideHuman.beard == "false") SocialStatistics.Instance.menWithBeards = 0;
                        if (overrideHuman.moustache == "true") SocialStatistics.Instance.menWithMoustaches = 100; else if (overrideHuman.moustache == "false") SocialStatistics.Instance.menWithMoustaches = 0;
                        if (overrideHuman.glasses == "true") SocialStatistics.Instance.glassesRatio = 100; else if (overrideHuman.glasses == "false") SocialStatistics.Instance.glassesRatio = 0;
                        */
                    }
                }
            }

            [HarmonyPatch(typeof(Descriptors), "GenerateFacialFeatures")]
            public class Descriptors_ConstructorLast_GenerateFacialFeatures
            {
                static void Postfix()
                {
                    SocialStatistics.Instance.heightMinMax = cachedStats_height;
                    SocialStatistics.Instance.averageHeight = cachedStates_heightAvg;
                    /*
                    SocialStatistics.Instance.menWithBeards = cachedStates_menWithBeards;
                    SocialStatistics.Instance.menWithMoustaches = cachedStates_menWithMoustaches;
                    SocialStatistics.Instance.glassesRatio = cachedStates_menWithMoustaches;
                    */
                }
            }
        }
        
        // If ethnicity is set only once, set it twice (otherwise the game might roll a random secondary)
        // The game will still add a third ethnicity, so we remove it before generating a skin colour
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
            public static void Prefix(Descriptors __instance)
            {
                if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;

                    // If they have an ethnicity, an extra one would have been added by the end of GenerateEthnicity
                    // Remove it before we generate colour
                    if (overrideHuman.ethnicity1 != null && overrideHuman.ethnicity1 != "")
                    {
                        __instance.ethnicities.RemoveAt(__instance.ethnicities.Count - 1);
                    }
                }
            }

            public static void Postfix(Descriptors __instance)
            {
                if (overridenHumans.ContainsKey(__instance.citizen.humanID))
                {
                    var overrideHuman = overridenHumans[__instance.citizen.humanID].Item2;

                    if(overrideHuman == playerOverride)
                    {
                        // Skip the player, their name is already set
                        return;
                    }

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
                }
            }
        }

        [HarmonyPatch(typeof(Human), nameof(Human.GetTraitChance))]
        public class Human_GetTraitChance
        {
            public static bool Prefix(Human __instance, CharacterTrait trait, ref float __result)
            {
                if (overridenHumans.TryGetValue(__instance.humanID, out var overrideHumanTuple))
                {
                    if(overrideHumanTuple.Item2.traits.Contains(trait.presetName.ToLowerInvariant()))
                    {
                        __result = 1;
                        return false;
                    }

                    if (overrideHumanTuple.Item2.notTraits.Contains(trait.presetName.ToLowerInvariant()))
                    {
                        __result = 0;
                        return false;
                    }
                }

                return true;
            }
        }
    }
}