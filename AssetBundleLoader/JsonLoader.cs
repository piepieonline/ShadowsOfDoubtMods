using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Reflection;
using System.Collections.Generic;

using UnityEngine;

using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

using UniverseLib;

namespace AssetBundleLoader
{
    public static class JsonLoader
    {
        public static Dictionary<string, (ScriptableObject scriptableObject, string fileId)> ScriptableObjectIDMap = new System.Collections.Generic.Dictionary<string, (ScriptableObject scriptableObject, string fileId)>();
        
        private static void SerializeTypes(ref Dictionary<string, Dictionary<string, string>> dict, Type soType)
        {
            if (soType.FullName == null || !soType.IsSubclassOf(typeof(ScriptableObject))) return;
            dict[soType.FullName] = new Dictionary<string, string>();
            foreach (var field in soType.GetProperties())
            {
                if (field.PropertyType.IsValueType)
                {
                    dict[soType.FullName][field.Name] = field.PropertyType.FullName;
                }
                else if (field.PropertyType.IsArray)
                {
                    SerializeTypes(ref dict, field.PropertyType.GetElementType());
                    dict[soType.FullName][field.Name] = field.PropertyType.GetElementType().FullName;
                }
                else if (field.PropertyType.IsGenericType)
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
            var newSOJSON = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(json);

            var fileName = newSOJSON.Value<string>("name");
            var fileType = newSOJSON.Value<string>("fileType");

            /*
            // Doesn't work, use reflection
            string replaced = Regex.Replace(json, $"\"REF:{fileType}\\|{fileName}\"", """{ "fileID": 11400000}""");
            BundleLoader.PluginLogger.LogInfo(replaced);
            newSOJSON = NewtonsoftJson.JToken_Parse(replaced);
            */

            newSOJSON.SelectToken("fileType").Replace(null);

            string copyFrom = newSOJSON.Value<string>("copyFrom");
            if (copyFrom != null)
            {
                newSOJSON.SelectToken("copyFrom").Replace(null);
            }

            var newSOJSONWithRefs = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(newSOJSON.ToString());

            // Replace references to existing (or created before us) ScriptableObjets
            try
            {
                ExtractAndReplaceTokens(JsonExtensions.FindTokensByValueMatch(newSOJSON, new System.Text.RegularExpressions.Regex("^REF")), $"{fileType}|{fileName}");
            }
            catch (KeyNotFoundException)
            {
                BundleLoader.PluginLogger.LogError($"{fileName} failed to load, missing reference.");
                throw;
            }

            ScriptableObject newSO;

            // Shortcut, so we don't have to create everything from scratch
            if (copyFrom != null && copyFrom.Contains("|"))
            {
                newSO = ScriptableObject.Instantiate(ScriptableObjectIDMap[copyFrom.Replace("REF:", "").Trim()].scriptableObject);
            }
            else
            {
                newSO = ScriptableObject.CreateInstance(fileType);
            }

            // Use the restored FromJson method to parse the completed JSON files
            newSO = RestoredJsonUtility.FromJsonInternal(newSOJSON.ToString(), newSO);
            newSO.name = newSOJSON.SelectToken("name").ToString();

            // Using the original json and the created SO, fix self-references
            // TODO: Set all custom references with this, not just self? Would reduce the need for the fileOrder to only being required for copyFrom
            FixSelfReferences(newSO, newSOJSONWithRefs);

            // Cache the ID of this new object, so following objects can refer to it
            string key = fileType + "|" + newSO.name;
            var arr = new Il2CppSystem.Collections.Generic.List<ScriptableObject>();
            arr.Add(newSO);
            string value = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(JsonUtilityArrays.ToJson(arr.ToArray()))
                .SelectToken("Items[0]").ToString()
            ;

            ScriptableObjectIDMap.Add(key, (newSO, value));

            return newSO;
        }

        private static void ExtractAndReplace(dynamic json, string token, string typeAndNameKey)
        {
            ExtractAndReplaceTokens(json.SelectTokens(token).ToList(), typeAndNameKey);
        }

        private static void ExtractAndReplaceTokens(List<dynamic> tokens, string typeAndNameKey)
        {
            foreach (var token in tokens)
            {
                var tokenType = NewtonsoftExtensions.NewtonsoftJson.JToken_Type(token);
                if (tokenType == NewtonsoftExtensions.e_JToken_Type.Array)
                {
                    var newArr = new Il2CppSystem.Collections.Generic.List<dynamic>();

                    foreach (var item in token.Children<dynamic>())
                    {
                        var tokenValue = item.ToString().Replace("REF:", "");
                        if (tokenValue != typeAndNameKey)
                        {
                            if (ScriptableObjectIDMap.ContainsKey(tokenValue))
                            {
                                newArr.Add(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(ScriptableObjectIDMap[tokenValue].Item2));
                            }
                            else
                            {
                                BundleLoader.PluginLogger.LogError($"{typeAndNameKey} failed to load, {tokenValue} doesn't exist! (Check your fileOrder in your manifest)");
                            }
                        }
                    }

                    token.Replace(NewtonsoftExtensions.NewtonsoftJson.JArray_FromObject(newArr));
                }
                else
                {
                    var tokenValue = token.ToString().Replace("REF:", "");

                    if (tokenValue == typeAndNameKey)
                    {
                        token.Replace(null);
                    }
                    else
                    {
                        if (ScriptableObjectIDMap.ContainsKey(tokenValue))
                        {
                            token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(ScriptableObjectIDMap[tokenValue].Item2));
                        }
                        else
                        {
                            BundleLoader.PluginLogger.LogError($"{typeAndNameKey} failed to load, {tokenValue} doesn't exist! (Check your fileOrder in your manifest)");
                        }
                    }
                }
            }
        }

