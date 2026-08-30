using System.Collections.Generic;

using UnityEngine;

using JsonWrapper = AssetBundleLoader.JsonLoader.NewtonsoftExtensions;

namespace AssetBundleLoader
{
    // Readers for the parts of a .sodprefab.json that are optional, so a missing key reads as a default.
    public static class PrefabJson
    {
        public static dynamic Field(dynamic json, string name)
        {
            if (json == null) return null;

            var field = json[name];
            if (field == null) return null;

            return JsonWrapper.NewtonsoftJson.JToken_Type(field) == JsonWrapper.e_JToken_Type.Null ? null : field;
        }

        public static string Text(dynamic json, string name)
        {
            var field = Field(json, name);
            return field == null ? null : field.ToString();
        }

        public static bool Bool(dynamic json, string name, bool fallback)
        {
            return Field(json, name) == null ? fallback : json.Value<bool>(name);
        }

        public static IEnumerable<dynamic> Array(dynamic json, string name)
        {
            var field = Field(json, name);
            if (field == null) yield break;

            foreach (dynamic item in field)
            {
                yield return item;
            }
        }

        /// The keys of a JSON object, to be read back out of it with Value&lt;T&gt;
        public static IEnumerable<string> Names(dynamic json, string name)
        {
            var field = Field(json, name);
            if (field == null) yield break;

            foreach (dynamic property in field)
            {
                yield return property.Name.ToString();
            }
        }

        public static Vector3 ReadVector3(dynamic json, string name, Vector3 fallback)
        {
            var field = Field(json, name);
            if (field == null) return fallback;

            return new Vector3(field.Value<float>(0), field.Value<float>(1), field.Value<float>(2));
        }

        public static Color ReadColor(dynamic field, Color fallback)
        {
            if (field == null) return fallback;

            float alpha = field.Count > 3 ? field.Value<float>(3) : 1f;
            return new Color(field.Value<float>(0), field.Value<float>(1), field.Value<float>(2), alpha);
        }
    }
}
