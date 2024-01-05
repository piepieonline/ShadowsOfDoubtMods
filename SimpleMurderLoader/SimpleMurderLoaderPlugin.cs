using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UniverseLib;
using Newtonsoft.Json;

#if MONO
using BepInEx.Unity.Mono;
using System.Collections.Generic;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
using Il2CppSystem.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
#endif

namespace SimpleMurderLoader
{

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class SimpleMurderLoaderPlugin : BaseUnityPlugin
#elif IL2CPP
    public class SimpleMurderLoaderPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        public static string DEBUG_LoadSpecificMurder;

        public static bool CreateMapping = false;
#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif

            DEBUG_LoadSpecificMurder = Config.Bind("Debug", "Force specific MurderMO", "").Value;

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
        }
    }

    [HarmonyPatch(typeof(Toolbox), "Start")]
    public class Toolbox_Start
    {
        static Dictionary<System.Type, Dictionary<string, ScriptableObject>> resourceCacheRef;
        static Dictionary<string, (ScriptableObject scriptableObject, string fileId)> soMapping = new Dictionary<string, (ScriptableObject scriptableObject, string fileId)>();
        
        public static void Postfix()
        {
            List<ScriptableObject> mapping = new List<ScriptableObject>();
            List<(ScriptableObject so, string type, string name)> typeNameMapping = new List<(ScriptableObject, string, string)>();
            foreach (var so in RuntimeHelper.FindObjectsOfTypeAll(typeof(ScriptableObject)))
            {
                mapping.Add((ScriptableObject)so);
                typeNameMapping.Add(((ScriptableObject)so, so.GetActualType().Name, so.name));
                // soMapping[so.GetActualType().Name + "|" + so.name] = (ScriptableObject)so;
            }
            JObject jobject = JObject.Parse(JsonUtilityArrays.ToJson(mapping.ToArray(), false));
            var soIndex = 0;
            foreach (var child in jobject.SelectToken("Items").Children())
            {
                child["name"] = typeNameMapping[soIndex].name;
                child["type"] = typeNameMapping[soIndex].type;
                soMapping[child["type"] + "|" + child["name"]] = (
                    typeNameMapping[soIndex].so,
                    "{\"m_FileID\":" + child["m_FileID"] + ",\"m_PathID\":" + child["m_PathID"] + "}"
                );
                soIndex++;
            }
            if (SimpleMurderLoaderPlugin.CreateMapping)
            {
                System.IO.File.WriteAllText("D:\\Game Modding\\ShadowsOfDoubt\\Documentation\\allSOs.json", jobject.ToString(Newtonsoft.Json.Formatting.Indented));
            }

            LoadManifest("""E:\SteamLibrary\steamapps\common\Shadows of Doubt mono\BepInEx\plugins\SimpleMurderLoader\ExampleMurders\TheftGoneWrong\""");

            // Force single type for testing
            if (SimpleMurderLoaderPlugin.DEBUG_LoadSpecificMurder != "")
            {
                SimpleMurderLoaderPlugin.PluginLogger.LogInfo($"Forcing MurderMO: {SimpleMurderLoaderPlugin.DEBUG_LoadSpecificMurder}");
                for (int i = Toolbox.Instance.allMurderMOs.Count - 1; i >= 0; i--)
                {
                    if (Toolbox.Instance.allMurderMOs[i].name != SimpleMurderLoaderPlugin.DEBUG_LoadSpecificMurder)
                        Toolbox.Instance.allMurderMOs[i].disabled = true; // TODO: Cache the current state to allow changing at runtime
                }
            }
        }

        private static void LoadManifest(string folderPath)
        {
            var manifest = JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(folderPath, "murdermanifest.sodso.json")));
            foreach(var file in manifest["fileOrder"])
            {
                LoadFileToGame(System.IO.Path.Combine(folderPath, file.Value<string>().Replace("REF:", "") + ".sodso.json"));
            }
        }

        public static List<JToken> Search(JObject newSOJSON, string path)
        {
            return newSOJSON.SelectTokens(path).ToList();
        }

        private static string LoadFileToGame(string filePath)
        {
            var newSOJSON = JObject.Parse(System.IO.File.ReadAllText(filePath));

            var fileType = newSOJSON.Value<string>("type");
            newSOJSON.SelectToken("type").Replace(null);

            string copyFrom = newSOJSON.Value<string>("copyFrom");
            if(copyFrom != null)
            {
                newSOJSON.SelectToken("copyFrom").Replace(null);
            }

            ExtractAndReplaceTokens(newSOJSON.FindTokensByValueMatch(new System.Text.RegularExpressions.Regex("^REF")));

            ScriptableObject newSO;

            if (copyFrom != null && copyFrom.Contains("|"))
            {
                newSO = ScriptableObject.Instantiate(soMapping[copyFrom].scriptableObject);
            }
            else
            {
                newSO = ScriptableObject.CreateInstance(fileType);
            }

            JsonUtility.FromJsonOverwrite(newSOJSON.ToString(), newSO);
            newSO.name = newSOJSON.SelectToken("name").ToString();

            switch (fileType)
            {
                case "MurderMO":
                    Toolbox.Instance.allMurderMOs.Add((MurderMO)newSO);
                    break;
                case "InteractablePreset":
                    Toolbox.Instance.objectPresetDictionary.Add(newSO.name, (InteractablePreset)newSO);
                    break;
            }

            if (resourceCacheRef == null)
                resourceCacheRef = (Dictionary<Type, Dictionary<string, ScriptableObject>>)typeof(Toolbox)
                    .GetField("resourcesCache", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(Toolbox.Instance);

#if MONO
            resourceCacheRef[newSO.GetActualType()].Add(newSO.name, newSO);
#elif IL2CPP
            resourceCacheRef[newSO.GetActualType()].Add(newSO.name, newSO);
#endif

            var key = fileType + "|" + newSO.name;
            var arr = new List<ScriptableObject>();
            arr.Add(newSO);
            var value = JObject
                .Parse(JsonUtilityArrays.ToJson(arr.ToArray()))
                .SelectToken("Items[0]").ToString()
            ;

            soMapping.Add(key, (newSO, value));

            return key;
        }

        private static void ExtractAndReplace(JObject json, string token)
        {
            ExtractAndReplaceTokens(json.SelectTokens(token).ToList());
        }

        private static void ExtractAndReplaceTokens(List<JToken> tokens)
        {
            tokens.ForEach(token =>
            {
                if (token.Type == JTokenType.Array)
                {
                    var newArr = new List<JObject>();

                    foreach (var item in token)
                    {
                        var tokenValue = item.Value<string>().Replace("REF:", "");
                        newArr.Add(JObject.Parse(soMapping[tokenValue].fileId));
                    }

                    token.Replace(JArray.FromObject(newArr));
                }
                else
                {
                    var tokenValue = token.Value<string>().Replace("REF:", "");
                    token.Replace(JObject.Parse(soMapping[tokenValue].fileId));
                }
            });
        }
    }

    public static class JsonExtensions
    {
        public static List<JToken> FindTokensByValueMatch(this JToken containerToken, System.Text.RegularExpressions.Regex value)
        {
            List<JToken> matches = new List<JToken>();
            FindTokensByValueMatch(containerToken, value, matches);
            return matches;
        }

        private static void FindTokensByValueMatch(JToken containerToken, System.Text.RegularExpressions.Regex value, List<JToken> matches)
        {
            if (containerToken.Type == JTokenType.Object)
            {
                foreach (JProperty child in containerToken.Children<JProperty>())
                {
                    FindTokensByValueMatch(child.Value, value, matches);
                }
            }
            else if (containerToken.Type == JTokenType.Array)
            {
                foreach (JToken child in containerToken.Children())
                {
                    FindTokensByValueMatch(child, value, matches);
                }
            }
            else if (containerToken.Type == JTokenType.String)
            {
                if (value.Match(containerToken.Value<string>()).Success)
                {
                    matches.Add(containerToken);
                }
            }
        }
    }

    /*
    public class JObject
    {
        object internalObject;

        static Assembly _Assembly;
        static Type _Type_JObject;
        static Type _Type_JToken;

        static MethodInfo _Parse_String;
        static MethodInfo _SelectToken_String;
        static MethodInfo _SelectTokens_String;
        static MethodInfo _Replace_JObject;
        static MethodInfo _ToString;
        static MethodInfo _ToString_Formatting;

        private JObject(object _JObject) 
        {
            internalObject = _JObject;
        }

        public static JObject Parse(string json)
        {
            if(_Parse_String == null)
            {
                _Assembly = Assembly.LoadFile(""""D:\Game Modding\r2modman\_Data\ShadowsofDoubt\profiles\LocalModding\BepInEx\plugins\SimpleMurderLoader\Newtonsoft.Json.dll"""");
                _Type_JObject = _Assembly.GetType("Newtonsoft.Json.Linq.JObject");
                _Type_JToken = _Assembly.GetType("Newtonsoft.Json.Linq.JToken");
                _Parse_String = _Type_JToken.GetMethod("Parse", new System.Type[] { typeof(string) });
                _SelectToken_String = _Type_JObject.GetMethod("SelectToken", new System.Type[] { typeof(string) });
                _SelectTokens_String = _Type_JObject.GetMethod("SelectTokens", new System.Type[] { typeof(string) });
                _Replace_JObject = _Type_JObject.GetMethod("Replace", new System.Type[] { _Type_JObject });

                _ToString = _Type_JObject.GetMethod("ToString", new System.Type[] {  });
                _ToString_Formatting = _Type_JObject.GetMethod("ToString", new System.Type[] { _Assembly.GetType("Newtonsoft.Json.Formatting") });
            }

            return new JObject(_Parse_String.Invoke(null, new object[] { json }));
        }

        public JObject SelectToken(string jpath)
        {
            return new JObject(_SelectToken_String.Invoke(internalObject, new object[] { jpath }));
        }
        
        public System.Collections.Generic.List<JObject> SelectTokens(string jpath)
        {
            var list = new System.Collections.Generic.List<JObject>();

            foreach(var token in ((System.Collections.Generic.IEnumerable<object>)_SelectTokens_String.Invoke(internalObject, new object[] { jpath })))
            {
                list.Add(new JObject(token));
            }

            return list;
        }


        public JObject Replace(JObject replacement)
        {
            return new JObject(_Replace_JObject.Invoke(internalObject, new object[] { replacement.internalObject }));
        }

        public JObject Replace(string replacement)
        {
            return Replace(JObject.Parse(replacement));
        }

        public override string ToString()
        {
            return (string)_ToString.Invoke(internalObject, null);
        }

        // TODO: Not working, _ToString_Formatting not found
        public string ToString(Newtonsoft.Json.Formatting formatting)
        {
            return (string)_ToString_Formatting.Invoke(internalObject, new object[] { formatting });
        }
    }
    */
}