using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace MoreMurderCyphers
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class MoreMurderCyphers : BasePlugin
    {
        public static ManualLogSource Logger;

        const int CYPHER_PLAINTEXT_OPTIONS_COUNT = 4;
        enum CypherPlaintextOptions
        {
            FullName,
            FirstInitial,
            Workplace,
            Address
        }

        public override void Load()
        {
            if (!Config.Bind("General", "Enabled", true).Value)
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is disabled.");
                return;
            }

            // loadMurderBundle = Config.Bind("General", "Load custom murder content", true).Value;
            // enableVanillaCypher = Config.Bind("General", "Load custom murder content", true).Value;

            Logger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
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
                if (newValue == "killernamecipher")
                {
                    var murderer = inputObject.Cast<Citizen>();
                    var murdererName = murderer.citizenName;

                    var nameAsIntValue = murdererName.ToCharArray().Aggregate(0, (result, c) => result + c);

                    var plainText = murdererName;

                    Logger.LogInfo($"Picking new cipher...");

                    switch ((CypherPlaintextOptions)(nameAsIntValue % CYPHER_PLAINTEXT_OPTIONS_COUNT))
                    {
                        case CypherPlaintextOptions.FullName:
                            plainText = murderer.citizenName;
                            break;
                        case CypherPlaintextOptions.FirstInitial:
                            plainText = murderer.GetInitialledName();
                            break;
                        case CypherPlaintextOptions.Workplace:
                            plainText = murderer.job?.employer?.name;
                            break;
                        case CypherPlaintextOptions.Address:
                            plainText = murderer.home?.building.name;
                            break;
                    }

                    Logger.LogDebug($"Using plaintext {nameAsIntValue % CYPHER_PLAINTEXT_OPTIONS_COUNT}");

                    // Catch jobless or homeless cases
                    if (plainText == null)
                    {
                        plainText = murdererName;
                        Logger.LogDebug($"Falling Back");
                    }

                    Logger.LogDebug($"Using cypher {nameAsIntValue % CIPHER_COUNT}");
                    Logger.LogDebug($"Plaintext: '{plainText}'");

                    switch ((CipherTypes)(nameAsIntValue % CIPHER_COUNT))
                    {
                        case CipherTypes.RotX:
                            // Generate a random rotation value, at least 5 away
                            __result = ROTX(plainText, (nameAsIntValue % 16) + 5);
                            Logger.LogDebug($"Cyphertext: '{__result}'");
                            return false;
                        case CipherTypes.Atbash:
                            __result = GetAtbash(plainText);
                            Logger.LogDebug($"Cyphertext: '{__result}'");
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

    }
}