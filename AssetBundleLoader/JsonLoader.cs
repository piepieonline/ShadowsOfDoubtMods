using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using UnityEngine;
using UniverseLib;
using System.Reflection;
using System.IO;
using HarmonyLib;
using static AssetBundleLoader.JsonLoader.NewtonsoftExtensions;

namespace AssetBundleLoader
{
    public static class JsonLoader
    {
        static Dictionary<string, (ScriptableObject scriptableObject, string fileId)> soMapping = new System.Collections.Generic.Dictionary<string, (ScriptableObject scriptableObject, string fileId)>();

        [HarmonyPriority(Priority.VeryHigh)]
        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                // We need to create a list of all ScriptableObject in the game, and the fileID associated
                // We use this to rewrite the "REF:*" text references before Unity deserialises for us
                var mapping = new Il2CppSystem.Collections.Generic.List<ScriptableObject>();
                var typeNameMapping = new List<(ScriptableObject so, string type, string name)>();
                foreach (var uo in RuntimeHelper.FindObjectsOfTypeAll(typeof(ScriptableObject)))
                {
                    ScriptableObject so = ((dynamic)uo).Cast<ScriptableObject>();
                    mapping.Add(so);
                    var soType = so.GetActualType().Name;
                    var soName = so.name;
                    typeNameMapping.Add(((ScriptableObject)so, soType, soName));
                }

                dynamic jobject = NewtonsoftJson.JToken_Parse(JsonUtilityArrays.ToJson(mapping.ToArray(), false));


