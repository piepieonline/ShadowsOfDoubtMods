using System;
using System.Linq;

using static AssetBundleLoader.JsonLoader.NewtonsoftExtensions;

namespace DDSLoader
{
    public class JSONPatch
    {
        public static dynamic ApplyPatch(string targetJSON, string patchJSON)
        {
            dynamic target = NewtonsoftJson.JToken_Parse(targetJSON);
            dynamic patch = NewtonsoftJson.JToken_Parse(patchJSON);

            foreach (var operation in patch)
            {
                var op = operation["op"]?.ToString();
                var path = operation["path"]?.ToString();
                var from = operation["from"]?.ToString();
                var value = operation["value"];

                switch (op)
                {
                    case "add":
                        SetValue(target, path, value, createIfMissing: true);
                        break;
                    case "replace":
                        SetValue(target, path, value, createIfMissing: false);
                        break;
                    case "remove":
                        RemoveValue(target, path);
                        break;
                    /*
                    case "move":
                        var movedValue = GetValue(target, from);
                        // RemoveValue(target, from);
                        SetValue(target, path, movedValue, createIfMissing: true);
                        break;
                    */
                    default:
                        throw new NotSupportedException($"Unsupported operation: {op}");
                }
            }

            return target;
        }

        private static string[] SplitPath(string path) =>
            path.Trim('/').Split('/')
                .Select(p => p.Replace("~1", "/").Replace("~0", "~"))
                .ToArray();

        /*
        private static dynamic GetValue(dynamic root, string path)
        {
            var tokens = SplitPath(path);
            dynamic current = root;

            int arrIndex = -1;

            foreach (var token in tokens)
            {
                if (current.Type == JTokenType.Object && current.ContainsKey(token))
                    current = current[token];
                else if (current.Type == JTokenType.Array && int.TryParse(token, out arrIndex) && arrIndex < current.Count)
                    current = current[arrIndex];
                else
                    throw new Exception($"Path not found: {path}");
            }

            return current;
        }
        */

        private static void SetValue(dynamic root, string path, dynamic value, bool createIfMissing)
        {
            var tokens = SplitPath(path);
            dynamic current = root;

            int arrIndex = -1;

            for (int i = 0; i < tokens.Length - 1; i++)
            {
                string key = tokens[i];
                
                if (NewtonsoftJson.JToken_Type(current) == e_JToken_Type.Object)
                {
                    if (!current.ContainsKey(key))
                    {
                        if (createIfMissing)
                            current[key] = NewtonsoftJson.JToken_Ctr();
                        else
                            throw new Exception($"Path not found: {path}");
                    }

                    current = current[key];
                }
                else if (NewtonsoftJson.JToken_Type(current) == e_JToken_Type.Array && int.TryParse(key, out arrIndex))
                {
                    current = current[arrIndex];
                }
                else
                {
                    throw new Exception($"Invalid path segment: {key}");
                }
            }

            string lastKey = tokens.Last();

            if (NewtonsoftJson.JToken_Type(current) == e_JToken_Type.Object)
            {
                current[lastKey] = value;
            }
            else if (NewtonsoftJson.JToken_Type(current) == e_JToken_Type.Array && int.TryParse(lastKey, out arrIndex))
            {
                if (arrIndex >= 0 && arrIndex <= current.Count)
                {
                    if (arrIndex == current.Count)
                        current.Add(value);
                    else
                        current[arrIndex] = value;
                }
                else
                {
                    throw new Exception($"Invalid array index: {lastKey}");
                }
            }
            else
            {
                throw new Exception($"Cannot set value at path: {path}");
            }
        }

        private static void RemoveValue(dynamic root, string path)
        {
            var tokens = SplitPath(path);
            dynamic current = root;

            for (int i = 0; i < tokens.Length - 1; i++)
            {
                string key = tokens[i];
                current = current[key] ?? throw new Exception($"Path not found: {path}");
            }

            string lastKey = tokens.Last();

            int arrIndex = -1;
            if (NewtonsoftJson.JToken_Type(current) == e_JToken_Type.Object)
            {
                current.Remove(lastKey);
            }
            else if (NewtonsoftJson.JToken_Type(current) == e_JToken_Type.Array && int.TryParse(lastKey, out arrIndex) && arrIndex < current.Count)
            {
                current.RemoveAt(arrIndex);
            }
            else
            {
                throw new Exception($"Invalid remove path: {path}");
            }
        }
    }
}