        private static void FixSelfReferences(ScriptableObject so, dynamic json)
        {
            string selfReference = "REF:" + so.GetActualType() + "|" + so.name;

            // Any reference to ourselves in the format "REF:FileType|FileName"
            foreach (var self in (json.Descendants() as IEnumerable<dynamic>)
                     .Where(val => val.GetType().FullName == "Newtonsoft.Json.Linq.JValue")
                     .Where(val => val.ToString() == selfReference)
                    .ToList())
            {
                List<string> path = new List<string>();

                // Split the path (doesn't handle arrays, will be done later)
                string[] pathComponents = self.Path.Split(".");

                // Loop through the path, keep a reference to the parent FieldInfo and object, and index if required
                object parentObj = null;
                PropertyInfo parentField = null;
                object currentObj = so;
                int lastIndex = -1;
                foreach (var comp in pathComponents)
                {
                    if (comp.IndexOf("[") > -1)
                    {
                        var splitComp = comp.Split("[");
                        int index = Int32.Parse(splitComp[1].Split("]")[0]);

                        try
                        {
                            parentField = currentObj.GetActualType().GetProperty(splitComp[0]);
                            if (parentField == null)
                                throw new NullReferenceException();
                        }
                        catch
                        {
                            BundleLoader.PluginLogger.LogError($"Unknown field '{splitComp[0]}' on type '{currentObj.GetActualType().FullName}'");
                        }

                        if (parentField.PropertyType.IsArray)
                        {
                            var objArray = (object[])(currentObj.GetActualType().GetProperty(splitComp[0]).GetValue(currentObj));

                            lastIndex = index;
                            parentObj = objArray;
                            currentObj = objArray[index];
                        }
                        else
                        {
                            dynamic preset = currentObj.TryCast(currentObj.GetActualType());
                            dynamic objArray = (currentObj.GetActualType().GetProperty(splitComp[0]).GetValue(preset));

                            lastIndex = index;
                            parentObj = objArray;
                            currentObj = objArray[index];
                        }
                    }
                    else
                    {
                        parentField = currentObj.GetActualType().GetProperty(comp);
                        parentObj = currentObj;

                        if (pathComponents.Length > 1)
                            currentObj = currentObj.GetActualType().GetProperty(comp).GetValue(currentObj);
                    }
                }

                // Overwrite the value with our own scriptable object
                if (parentField.PropertyType.IsArray)
                {
                    ((System.Array)parentObj).SetValue(so.TryCast(so.GetActualType()), lastIndex);
                }
                else if (parentField.PropertyType.Name.StartsWith("List"))
                {
                    parentObj.GetActualType().GetMethod("set_Item").Invoke(parentObj, new object[] { lastIndex, so.TryCast(so.GetActualType()) });
                }
                else
                {
                    parentField.SetValue(parentObj, so.TryCast(so.GetActualType()));
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
                var tokenType = NewtonsoftExtensions.NewtonsoftJson.JToken_Type(containerToken);

                if (tokenType == NewtonsoftExtensions.e_JToken_Type.Object)
                {
                    foreach (dynamic child in containerToken)
                    {
                        FindTokensByValueMatch(child.Value, value, ref matches);
                    }
                }
                else if (tokenType == NewtonsoftExtensions.e_JToken_Type.Array)
                {
                    foreach (dynamic child in containerToken)
                    {
                        FindTokensByValueMatch(child, value, ref matches);
                    }
                }
                else if (tokenType == NewtonsoftExtensions.e_JToken_Type.String)
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
            ConstructorInfo m_JObject_Ctr;
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
                m_JObject_Ctr = t_JObject.GetConstructor([]);
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
                    if (_instance == null)
                        _instance = new NewtonsoftExtensions();
                    return _instance;
                }
            }

            public dynamic JToken_Ctr()
            {
                return _instance.m_JObject_Ctr.Invoke(null);
            }

            public dynamic JToken_Parse(string json)
            {
                return _instance.m_JToken_Parse.Invoke(null, [json]);
            }

            public e_JToken_Type JToken_Type(dynamic jtoken)
            {
                if (!_instance.m_Type_Getter.ContainsKey(jtoken.GetType().FullName))
                    _instance.m_Type_Getter[jtoken.GetType().FullName] = jtoken.GetType().GetProperty("Type").GetMethod;

                dynamic result = _instance.m_Type_Getter[jtoken.GetType().FullName].Invoke(jtoken, null);

                int intResult = (int)result;
                return (e_JToken_Type)intResult;
            }

            public dynamic JArray_FromObject(object obj)
            {
                return _instance.m_JArray_FromObject.Invoke(null, [obj]);
            }

            public dynamic JObject_FromObject(object obj)
            {
                return _instance.m_JObject_FromObject.Invoke(null, [obj]);
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