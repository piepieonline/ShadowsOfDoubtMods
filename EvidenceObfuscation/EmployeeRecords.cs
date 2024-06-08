using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvidenceObfuscation
{
    internal class EmployeeRecords
    {
        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                if(EvidenceObfuscationPlugin.EmployeeRecord_Printed_FingerprintsRemoved.Value)
                {
                    Toolbox.Instance.evidencePresetDictionary["printedemployeerecord"].keyMergeOnDiscovery[0].mergeKeys.Remove(Evidence.DataKey.fingerprints);
                    Toolbox.Instance.allDDSTrees["10084020-1a7d-4048-8885-28c8985199e1"].messages.RemoveAt(2);//(msg => msg.elementName == "FingerprintPhoto")
                    EvidenceObfuscationPlugin.PluginLogger.LogInfo($"Removed fingerprints from printed employee records");
                }

                if (EvidenceObfuscationPlugin.EmployeeRecord_Filling_FingerprintsRemoved.Value)
                {
                    Toolbox.Instance.evidencePresetDictionary["employeerecord"].keyMergeOnDiscovery[0].mergeKeys.Remove(Evidence.DataKey.fingerprints);
                    Toolbox.Instance.allDDSTrees["674940fb-2f4a-4f15-8675-5864a5296c8b"].messages.RemoveAt(7);//(msg => msg.elementName == "FingerprintPhoto")
                    EvidenceObfuscationPlugin.PluginLogger.LogInfo($"Removed fingerprints from employee records in filling cabinets");
                }
            }
        }
    }
}
