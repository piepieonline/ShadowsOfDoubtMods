using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvidenceObfuscation
{
    internal class GovDatabase
    {
        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix(ref Toolbox __instance)
            {
                if (EvidenceObfuscationPlugin.GovDatabase_FingerprintsRemoved.Value)
                {
                    Toolbox.Instance.evidencePresetDictionary["printedcitizenfile"].keyMergeOnDiscovery[0].mergeKeys.Remove(Evidence.DataKey.fingerprints);
                    EvidenceObfuscationPlugin.RemoveMessageMatching(Toolbox.Instance.allDDSTrees["bb788164-3ced-4f72-88b4-998ba1dc822d"], "FingerprintPhoto");
                    EvidenceObfuscationPlugin.PluginLogger.LogInfo($"Removed fingerprints from government database records");
                }
                
                __instance.evidencePresetDictionary["employeephoto"].itemOwner = EvidencePreset.BelongsToSetting.boss;
                __instance.objectPresetDictionary["EmployeePhoto"].printsSource = RoomConfiguration.PrintsSource.owners;
            }
        }
    }
}
