using System;
using System.Linq;

using static AssetBundleLoader.JsonLoader.NewtonsoftExtensions;

namespace AssetBundleLoader
{
    public class JSONPatch
    {
        public static dynamic ApplyPatch(string targetJSON, string patchJSON)
        {
            dynamic target = NewtonsoftJson.JToken_Parse(targetJSON);
            dynamic operations = NewtonsoftJson.JToken_Parse(patchJSON);

            ApplyOperations(target, operations);

            return target;
        }

        public static void ApplyOperations(dynamic target, dynamic operations)
        {
            foreach (var operation in operations)
            {
                string op = operation["op"]?.ToString();
                string path = operation["path"]?.ToString();
                var value = operation["value"];

                switch (op)
                {
                    case "add":
                        SetValue(target, path, value, isAdd: true);
                        break;
                    case "replace":
                        SetValue(target, path, value, isAdd: false);
                        break;
                    case "remove":
                        RemoveValue(target, path);
                        break;
                    case "test":
                        if (!NewtonsoftJson.JToken_DeepEquals(GetValue(target, path), value))
                            throw new Exception($"Test failed at path: {path}");
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported operation: {op}");
                }
            }
        }

        private static string[] SplitPath(string path) =>
            path.Trim('/').Split('/')
                .Select(p => p.Replace("~1", "/").Replace("~0", "~"))
                .ToArray();

        private static bool IsSelector(string key) =>
            key.Length > 2 && key[0] == '[' && key[key.Length - 1] == ']';

        /// Matches the one array element whose field holds this value, so a path survives the list shifting under it
        private static int SelectorIndex(dynamic array, string key, string path)
        {
            int separator = key.IndexOf('=');
            if (separator < 2)
                throw new Exception($"Selector '{key}' in '{path}' is not in the form [field=value]");

            string field = key.Substring(1, separator - 1);
            string wanted = key.Substring(separator + 1, key.Length - separator - 2);

            int found = -1;
            int count = (int)array.Count;

            for (int i = 0; i < count; i++)
            {
                dynamic element = array[i];

                if (NewtonsoftJson.JToken_Type(element) != e_JToken_Type.Object || !element.ContainsKey(field))
                    continue;

                if (element[field].ToString() != wanted)
                    continue;

                if (found > -1)
                    throw new Exception($"Selector '{key}' in '{path}' matches more than one element");

                found = i;
            }

            if (found < 0)
                throw new Exception($"Selector '{key}' in '{path}' matches nothing");

            return found;
        }

        private static int ArrayIndex(dynamic array, string key, string path)
        {
            if (IsSelector(key))
                return SelectorIndex(array, key, path);

            int arrIndex;
            if (!int.TryParse(key, out arrIndex))
                throw new Exception($"Invalid array index '{key}' in: {path}");

            return arrIndex;
        }

        private static dynamic Step(dynamic current, string key, string path, bool createIfMissing)
        {
            var currentType = NewtonsoftJson.JToken_Type(current);

            if (currentType == e_JToken_Type.Object)
            {
                if (!current.ContainsKey(key))
                {
                    if (!createIfMissing)
                        throw new Exception($"Path not found: {path}");

                    current[key] = NewtonsoftJson.JToken_Ctr();
                }

                return current[key];
            }

            if (currentType == e_JToken_Type.Array)
            {
                int arrIndex = ArrayIndex(current, key, path);

                if (arrIndex < 0 || arrIndex >= (int)current.Count)
                    throw new Exception($"Array index out of range '{key}' in: {path}");

                return current[arrIndex];
            }

            throw new Exception($"Invalid path segment '{key}' in: {path}");
        }

        private static dynamic WalkToParent(dynamic root, string[] tokens, string path, bool createIfMissing)
        {
            dynamic current = root;

            for (int i = 0; i < tokens.Length - 1; i++)
            {
                current = Step(current, tokens[i], path, createIfMissing);
            }

            return current;
        }

        private static dynamic GetValue(dynamic root, string path)
        {
            var tokens = SplitPath(path);

            return Step(WalkToParent(root, tokens, path, false), tokens.Last(), path, false);
        }

        private static void SetValue(dynamic root, string path, dynamic value, bool isAdd)
        {
            var tokens = SplitPath(path);
            dynamic current = WalkToParent(root, tokens, path, isAdd);

            string lastKey = tokens.Last();
            var currentType = NewtonsoftJson.JToken_Type(current);

            if (currentType == e_JToken_Type.Object)
            {
                if (!isAdd && !current.ContainsKey(lastKey))
                    throw new Exception($"Path not found: {path}");

                current[lastKey] = value;
            }
            else if (currentType == e_JToken_Type.Array)
            {
                int count = (int)current.Count;
                int arrIndex = isAdd && lastKey == "-" ? count : ArrayIndex(current, lastKey, path);

                if (arrIndex < 0 || arrIndex > count || (!isAdd && arrIndex == count))
                    throw new Exception($"Array index out of range '{lastKey}' in: {path}");

                if (!isAdd)
                    current[arrIndex] = value;
                else if (arrIndex == count)
                    current.Add(value);
                else
                    current.Insert(arrIndex, value);
            }
            else
            {
                throw new Exception($"Cannot set value at path: {path}");
            }
        }

        private static void RemoveValue(dynamic root, string path)
        {
            var tokens = SplitPath(path);
            dynamic current = WalkToParent(root, tokens, path, false);

            string lastKey = tokens.Last();
            var currentType = NewtonsoftJson.JToken_Type(current);

            if (currentType == e_JToken_Type.Object)
            {
                if (!current.Remove(lastKey))
                    throw new Exception($"Path not found: {path}");
            }
            else if (currentType == e_JToken_Type.Array)
            {
                int arrIndex = ArrayIndex(current, lastKey, path);

                if (arrIndex < 0 || arrIndex >= (int)current.Count)
                    throw new Exception($"Array index out of range '{lastKey}' in: {path}");

                current.RemoveAt(arrIndex);
            }
            else
            {
                throw new Exception($"Cannot remove value at path: {path}");
            }
        }
    }
}
