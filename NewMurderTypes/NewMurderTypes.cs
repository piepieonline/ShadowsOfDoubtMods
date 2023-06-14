using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Configuration;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using UniverseLib;
using AssetBundleLoader;

namespace NewMurderTypes
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class NewMurderTypes : BasePlugin
    {
        public static ManualLogSource Logger;
        public static ConfigFile ConfigFile;

        public static bool loadMurderBundle;
        public static bool loadSideJobBundle;
        public static bool modifyCiphers;

        public static string DEBUG_LoadSpecificMurder;
        public static string DEBUG_LoadSpecificSideJob;

        public override void Load()
        {
            if (!Config.Bind("General", "Enabled", true).Value)
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is disabled.");
                return;
            }

            loadMurderBundle = Config.Bind("General", "Load custom murder content", true).Value;
            loadSideJobBundle = Config.Bind("General", "Load custom side job content", true).Value;
            modifyCiphers = Config.Bind("General", "Modify killer ciphers", true).Value;

            DEBUG_LoadSpecificMurder = Config.Bind("Debug", "Force specific MurderMO", "").Value;

            Logger = Log;
            ConfigFile = Config;

            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }
    }

    // TODO: Breaking on reload?
    [HarmonyPatch(typeof(Toolbox), "Start")]
    public class Toolbox_Start
    {
        public static void Postfix()
        {
            if (!NewMurderTypes.loadMurderBundle)
            {
                NewMurderTypes.Logger.LogInfo($"Not loading murder bundle.");
                return;
            }

            var murderBundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "BundleContent\\newmurdertypes"), true);

            foreach (var asset in murderBundle.LoadAllAssets().Where(asset => asset.GetActualType() == typeof(MurderPreset)))
            {
                MurderPreset murderPreset = asset.Cast<MurderPreset>();
                NewMurderTypes.Logger.LogInfo($"Loading MurdererPreset: {murderPreset.name}");
                Toolbox.Instance.allMurderPresets.Add(murderPreset);
            }
            
            foreach (var asset in murderBundle.LoadAllAssets().Where(asset => asset.GetActualType() == typeof(MurderMO)))
            {
                MurderMO mo = asset.Cast<MurderMO>();
                NewMurderTypes.Logger.LogInfo($"Loading mo: {mo.name}{(mo.disabled ? " (DISABLED)" : "")}");
                Toolbox.Instance.allMurderMOs.Add(mo);
            }

            // Force single type for testing
            if(NewMurderTypes.DEBUG_LoadSpecificMurder != "")
            {
                for (int i = Toolbox.Instance.allMurderMOs.Count - 1; i >= 0; i--)
                {
                    if (Toolbox.Instance.allMurderMOs[i].name != NewMurderTypes.DEBUG_LoadSpecificMurder)
                        Toolbox.Instance.allMurderMOs.RemoveAt(i);
                }
            }
        }
    }

    // Random Ciphers instead of always the same
    [HarmonyPatch(typeof(Strings), "GetContainedValue")]
    public class Strings_GetContainedValue
    {
        static int CIPHER_COUNT = 3;

        enum CipherTypes
        {
            Standard,
            RotX,
            Atbash,
        }

        static Dictionary<string, CipherTypes> selectedCiphers = new Dictionary<string, CipherTypes>();

        public static bool Prefix(ref string __result, Il2CppSystem.Object baseObject, string withinScope, string newValue, UnityEngine.Object inputObject, Evidence baseEvidence, Strings.LinkSetting linkSetting, Il2CppSystem.Collections.Generic.List<Evidence.DataKey> evidenceKeys, Il2CppSystem.Object additionalObject, bool knowCitizenGender)
        {
            if (!NewMurderTypes.modifyCiphers)
            {
                NewMurderTypes.Logger.LogInfo($"Not adding ciphers.");
                return true;
            }

            if (newValue == "killernamecipher")
            {
                var murderer = inputObject.Cast<Citizen>();
                var murdererName = murderer.citizenName;
                
                var nameAsIntValue = murdererName.ToCharArray().Aggregate(0, (result, c) => result + c);

                if (!selectedCiphers.ContainsKey(murdererName))
                {
                    selectedCiphers.Add(murdererName, (CipherTypes)(nameAsIntValue % CIPHER_COUNT));
                }

                switch (selectedCiphers[murdererName])
                {
                    case CipherTypes.RotX:
                        // Generate a random rotation value, at least 5 away
                        __result = ROTX(murdererName, (nameAsIntValue % 16) + 5);
                        return false;
                    case CipherTypes.Atbash:
                        __result = GetAtbash(murdererName);
                        return false;
                    case CipherTypes.Standard:
                    default:
                        return true;
                }
            }

            return true;
        }

        // Taken from https://dotnetfiddle.net/TeL8qu - JerryChen
        static string ROTX(string input, int amount)
        {
            if (input.Length > 0)
            {
                //char [] origCharArray = new char[input.Length];
                char[] retCharArray = new char[input.Length];
                for (int i = 0; i < input.Length; i++)
                {
                    char curChar = input[i];
                    if (System.Char.IsLetter(curChar))
                    { // current Char is alphanumeric
                      //System.Globalization.CharUnicodeInfo.GetNumericValue(curChar);
                        int tempVal = (int)curChar + amount % 26;
                        if (System.Char.IsLower(curChar))
                        { // check if the tempVal is greater than the value of 'z'
                            if (tempVal > (int)'z')
                            {
                                tempVal = (int)'a' + (tempVal - (int)'z') - 1;
                            }
                        }
                        else
                        { // upper case check if tempVal is greater than 'Z'
                            if (tempVal > (int)'Z')
                            {
                                tempVal = (int)'A' + (tempVal - (int)'Z') - 1;
                            }
                        }

                        retCharArray[i] = (char)(tempVal);
                    }
                    else
                    {
                        retCharArray[i] = curChar;
                    }
                }

                return string.Concat(retCharArray);
            }

            return "";
        }

        static string GetAtbash(string s)
        {
            var charArray = s.ToCharArray();

            for (int i = 0; i < charArray.Length; i++)
            {
                char c = charArray[i];

                if (c >= 'a' && c <= 'z')
                {
                    charArray[i] = (char)(96 + (123 - c));
                }

                if (c >= 'A' && c <= 'Z')
                {
                    charArray[i] = (char)(64 + (91 - c));
                }
            }

            return System.String.Concat(charArray);
        }
    }

    // Disabled until worth including
    // [HarmonyPatch(typeof(SideJobController), "Start")]
    public class SideJobController_Start
    {
        public static void Prefix()
        {
            if (!NewMurderTypes.loadSideJobBundle)
            {
                NewMurderTypes.Logger.LogInfo($"Not loading side-job bundle.");
                return;
            }

            var sideJobBundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "BundleContent\\newsidejobtypes"), true);

            foreach (var asset in sideJobBundle.LoadAllAssets().Where(asset => asset.GetActualType() == typeof(JobPreset)))
            {
                JobPreset jobPreset = asset.Cast<JobPreset>();
                NewMurderTypes.Logger.LogInfo($"Loading JobPreset: {jobPreset.name}{(jobPreset.disabled ? " (DISABLED)" : "")}");
                Toolbox.Instance.allSideJobs.Add(jobPreset);
            }

            if(NewMurderTypes.DEBUG_LoadSpecificSideJob != "")
            {
                for (int i = Toolbox.Instance.allSideJobs.Count - 1; i >= 0; i--)
                {
                    if (Toolbox.Instance.allSideJobs[i].name != NewMurderTypes.DEBUG_LoadSpecificSideJob)
                        Toolbox.Instance.allSideJobs.RemoveAt(i);
                }
            }
        }
    }


    // Murder Debugging
    // [HarmonyPatch(typeof(MurderController), "Update")]
    public class MurderController_Update
    {
        public static void Postfix()
        {
            if (Murder_SetMurderState.murderIsExecuting)
            {
                Murder_SetMurderState.murderDuration += SessionData.Instance.gameTimePassedThisFrame;

                if (Murder_SetMurderState.murderDuration > 60)
                {
                    NewMurderTypes.Logger.LogInfo($"Time passed: {Time.time} - {Murder_SetMurderState.murderDuration}");
                }
            }
        }
    }

    // [HarmonyPatch(typeof(MurderController.Murder), "SetMurderState")]
    public class Murder_SetMurderState
    {
        public static bool murderIsExecuting;
        public static double murderDuration;

        public static void Postfix(MurderController.MurderState newState)
        {
            if (newState == MurderController.MurderState.executing)
            {
                murderIsExecuting = true;
                murderDuration = 0;
                NewMurderTypes.Logger.LogInfo($"Time started: {Time.time}");
            }
            else
            {
                if(murderIsExecuting)
                    NewMurderTypes.Logger.LogInfo($"Time ended: {Time.time} - {murderDuration}");
                murderIsExecuting = false;
            }
        }
    }
}
