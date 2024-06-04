using System.IO;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Cpp2IL.Core.Extensions;

using static Descriptors;

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
            public string hairColourExact;
            public string hairType;
            public string eyeColour;

            public string glasses;
            public string beard;
            public string moustache;
        }

        static System.Collections.Generic.List<HumanOverride> loadedHumanOverrides = new System.Collections.Generic.List<HumanOverride>();
        static System.Collections.Generic.Dictionary<int, (Human, HumanOverride)> overridenHumans = new System.Collections.Generic.Dictionary<int, (Human, HumanOverride)>();

#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            ReloadCitizensFromFile();

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }

        static void ReloadCitizensFromFile()
        {
            var path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "citizens.csv");
            if (!File.Exists(path))
            {
                PluginLogger.LogError("Citizen file not found, no citizen overrides will be loaded. See https://docs.google.com/spreadsheets/d/11OvB572ezm2j-iO2rd89XHInrTNXey83eLdw2VJcujE/edit#gid=1379962932");
                return;
            }

            string[] citizensCSV = File.ReadAllLines(path);

            loadedHumanOverrides.Clear();

            // Header
            for (int i = 1; i < citizensCSV.Length; i++)
            {
                string[] vals = citizensCSV[i].Split(",");
                loadedHumanOverrides.Add(new HumanOverride()
                {
                    firstName = vals[0],
                    lastName = vals[1],
                    casualName = vals[2],
                    dayOfBirth = vals[3],
                    monthOfBirth = vals[4],
                    age = vals[5],
                    gender = vals[6],
                    attractedTo = vals[7],
                    ethnicity1 = vals[8],
                    ethnicity2 = vals[9],
                    height = vals[10],
                    buildType = vals[11],
                    hairColour = vals[12],
                    hairType = vals[13],
                    eyeColour = vals[14],
                    glasses = vals[15],
                    beard = vals[16],
                    moustache = vals[17]
                });
            }

            if (loadedHumanOverrides.Count == 0)
            {
                PluginLogger.LogError("Citizen file found, but no citizen overrides loaded. No overrides will be applied.");
                return;
            }
        }

        static void LogParseFailure(Human human, HumanOverride humanOverride, string field)
        {
            PluginLogger.LogWarning($"Human {human.humanID} (Override name: {humanOverride.firstName} {humanOverride.lastName}) failed to parse {field}");
        }

        // Partner is set after this, random dice roll.. might have to override it to force, no neat hook location
        [HarmonyPatch(typeof(Human), "SetSexualityAndGender")]
        public class Human_SetSexualityAndGender
        {
            public static void Postfix(Human __instance)
            {
                if (SessionData.Instance.isFloorEdit || CityConstructor.Instance.generateNew)
                {
                    if (loadedHumanOverrides.Count > 0)
                    {
                        if (__instance.gender == Human.Gender.male && __instance.attractedTo.Contains(Human.Gender.female) && __instance.attractedTo.Count == 1)
                        {
                            overridenHumans[__instance.humanID] = (__instance, loadedHumanOverrides.RemoveAndReturn(0));
                            PluginLogger.LogInfo($"Setting override: {overridenHumans[__instance.humanID].Item2.firstName} {overridenHumans[__instance.humanID].Item2.lastName} will replace {__instance.humanID}");
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
                    if(
                        int.TryParse(overrideHuman.dayOfBirth, out int dayOfBirth) &&  dayOfBirth > 0 && dayOfBirth <= 31 && 
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
            public static void Postfix(string input, ref string output, ref float __result)
            {
                if (input.StartsWith("piecitizenoverride"))
                {
                    var vals = input.Split('_');
                    output = vals[2];
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

                        if (!System.Enum.TryParse<EthnicGroup>(overrideHuman.ethnicity1, out var eth1))
                        {
                            LogParseFailure(__instance.citizen, overrideHuman, "ethnicity1");
                            return;
                        }
                        if (!System.Enum.TryParse<EthnicGroup>(overrideHuman.ethnicity2, out var eth2))
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
                        if (System.Enum.TryParse<EyeColour>(overrideHuman.eyeColour, out var setEyeColour))
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
                    __instance.citizen.firstName = overrideHuman.firstName;
                    __instance.citizen.surName = overrideHuman.lastName;
                    __instance.citizen.casualName = overrideHuman.casualName ?? __instance.citizen.firstName;
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
                    if (overrideHuman.hairColour != null && overrideHuman.hairColour != "")
                    {
                        if (System.Enum.TryParse<HairColour>(overrideHuman.hairColour, out var setHairColour))
                        {
                            __instance.hairColourCategory = setHairColour;

                            if (overrideHuman.hairColourExact != null && overrideHuman.hairColourExact != "")
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
                                        __instance.hairColour = Color.Lerp(hairColourSetting.hairColourRange1, hairColourSetting.hairColourRange2, Toolbox.Instance.GetPsuedoRandomNumberContained(0.0f, 1f, __instance.citizen.seed, out _));
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

                    if (overrideHuman.hairType != null)
                    {
                        if (System.Enum.TryParse<HairStyle>(overrideHuman.hairType, out var setHairStyle))
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
                    if (overrideHuman.buildType != null && overrideHuman.buildType != "")
                    {
                        if (System.Enum.TryParse<BuildType>(overrideHuman.buildType, out var setValue))
                        {
                            __instance.build = setValue;
                        }
                        else
                        {
                            LogParseFailure(__instance.citizen, overrideHuman, "buildType");
                        }
                    }

                    // Setup the height override if set
                    if (overrideHuman.height != null && overrideHuman.height != "" && float.TryParse(overrideHuman.height, out var height))
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
                        if(overrideHuman.glasses == "true") glasses = OverrideFeature.On;
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