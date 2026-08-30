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
    /// A file that copies from, or patches, an object that hasn't loaded yet, so whoever is loading it can come back to it
    public class MissingDependencyException : Exception
    {
        public string Key { get; }

        public MissingDependencyException(string fileName, string key)
            : base($"{fileName} needs {key}, which hasn't loaded yet")
        {
            Key = key;
        }
    }

    /// A file the loader can't act on at all, so the rest of its mod's files should still get their turn
    public class InvalidContentException : Exception
    {
        public InvalidContentException(string fileName, string problem)
            : base($"{fileName} can't be loaded, {problem}")
        {
        }
    }

    public static class JsonLoader
    {
        public static Dictionary<string, (ScriptableObject scriptableObject, string fileId)> ScriptableObjectIDMap =
            new System.Collections.Generic.Dictionary<string, (ScriptableObject scriptableObject, string fileId)>();

        // References to objects that hadn't loaded yet, assigned once every mod has had its turn to load
        private static List<(ScriptableObject Target, string JsonPath, string Key)> pendingReferences =
            new List<(ScriptableObject Target, string JsonPath, string Key)>();

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

        [Obsolete("Use the overload taking the folder the mod's content was loaded from, so that file based references can be resolved")]
        public static ScriptableObject LoadFileToGame(string json)
        {
            return LoadFileToGame(json, true, null);
        }
        
        [Obsolete("Use the overload taking the folder the mod's content was loaded from, so that file based references can be resolved")]
        public static ScriptableObject LoadFileToGame(string json, bool isNewObject)
        {
            return LoadFileToGame(json, isNewObject, null);
        }
        
        public static ScriptableObject LoadFileToGame(string json, bool isNewObject, string modFolderPath)
        {
            var newSOJSON = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(json);

            // Explicitly typed, otherwise anything they are passed to binds dynamically, and extension methods and tuple names don't survive that
            string fileName = newSOJSON.Value<string>("name");
            string fileType = newSOJSON.Value<string>("fileType");

            /*
            // Doesn't work, use reflection
            string replaced = Regex.Replace(json, $"\"REF:{fileType}\\|{fileName}\"", """{ "fileID": 11400000}""");
            BundleLoader.PluginLogger.LogInfo(replaced);
            newSOJSON = NewtonsoftJson.JToken_Parse(replaced);
            */

            if (fileName == null)
            {
                throw new InvalidContentException("<unnamed>", "it has no 'name' to create or look up");
            }

            if (fileType == null)
            {
                throw new InvalidContentException(fileName, "it has no 'fileType' to create or look up");
            }

            newSOJSON.SelectToken("fileType").Replace(null);

            string copyFrom = newSOJSON.Value<string>("copyFrom");
            if (copyFrom != null)
            {
                newSOJSON.SelectToken("copyFrom").Replace(null);
            }

            var patches = newSOJSON.SelectToken("patches");
            if (patches != null)
            {
                if (isNewObject)
                {
                    throw new InvalidContentException(fileName, "'patches' can only be applied to an object that already exists");
                }

                newSOJSON = BuildPatchedDocument(patches, fileType + "|" + fileName, fileName);
            }

            // Assets built at runtime have no file ID, so they are blanked out here and assigned once Unity has deserialised
            List<dynamic> floorTokens = JsonExtensions.FindTokensByValueMatch(newSOJSON, new System.Text.RegularExpressions.Regex("^FLOOR:"));
            var deferredAssets = ExtractAndReplaceFloorTokens(floorTokens, modFolderPath, fileName);

            List<dynamic> bundleTokens = JsonExtensions.FindTokensByValueMatch(newSOJSON, new System.Text.RegularExpressions.Regex("^BUNDLE:"));
            deferredAssets.AddRange(ExtractAndReplaceBundleTokens(bundleTokens, modFolderPath, fileName));

            List<dynamic> prefabTokens = JsonExtensions.FindTokensByValueMatch(newSOJSON, new System.Text.RegularExpressions.Regex("^PREFAB:"));
            deferredAssets.AddRange(ExtractAndReplacePrefabTokens(prefabTokens, modFolderPath, fileName));

            List<dynamic> textureTokens = JsonExtensions.FindTokensByValueMatch(newSOJSON, new System.Text.RegularExpressions.Regex("^TEXTURE:"));
            deferredAssets.AddRange(ExtractAndReplaceTextureTokens(textureTokens, modFolderPath, fileName));

            List<dynamic> vanillaTokens = JsonExtensions.FindTokensByValueMatch(newSOJSON, new System.Text.RegularExpressions.Regex("^VANILLA:"));
            deferredAssets.AddRange(ExtractAndReplaceVanillaTokens(vanillaTokens, fileName));

            var newSOJSONWithRefs = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(newSOJSON.ToString());

            // Replace references to existing (or created before us) ScriptableObjets
            List<(string JsonPath, string Key)> unresolvedReferences;
            try
            {
                unresolvedReferences = ExtractAndReplaceTokens(
                    JsonExtensions.FindTokensByValueMatch(newSOJSON, new System.Text.RegularExpressions.Regex("^REF")),
                    $"{fileType}|{fileName}");
            }
            catch (KeyNotFoundException ex)
            {
                BundleLoader.PluginLogger.LogError($"{fileName} failed to load, missing reference ({ex.Message}).");
                throw;
            }

            ScriptableObject newSO;
            string newSOName = fileName;
            string cacheKey = fileType + "|" + newSOName;
                
            if (isNewObject)
            {
                // Shortcut, so we don't have to create everything from scratch
                if (copyFrom != null && copyFrom.Contains("|"))
                {
                    string copyFromKey = copyFrom.Replace("REF:", "").Trim();
                    if (!ScriptableObjectIDMap.ContainsKey(copyFromKey))
                    {
                        throw new MissingDependencyException(fileName, copyFromKey);
                    }

                    newSO = ScriptableObject.Instantiate(ScriptableObjectIDMap[copyFromKey].scriptableObject);
                }
                else
                {
                    newSO = ScriptableObject.CreateInstance(fileType);
                }
            }
            else
            {
                if (!ScriptableObjectIDMap.ContainsKey(cacheKey))
                {
                    throw new MissingDependencyException(fileName, cacheKey);
                }

                newSO = ScriptableObjectIDMap[cacheKey].scriptableObject;
            }

            // Use the restored FromJson method to parse the completed JSON files
            newSO = RestoredJsonUtility.FromJsonInternal(newSOJSON.ToString(), newSO);
            newSO.name = newSOName;

            // Using the original json and the created SO, fix self-references
            FixSelfReferences(newSO, newSOJSONWithRefs);

            foreach (var unresolved in unresolvedReferences)
            {
                pendingReferences.Add((newSO, unresolved.JsonPath, unresolved.Key));
            }

            foreach (var deferred in deferredAssets)
            {
                AssignAtJsonPath(newSO, deferred.JsonPath, deferred.Asset);
            }

            BuildingModelLoader.TryPrepareBuildingModel(newSO);

            // Cache the ID of this new object, so following objects can refer to it (if it doesn't already exist)
            if (isNewObject)
            {
                var arr = new Il2CppSystem.Collections.Generic.List<ScriptableObject>();
                arr.Add(newSO);
                string value = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(JsonUtilityArrays.ToJson(arr.ToArray()))
                        .SelectToken("Items[0]").ToString()
                    ;

                ScriptableObjectIDMap.Add(cacheKey, (newSO, value));
            }

            return newSO;
        }

        /// The object as it stands now is what the operations are applied to, so patches stack rather than overwrite each other
        private static dynamic BuildPatchedDocument(dynamic patches, string cacheKey, string fileName)
        {
            if (!ScriptableObjectIDMap.ContainsKey(cacheKey))
            {
                throw new MissingDependencyException(fileName, cacheKey);
            }

            var target = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(
                RestoredJsonUtility.ToJsonInternal(ScriptableObjectIDMap[cacheKey].scriptableObject, false));

            JSONPatch.ApplyOperations(target, patches);

            return target;
        }

        /// Anything that can't be resolved yet is blanked out and returned, so it can be assigned once every mod has loaded
        private static List<(string JsonPath, string Key)> ExtractAndReplaceTokens(List<dynamic> tokens, string typeAndNameKey)
        {
            var unresolved = new List<(string JsonPath, string Key)>();

            foreach (var token in tokens)
            {
                var tokenType = NewtonsoftExtensions.NewtonsoftJson.JToken_Type(token);
                if (tokenType == NewtonsoftExtensions.e_JToken_Type.Array)
                {
                    var newArr = new Il2CppSystem.Collections.Generic.List<dynamic>();

                    foreach (var item in token.Children<dynamic>())
                    {
                        string itemPath = item.Path.ToString();
                        var tokenValue = item.ToString().Replace("REF:", "");

                        if (tokenValue != typeAndNameKey && ScriptableObjectIDMap.ContainsKey(tokenValue))
                        {
                            newArr.Add(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(ScriptableObjectIDMap[tokenValue].Item2));
                            continue;
                        }

                        // The slot is kept either way, so the indices the fixups were recorded against still line up
                        newArr.Add(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse("{\"m_FileID\":0,\"m_PathID\":0}"));

                        if (tokenValue != typeAndNameKey)
                        {
                            unresolved.Add((itemPath, tokenValue));
                        }
                    }

                    token.Replace(NewtonsoftExtensions.NewtonsoftJson.JArray_FromObject(newArr));
                }
                else
                {
                    string jsonPath = token.Path.ToString();
                    var tokenValue = token.ToString().Replace("REF:", "");

                    if (tokenValue == typeAndNameKey)
                    {
                        token.Replace(null);
                    }
                    else if (ScriptableObjectIDMap.ContainsKey(tokenValue))
                    {
                        token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(ScriptableObjectIDMap[tokenValue].Item2));
                    }
                    else
                    {
                        token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse("{\"m_FileID\":0,\"m_PathID\":0}"));
                        unresolved.Add((jsonPath, tokenValue));
                    }
                }
            }

            return unresolved;
        }

        /// Every mod has loaded, so anything still missing is missing for good
        public static void ResolvePendingReferences()
        {
            foreach (var pending in pendingReferences)
            {
                if (ScriptableObjectIDMap.TryGetValue(pending.Key, out var reference))
                {
                    AssignAtJsonPath(pending.Target, pending.JsonPath, reference.scriptableObject.TryCast(reference.scriptableObject.GetActualType()));
                }
                else
                {
                    BundleLoader.PluginLogger.LogError($"{pending.Target.name} is missing its reference to {pending.Key}, which no mod loaded!");
                }
            }

            pendingReferences.Clear();
        }

        private static List<(string JsonPath, UnityEngine.Object Asset)> ExtractAndReplaceFloorTokens(List<dynamic> tokens, string modFolderPath, string fileName)
        {
            var deferredAssets = new List<(string JsonPath, UnityEngine.Object Asset)>();

            foreach (var token in tokens)
            {
                string jsonPath = token.Path.ToString();
                string relativePath = token.ToString().Replace("FLOOR:", "").Trim();

                if (modFolderPath == null)
                {
                    BundleLoader.PluginLogger.LogError($"{fileName} failed to load floorplan {relativePath}, the mod loading it didn't provide a folder to search");
                }
                else
                {
                    var floorplan = FloorDataLoader.LoadFloorplan(modFolderPath, relativePath);
                    if (floorplan != null)
                    {
                        deferredAssets.Add((jsonPath, floorplan));
                    }
                }

                token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse("{\"m_FileID\":0,\"m_PathID\":0}"));
            }

            return deferredAssets;
        }

        private static List<(string JsonPath, UnityEngine.Object Asset)> ExtractAndReplaceTextureTokens(List<dynamic> tokens, string modFolderPath, string fileName)
        {
            var deferredAssets = new List<(string JsonPath, UnityEngine.Object Asset)>();

            foreach (var token in tokens)
            {
                string jsonPath = token.Path.ToString();
                string relativePath = token.ToString().Replace("TEXTURE:", "").Trim();

                if (modFolderPath == null)
                {
                    BundleLoader.PluginLogger.LogError($"{fileName} failed to load texture {relativePath}, the mod loading it didn't provide a folder to search");
                }
                else
                {
                    var texture = Texture2DLoader.LoadTextureFromMod(modFolderPath, relativePath);
                    if (texture != null)
                    {
                        deferredAssets.Add((jsonPath, texture));
                    }
                }

                token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse("{\"m_FileID\":0,\"m_PathID\":0}"));
            }

            return deferredAssets;
        }

        private static List<(string JsonPath, UnityEngine.Object Asset)> ExtractAndReplacePrefabTokens(List<dynamic> tokens, string modFolderPath, string fileName)
        {
            var deferredAssets = new List<(string JsonPath, UnityEngine.Object Asset)>();

            foreach (var token in tokens)
            {
                string jsonPath = token.Path.ToString();
                string relativePath = token.ToString().Replace("PREFAB:", "").Trim();

                if (modFolderPath == null)
                {
                    BundleLoader.PluginLogger.LogError($"{fileName} failed to load prefab {relativePath}, the mod loading it didn't provide a folder to search");
                }
                else
                {
                    var prefab = PrefabLoader.LoadPrefabFromJSON(modFolderPath, relativePath);
                    // TODO: Create one if null so roofs are shown?
                    if (prefab != null)
                    {
                        deferredAssets.Add((jsonPath, prefab));
                    }
                }

                token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse("{\"m_FileID\":0,\"m_PathID\":0}"));
            }

            return deferredAssets;
        }

        private static List<(string JsonPath, UnityEngine.Object Asset)> ExtractAndReplaceVanillaTokens(List<dynamic> tokens, string fileName)
        {
            var deferredAssets = new List<(string JsonPath, UnityEngine.Object Asset)>();

            foreach (var token in tokens)
            {
                string jsonPath = token.Path.ToString();
                string assetReference = token.ToString().Replace("VANILLA:", "").Trim();

                var asset = VanillaAssetLoader.Resolve(assetReference, fileName);
                if (asset != null)
                {
                    deferredAssets.Add((jsonPath, asset));
                }

                token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse("{\"m_FileID\":0,\"m_PathID\":0}"));
            }

            return deferredAssets;
        }

        private static List<(string JsonPath, UnityEngine.Object Asset)> ExtractAndReplaceBundleTokens(List<dynamic> tokens, string modFolderPath, string fileName)
        {
            var deferredAssets = new List<(string JsonPath, UnityEngine.Object Asset)>();

            foreach (var token in tokens)
            {
                string jsonPath = token.Path.ToString();
                string assetReference = token.ToString().Replace("BUNDLE:", "").Trim();
                var bundleAndAsset = assetReference.Split('|');

                if (modFolderPath == null)
                {
                    BundleLoader.PluginLogger.LogError($"{fileName} failed to load asset {assetReference}, the mod loading it didn't provide a folder to search");
                }
                else if (bundleAndAsset.Length != 2)
                {
                    BundleLoader.PluginLogger.LogError($"{fileName} failed to load asset '{assetReference}', references are in the form BUNDLE:bundleName|assetName");
                }
                else
                {
                    var asset = BundleAssetLoader.LoadAsset(modFolderPath, bundleAndAsset[0], bundleAndAsset[1]);
                    if (asset != null)
                    {
                        deferredAssets.Add((jsonPath, asset));
                    }
                }

                token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse("{\"m_FileID\":0,\"m_PathID\":0}"));
            }

            return deferredAssets;
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
                // Explicitly typed, otherwise the call below binds dynamically
                string selfPath = self.Path.ToString();
                AssignAtJsonPath(so, selfPath, so.TryCast(so.GetActualType()));
            }
        }

        private static void AssignAtJsonPath(ScriptableObject so, string jsonPath, object value)
        {
            // Split the path (doesn't handle arrays, will be done later)
            string[] pathComponents = jsonPath.Split(".");

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

                        // Deserialising can leave the list shorter than the path expects, so grow it rather than fail
                        GrowListToFit((object)objArray, index);

                        lastIndex = index;
                        parentObj = objArray;
                        currentObj = objArray[index];
                    }
                }
                else
                {
                    // Reflection needs the object as its own type, not the ScriptableObject it arrived as
                    var typedObj = currentObj.TryCast(currentObj.GetActualType());

                    parentField = currentObj.GetActualType().GetProperty(comp);
                    parentObj = typedObj;

                    if (pathComponents.Length > 1)
                        currentObj = parentField.GetValue(typedObj);
                }
            }

            // Bundle assets are loaded as UnityEngine.Object, so cast them to the type the field holds
            Type fieldType = parentField.PropertyType.IsArray ? parentField.PropertyType.GetElementType()
                : parentField.PropertyType.Name.StartsWith("List") ? parentField.PropertyType.GetGenericArguments()[0]
                : parentField.PropertyType;

            if (value != null && !fieldType.IsInstanceOfType(value))
            {
                value = value.TryCast(fieldType);
            }

            if (parentField.PropertyType.IsArray)
            {
                ((System.Array)parentObj).SetValue(value, lastIndex);
            }
            else if (parentField.PropertyType.Name.StartsWith("List"))
            {
                parentObj.GetActualType().GetMethod("set_Item").Invoke(parentObj, new object[] { lastIndex, value });
            }
            else
            {
                parentField.SetValue(parentObj, value);
            }
        }

        private static void GrowListToFit(object list, int index)
        {
            var listType = list.GetActualType();
            var count = (int)listType.GetProperty("Count").GetValue(list);
            if (count > index) return;

            var add = listType.GetMethod("Add");
            while (count <= index)
            {
                add.Invoke(list, new object[] { null });
                count++;
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
            MethodInfo m_JToken_DeepEquals;
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
                m_JToken_DeepEquals = t_JToken.GetMethod("DeepEquals", [t_JToken, t_JToken]);
                m_Type_Getter = new Dictionary<string, MethodInfo>();

                t_JObject = newtonsoftJson.GetType("Newtonsoft.Json.Linq.JObject");
                m_JObject_Ctr = t_JObject.GetConstructor([]);
                m_JObject_Parse = t_JObject.GetMethod("Parse", [typeof(string)]);
                m_JObject_FromObject = t_JObject.GetMethod("FromObject", [typeof(object)]);
                m_JObject_Type = t_JObject.GetProperty("Type");

                t_JArray = newtonsoftJson.GetType("Newtonsoft.Json.Linq.JArray");
                m_JArray_FromObject = t_JArray.GetMethod("FromObject", [typeof(object)]);
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

            public bool JToken_DeepEquals(dynamic left, dynamic right)
            {
                return (bool)_instance.m_JToken_DeepEquals.Invoke(null, [left, right]);
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
                var newPointer = _iCallToJsonInternal.Invoke(obj.Pointer, prettyPrint);
                var newStr = IL2CPP.Il2CppStringToManaged(newPointer);

                return newStr;
            }
        }
    }
}