                var soIndex = 0;
                foreach (var child in jobject["Items"])
                {
                    child["name"] = typeNameMapping[soIndex].name;
                    child["type"] = typeNameMapping[soIndex].type;

                    string key = child["type"].ToString() + "|" + child["name"].ToString();

                    ScriptableObject value1 = typeNameMapping[soIndex].so;
                    string value2 = "{\"m_FileID\":" + child["m_FileID"].ToString() + ",\"m_PathID\":" + child["m_PathID"].ToString() + "}";

                    soMapping[key] = (
                        value1,
                        value2
                    );

                    soIndex++;
                }
            }
        }

        private static void SerializeTypes(ref Dictionary<string, Dictionary<string, string>> dict, Type soType)
        {
            if (soType.FullName == null || !soType.IsSubclassOf(typeof(ScriptableObject))) return;
            dict[soType.FullName] = new Dictionary<string, string>();
            foreach(var field in soType.GetProperties())
            {
                if(field.PropertyType.IsValueType)
                {
                    dict[soType.FullName][field.Name] = field.PropertyType.FullName;
                }
                else if(field.PropertyType.IsArray)
                {
                    SerializeTypes(ref dict, field.PropertyType.GetElementType());
                    dict[soType.FullName][field.Name] = field.PropertyType.GetElementType().FullName;
                }
                else if(field.PropertyType.IsGenericType)
                {
                    SerializeTypes(ref dict, field.PropertyType.GetGenericTypeDefinition());
                    dict[soType.FullName][field.Name] = field.PropertyType.GetGenericTypeDefinition().FullName;
                }
                else
                {
                    SerializeTypes(ref dict, field.PropertyType);
                }
                dict[soType.FullName][field.Name] = field.PropertyType.FullName;
            }
        }

        public static ScriptableObject LoadFileToGame(string json)
        {
            var newSOJSON = NewtonsoftJson.JToken_Parse(json);

            var fileType = newSOJSON.Value<string>("fileType");
            newSOJSON.SelectToken("fileType").Replace(null);

            string copyFrom = newSOJSON.Value<string>("copyFrom");
            if (copyFrom != null)
            {
                newSOJSON.SelectToken("copyFrom").Replace(null);
            }

            // Replace references to existing (or created before us) ScriptableObjets
            ExtractAndReplaceTokens(JsonExtensions.FindTokensByValueMatch(newSOJSON, new System.Text.RegularExpressions.Regex("^REF")));

            ScriptableObject newSO;

            // Shortcut, so we don't have to create everything from scratch
            if (copyFrom != null && copyFrom.Contains("|"))
            {
                newSO = ScriptableObject.Instantiate(soMapping[copyFrom.Replace("REF:", "").Trim()].scriptableObject);
            }
            else
            {
                newSO = ScriptableObject.CreateInstance(fileType);
            }

            // Use the restored FromJson method to parse the completed JSON files
            newSO = RestoredJsonUtility.FromJsonInternal(newSOJSON.ToString(), newSO);
            newSO.name = newSOJSON.SelectToken("name").ToString();

            FixScriptableObjects(newSO, newSOJSON.ToString());

            Toolbox.Instance.ProcessLoadedScriptableObject(newSO);

            // Cache the ID of this new object, so following objects can refer to it
            string key = fileType + "|" + newSO.name;
            var arr = new Il2CppSystem.Collections.Generic.List<ScriptableObject>();
            arr.Add(newSO);
            string value = NewtonsoftJson.JToken_Parse(JsonUtilityArrays.ToJson(arr.ToArray()))
                .SelectToken("Items[0]").ToString()
            ;

            soMapping.Add(key, (newSO, value));

            return newSO;
        }

        private static void ExtractAndReplace(dynamic json, string token)
        {
            ExtractAndReplaceTokens(json.SelectTokens(token).ToList());
        }

        private static void ExtractAndReplaceTokens(System.Collections.Generic.List<dynamic> tokens)
        {
            foreach (var token in tokens)
            {
                var tokenType = NewtonsoftJson.JToken_Type(token);
                if (tokenType == e_JToken_Type.Array)
                {
                    var newArr = new Il2CppSystem.Collections.Generic.List<dynamic>();

                    foreach (var item in token.Children<dynamic>())
                    {
                        var tokenValue = item.ToString().Replace("REF:", "");
                        newArr.Add(NewtonsoftJson.JToken_Parse(soMapping[tokenValue].Item2));
                    }

                    token.Replace(NewtonsoftJson.JArray_FromObject(newArr));
                }
                else
                {
                    var tokenValue = token.ToString().Replace("REF:", "");
                    token.Replace(NewtonsoftJson.JToken_Parse(soMapping[tokenValue].Item2));
                }
            }
        }

        private static void FixScriptableObjects(ScriptableObject so, string json)
        {
            if(so.GetActualType() == typeof(MurderMO))
            {
                MurderMO murderMO = ((dynamic)so).Cast<MurderMO>();
                foreach (var lead in murderMO.MOleads)
                {
                    for (int i = 0; i < lead.compatibleWithMotives.Count; i++)
                    {
                        if (lead.compatibleWithMotives[i] == null)
                            lead.compatibleWithMotives[i] = murderMO;
                    }
                }
            }
            
        }

        public static class JsonExtensions
        {
            public static System.Collections.Generic.List<dynamic> FindTokensByValueMatch(dynamic containerToken, System.Text.RegularExpressions.Regex value)
            {
                var matches = new System.Collections.Generic.List<dynamic>();
                FindTokensByValueMatch(containerToken, value, ref matches);
                return matches;
            }

            private static void FindTokensByValueMatch(dynamic containerToken, System.Text.RegularExpressions.Regex value, ref System.Collections.Generic.List<dynamic> matches)
            {
                var tokenType = NewtonsoftJson.JToken_Type(containerToken);

                if (tokenType == e_JToken_Type.Object)
                {
                    foreach (dynamic child in containerToken)
                    {
                        FindTokensByValueMatch(child.Value, value, ref matches);
                    }
                }
                else if (tokenType == e_JToken_Type.Array)
                {
                    foreach (dynamic child in containerToken)
                    {
                        FindTokensByValueMatch(child, value, ref matches);
                    }
                }
                else if (tokenType == e_JToken_Type.String)
                {
                    if (value.Match(containerToken.ToString()).Success)
                    {
                        matches.Add(containerToken);
                    }
                }
            }
        }

        // Replacement Newtonsoft wrapper
        public class NewtonsoftExtensions
        {
            static NewtonsoftExtensions _instance;

            AssemblyLoadContext assemblyLoadContext;
            Assembly newtonsoftJson;

            Type t_JToken;
            MethodInfo m_JToken_Parse;
            Dictionary<string, MethodInfo> m_Type_Getter;
            public enum e_JToken_Type { None, Object, Array, Constructor, Property, Comment, Integer, Float, String, Boolean, Null, Undefined, Date, Raw, Bytes, Guid, Uri, TimeSpan };

            Type t_JObject;
            MethodInfo m_JObject_Parse;
            MethodInfo m_JObject_FromObject;
            PropertyInfo m_JObject_Type;

            Type t_JArray;
            MethodInfo m_JArray_FromObject;

            private NewtonsoftExtensions()
            {
                assemblyLoadContext = new AssemblyLoadContext("NewtonsoftContext");
                assemblyLoadContext.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Newtonsoft.Json.dll"));
                newtonsoftJson = assemblyLoadContext.Assemblies.Where(a => a.GetName().Name == "Newtonsoft.Json").First();

                t_JToken = newtonsoftJson.GetType("Newtonsoft.Json.Linq.JToken");
                m_JToken_Parse = t_JToken.GetMethod("Parse", [typeof(string)]);
                m_Type_Getter = new Dictionary<string, MethodInfo>();

                t_JObject = newtonsoftJson.GetType("Newtonsoft.Json.Linq.JObject");
                m_JObject_Parse = t_JObject.GetMethod("Parse", [typeof(string)]);
                m_JObject_FromObject = t_JObject.GetMethod("FromObject", [typeof(object)]);
                m_JObject_Type = t_JObject.GetProperty("Type");

                t_JArray = newtonsoftJson.GetType("Newtonsoft.Json.Linq.t_JArray");
                m_JArray_FromObject = t_JObject.GetMethod("FromObject", [typeof(object)]);
            }

            public static NewtonsoftExtensions NewtonsoftJson
            { 
                get
                {
                    if(_instance == null)
                        _instance = new NewtonsoftExtensions();
                    return _instance;
                }
            }

            public dynamic JToken_Parse(string json)
            {
                return m_JToken_Parse.Invoke(null, [json]);
            }

            public e_JToken_Type JToken_Type(dynamic jtoken)
            {
                if(!m_Type_Getter.ContainsKey(jtoken.GetType().FullName))
                    m_Type_Getter[jtoken.GetType().FullName] = jtoken.GetType().GetProperty("Type").GetMethod;

                dynamic result = m_Type_Getter[jtoken.GetType().FullName].Invoke(jtoken, null);

                int intResult = (int)result;
                return (e_JToken_Type)intResult;
            }

            public dynamic JArray_FromObject(object obj)
            {
                return m_JArray_FromObject.Invoke(null, [obj]);
            }

            public dynamic JObject_FromObject(object obj)
            {
                return m_JObject_FromObject.Invoke(null, [obj]);
            }
        }

        // Replacement UnityEngine.JsonUtility 
        public static class RestoredJsonUtility
        {
            private delegate System.IntPtr Delegate_FromJsonInternal(System.IntPtr json, System.IntPtr scriptableObject, System.IntPtr type);
            private static Delegate_FromJsonInternal _iCallFromJsonInternal;

            public static T FromJsonInternal<T>(string json, T scriptableObject) where T : UnityEngine.Object
            {
                _iCallFromJsonInternal ??= IL2CPP.ResolveICall<Delegate_FromJsonInternal>("UnityEngine.JsonUtility::FromJsonInternal");
                System.IntPtr jsonPtr = IL2CPP.ManagedStringToIl2Cpp(json);
                System.IntPtr scriptableObjectPtr = (System.IntPtr)typeof(T).GetMethod("get_Pointer").Invoke(scriptableObject, null);
                System.IntPtr typePtr = Il2CppType.Of<T>().Pointer;
                var newPointer = _iCallFromJsonInternal.Invoke(jsonPtr, scriptableObjectPtr, typePtr);
                var newSO = new Il2CppObjectBase(newPointer).TryCast<T>();

                return newSO;
            }

            private delegate System.IntPtr Delegate_ToJsonInternal(System.IntPtr json, bool prettyPrint);
            private static Delegate_ToJsonInternal _iCallToJsonInternal;

            public static string ToJsonInternal(Il2CppObjectBase obj, bool prettyPrint)
            {
                _iCallToJsonInternal ??= IL2CPP.ResolveICall<Delegate_ToJsonInternal>("UnityEngine.JsonUtility::ToJsonInternal");
                System.IntPtr scriptableObjectPtr = (System.IntPtr)typeof(MurderMO).GetMethod("get_Pointer").Invoke(obj, null);

                var newPointer = _iCallToJsonInternal.Invoke(scriptableObjectPtr, prettyPrint);
                var newStr = IL2CPP.Il2CppStringToManaged(newPointer);

                return newStr;
            }
        }
    }
}
