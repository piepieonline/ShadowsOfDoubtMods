using AssetBundleLoader;
using HarmonyLib;
using Il2CppSystem.IO;
using Microsoft.VisualBasic;
using MoonSharp.Interpreter;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UniverseLib;
using Il2CppType = Il2CppInterop.Runtime.Il2CppType;
using static lzma;

namespace DDSScriptExtensions
{
    internal class DDSScriptExtensionsHooks
    {
        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                DDSScriptExtensionsPlugin.ReloadScriptList();
            }
        }

        [HarmonyPatch(typeof(CityData), "CreateSingletons")]
        internal class CityData_CreateSingletons
        {
            public static void Postfix()
            {
                // Map all types that have a static "Instance" member so we can access them in lua
                // TODO: Some are still null at this point - is it worth moving this later in the load chain?
                foreach(var t in typeof(Toolbox).Assembly.GetTypes())
                {
                    if(t.Namespace == null && !t.ContainsGenericParameters)
                    {
                        var instanceMemberInfo = t.GetMember("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).FirstOrDefault();
                        if (instanceMemberInfo != null)
                        {
                            try
                            {
                                var instanceMember = instanceMemberInfo.GetActualType() == typeof(System.Reflection.FieldInfo) ?
                                    ((System.Reflection.FieldInfo)instanceMemberInfo).GetValue(null) :
                                    ((System.Reflection.PropertyInfo)instanceMemberInfo).GetValue(null);
                                // DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"Mapping Instance member: {t.Name} - {(instanceMember != null ? ToTypedStringExtension.ToTypedString(instanceMember) : "null")}");

                                if ( instanceMember != null )
                                    DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals[t.Name] = UserData.Create(instanceMember);
                            }
                            catch (System.Exception ex)
                            {
                                DDSScriptExtensionsPlugin.PluginLogger.LogError($"Failed Mapping Instance member: {t.Name}");
                                DDSScriptExtensionsPlugin.PluginLogger.LogError(ex);
                            }
                        }
                    }
                }

                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["CSToString"] = (System.Func<string, object>)ToTypedStringExtension.ToTypedString;

                // Per-load scratch space for scripts. The Lua environment is built once at plugin
                // load and shared by every script, so anything a script leaves in Globals would
                // otherwise outlive the city it was computed for. Replacing the table here scopes
                // it to one loaded save.
                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["_cache"] = new Table(DDSScriptExtensionsPlugin.LuaScriptEnvironment);
            }
        }

        [HarmonyPatch(typeof(Strings), nameof(Strings.GetContainedScope))]
        public class Strings_GetContainedScope
        {
            public static bool Prefix(ref DDSScope __result, DDSScope baseScope, DDSScope currentScope, string newScope, object inputObject, ref object outputObject, object additionalObject)
            {
                if (newScope.StartsWith("customscope_")
                    && DDSScriptExtensionsPlugin.LoadedExtensions["scopes"].TryGetValue(currentScope.name, out var scopeScripts)
                    && scopeScripts.TryGetValue(newScope, out var ddsScript))
                {
                    DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["_caller"] = newScope.Substring("customscope_".Length);
                    DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["inputObject"] = inputObject?.TryCast(inputObject.GetActualType());
                    DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["additionalObject"] = additionalObject?.TryCast(additionalObject.GetActualType());

                    if (DDSScriptExtensionsPlugin.DebugEnabled.Value)
                    {
                        try
                        {
                            if (inputObject != null)
                                DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"inputObject is: {inputObject.GetType().Name} - {ToTypedStringExtension.ToTypedString(inputObject)}");
                            else
                                DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"inputObject is null");

                            if (additionalObject != null)
                                DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"additionalObject is: {additionalObject.GetType().Name} - {ToTypedStringExtension.ToTypedString(additionalObject)}");
                            else
                                DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"additionalObject is null");
                        }
                        catch
                        { }
                    }

                    try
                    {
                        // Run the calculation function
                        outputObject = DDSScriptExtensionsPlugin.LuaScriptEnvironment.DoString(ddsScript.script).ToObject();

                        if (DDSScriptExtensionsPlugin.DebugEnabled.Value)
                        {
                            DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"DDSScript 'Scope' result for {newScope}: Scope: {ddsScript.scope}, Object: {ToTypedStringExtension.ToTypedString(outputObject)} ");
                        }
                        __result = Toolbox.Instance.resourcesCache[Il2CppType.Of<DDSScope>()][ddsScript.scope].TryCast<DDSScope>();
                        return false;
                    }
                    catch (System.Exception ex)
                    {
                        DDSScriptExtensionsPlugin.PluginLogger.LogError(ex.Message);
                        DDSScriptExtensionsPlugin.PluginLogger.LogError(ex.StackTrace);
                    }
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Strings), nameof(Strings.GetContainedValue))]
        public class Strings_GetContainedValue
        {
            static Regex seedOffsetRegex = new Regex(@"(.*)_(\d+)", RegexOptions.IgnoreCase);

            public static bool Prefix(ref string __result, string withinScope, string newValue, object baseObject, object inputObject, object additionalObject)
            {
                string scopeToSearch = withinScope;

                if (newValue.StartsWith("custom_") && DDSScriptExtensionsPlugin.LoadedExtensions["values"].ContainsKey(scopeToSearch))
                {
                    var trimmedNewValue = newValue;
                    int seedOffset = 0;

                    // Check if there is a seed offset to the applied from the DDS content
                    var offsetMatch = seedOffsetRegex.Match(newValue);
                    if (offsetMatch != null && offsetMatch.Groups.Count == 3)
                    {
                        trimmedNewValue = offsetMatch.Groups[1].Value;
                        seedOffset = int.Parse(offsetMatch.Groups[2].Value);
                    }
                    
                    if (DDSScriptExtensionsPlugin.LoadedExtensions["values"][scopeToSearch].ContainsKey(trimmedNewValue))
                    {
                        var ddsScript = DDSScriptExtensionsPlugin.LoadedExtensions["values"][scopeToSearch][trimmedNewValue];

                        DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["_caller"] = newValue.Substring("custom_".Length);
                        DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["baseObject"] = baseObject?.TryCast(baseObject.GetActualType());
                        DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["inputObject"] = inputObject?.TryCast(inputObject.GetActualType());
                        DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["additionalObject"] = additionalObject?.TryCast(additionalObject.GetActualType());

                        if (DDSScriptExtensionsPlugin.DebugEnabled.Value)
                        {
                            try
                            {
                                if (baseObject != null)
                                    DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"baseObject is: {baseObject.GetType().Name} - {ToTypedStringExtension.ToTypedString(baseObject)}");
                                else
                                    DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"baseObject is null");

                                if (inputObject != null)
                                    DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"inputObject is: {inputObject.GetType().Name} - {ToTypedStringExtension.ToTypedString(inputObject)}");
                                else
                                    DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"inputObject is null");

                                if (additionalObject != null)
                                    DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"additionalObject is: {additionalObject.GetType().Name} - {ToTypedStringExtension.ToTypedString(additionalObject)}");
                                else
                                    DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"additionalObject is null");
                            }
                            catch
                            { }
                        }

                        try
                        {
                            string calculatedSeedStatement = "";
                            // If we have a defined seed function, run it first
                            if (ddsScript.seedStatement != null)
                            {
                                calculatedSeedStatement = $"math.randomseed({ddsScript.seedStatement} + {seedOffset})";
                                DDSScriptExtensionsPlugin.LuaScriptEnvironment.DoString(calculatedSeedStatement);
                            }

                            // Run the calculation function
                            DynValue res = DDSScriptExtensionsPlugin.LuaScriptEnvironment.DoString(ddsScript.script);

                            if (DDSScriptExtensionsPlugin.DebugEnabled.Value)
                            {
                                DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"DDSScript 'Value' result for {scopeToSearch}.{newValue}: {res.String}. Seed statement was: {calculatedSeedStatement}");
                            }
                            __result = $"{res.String}";
                            return false;
                        }
                        catch (System.Exception ex)
                        {
                            DDSScriptExtensionsPlugin.PluginLogger.LogError(ex.Message);
                            DDSScriptExtensionsPlugin.PluginLogger.LogError(ex.StackTrace);
                        }
                    }
                }

                return true;
            }
        }
    }
}