using AssetBundleLoader;
using HarmonyLib;
using Il2CppSystem.IO;
using MoonSharp.Interpreter;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
                // TODO: Do we need to expose all globals here? Might be worth doing
                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["Player"] = UserData.Create(Player.Instance);
                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["SessionData"] = UserData.Create(SessionData.Instance);
                DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["CSToString"] = (System.Func<string, object>)ToTypedStringExtension.ToTypedString;
                // Test since DDS patches are broken
                // Strings.stringTable["dds.blocks"]["e2c43611-8e95-4a85-94c6-969a5c3c3015"].displayStr = "Test: |writer.custom_selected_random| |writer.custom_selected_random_1|\r\n";
            }
        }

        [HarmonyPatch(typeof(Strings), nameof(Strings.GetContainedValue))]
        public class Strings_GetContainedValue
        {
            static Regex seedOffsetRegex = new Regex(@"(.*)_(\d+)", RegexOptions.IgnoreCase);

            public static bool Prefix(ref string __result, string withinScope, string newValue, object baseObject, object inputObject, object additionalObject)
            {
                if (newValue.StartsWith("custom_") && DDSScriptExtensionsPlugin.LoadedExtensions.ContainsKey(withinScope))
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

                    if (DDSScriptExtensionsPlugin.LoadedExtensions[withinScope].ContainsKey(trimmedNewValue))
                    {
                        var ddsScript = DDSScriptExtensionsPlugin.LoadedExtensions[withinScope][trimmedNewValue];

                        DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["baseObject"] = baseObject;
                        DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["inputObject"] = inputObject;
                        DDSScriptExtensionsPlugin.LuaScriptEnvironment.Globals["additionalObject"] = additionalObject;

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
                                    DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"additionalObject is: {additionalObject.GetType().Name} - {ToTypedStringExtension.ToTypedString(inputObject)}");
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
                                DDSScriptExtensionsPlugin.PluginLogger.LogInfo($"DDSScript result for {withinScope}.{newValue}: {res.String}. Seed statement was: {calculatedSeedStatement}");
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