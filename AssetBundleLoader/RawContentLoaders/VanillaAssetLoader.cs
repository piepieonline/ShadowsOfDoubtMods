using System;
using System.Linq;
using System.Reflection;

using UnityEngine;

using Il2CppInterop.Runtime.InteropTypes;

using UniverseLib;

namespace AssetBundleLoader
{
    // Resolve an asset the game already ships, by walking to it from a ScriptableObject that holds it
    public static class VanillaAssetLoader
    {
        public static UnityEngine.Object Resolve(string reference, string source)
        {
            var (holder, memberPath) = FindHolder(reference, source);
            if (holder == null) return null;

            if (memberPath.Length == 0)
            {
                BundleLoader.PluginLogger.LogError($"{source} failed to load, '{reference}' names a preset without a field to take an asset from (use REF: to reference the preset itself)");
                return null;
            }

            object current = Walk(holder, memberPath, reference, source);
            if (current == null) return null;

            var asset = current as UnityEngine.Object ?? (current as Il2CppObjectBase)?.TryCast<UnityEngine.Object>();
            if (asset == null)
            {
                BundleLoader.PluginLogger.LogError($"{source} failed to load, '{reference}' is a {current.GetActualType().Name}, which isn't an asset that can be referenced");
                return null;
            }

            return asset;
        }

        static (ScriptableObject Holder, string MemberPath) FindHolder(string reference, string source)
        {
            int separator = reference.IndexOf('|');
            if (separator < 0)
            {
                BundleLoader.PluginLogger.LogError($"{source} failed to load, '{reference}' isn't a reference of the form VANILLA:PresetType|PresetName.field");
                return (null, null);
            }

            string presetType = reference.Substring(0, separator);
            string[] segments = reference.Substring(separator + 1).Split('.');

            // A preset name can hold dots of its own, so the longest run of segments that names one wins
            for (int count = segments.Length; count > 0; count--)
            {
                string key = presetType + "|" + string.Join(".", segments.Take(count));
                if (JsonLoader.ScriptableObjectIDMap.TryGetValue(key, out var entry))
                {
                    return (entry.scriptableObject, string.Join(".", segments.Skip(count)));
                }
            }

            BundleLoader.PluginLogger.LogError($"{source} failed to load, the preset it takes an asset from ({presetType}|{segments[0]}) doesn't exist!");
            return (null, null);
        }

        static object Walk(object current, string memberPath, string reference, string source)
        {
            foreach (var component in memberPath.Split('.'))
            {
                string fieldName = component;
                int index = -1;

                if (component.EndsWith("]"))
                {
                    var splitComponent = component.Split('[');
                    fieldName = splitComponent[0];
                    if (splitComponent.Length != 2 || !Int32.TryParse(splitComponent[1].TrimEnd(']'), out index))
                    {
                        BundleLoader.PluginLogger.LogError($"{source} failed to load, '{component}' of '{reference}' isn't a field with an index");
                        return null;
                    }
                }

                var currentType = current.GetActualType();
                var field = currentType.GetProperty(fieldName);
                if (field == null)
                {
                    BundleLoader.PluginLogger.LogError($"{source} failed to load, '{reference}' asks for '{fieldName}', which {currentType.Name} doesn't have");
                    return null;
                }

                current = field.GetValue(Typed(current));
                if (current == null)
                {
                    BundleLoader.PluginLogger.LogError($"{source} failed to load, '{fieldName}' of '{reference}' is empty in the game's own data");
                    return null;
                }

                if (index > -1)
                {
                    current = ElementAt(current, index, fieldName, reference, source);
                    if (current == null) return null;
                }
            }

            return current;
        }

        static object ElementAt(object container, int index, string fieldName, string reference, string source)
        {
            if (container is Array array)
            {
                if (index < array.Length) return array.GetValue(index);
            }
            else
            {
                object typed = Typed(container);
                var getItem = typed.GetType().GetMethod("get_Item", new[] { typeof(int) })
                    ?? typed.GetActualType().GetMethod("get_Item", new[] { typeof(int) });

                if (getItem == null)
                {
                    BundleLoader.PluginLogger.LogError($"{source} failed to load, '{fieldName}' of '{reference}' isn't a list that can be indexed");
                    return null;
                }

                try
                {
                    return getItem.Invoke(typed, new object[] { index });
                }
                catch (TargetInvocationException)
                {
                    // Falls through to the out of range error below
                }
            }

            BundleLoader.PluginLogger.LogError($"{source} failed to load, '{fieldName}' of '{reference}' has no entry {index}");
            return null;
        }

        // Reflection needs an object as its own type, not the base type the interop wrapper handed us
        static object Typed(object obj)
        {
            return obj is Il2CppObjectBase ? obj.TryCast(obj.GetActualType()) ?? obj : obj;
        }
    }
}
