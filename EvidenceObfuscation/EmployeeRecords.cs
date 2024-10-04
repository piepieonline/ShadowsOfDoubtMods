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
            public static void Prefix()
            {
                /*
                foreach (var interactablePreset in UnityEngine.Resources.FindObjectsOfTypeAll<InteractablePreset>().Where(preset => preset.name.ToLower() == "employeephoto"))
                {
                    interactablePreset.printsSource = RoomConfiguration.PrintsSource.owners;
                }
                */
                
                /*
                foreach (var evidencePreset in UnityEngine.Resources.FindObjectsOfTypeAll<EvidencePreset>().Where(preset => preset.name.ToLower() == "employeephoto"))
                {
                    evidencePreset.itemOwner = EvidencePreset.BelongsToSetting.boss;
                }
                */
            }

            public static void Postfix(ref Toolbox __instance)
            {
                if(EvidenceObfuscationPlugin.EmployeeRecord_Printed_FingerprintsRemoved.Value)
                {
                    Toolbox.Instance.evidencePresetDictionary["printedemployeerecord"].keyMergeOnDiscovery[0].mergeKeys.Remove(Evidence.DataKey.fingerprints);
                    RemoveMessageMatching(Toolbox.Instance.allDDSTrees["10084020-1a7d-4048-8885-28c8985199e1"], "FingerprintPhoto");
                    EvidenceObfuscationPlugin.PluginLogger.LogInfo($"Removed fingerprints from printed employee records");
                }

                if (EvidenceObfuscationPlugin.EmployeeRecord_Filling_FingerprintsRemoved.Value)
                {
                    Toolbox.Instance.evidencePresetDictionary["employeerecord"].keyMergeOnDiscovery[0].mergeKeys.Remove(Evidence.DataKey.fingerprints);
                    RemoveMessageMatching(Toolbox.Instance.allDDSTrees["674940fb-2f4a-4f15-8675-5864a5296c8b"], "FingerprintPhoto");
                    EvidenceObfuscationPlugin.PluginLogger.LogInfo($"Removed fingerprints from employee records in filling cabinets");
                }

                __instance.evidencePresetDictionary["employeephoto"].itemOwner = EvidencePreset.BelongsToSetting.boss;
                __instance.objectPresetDictionary["EmployeePhoto"].printsSource = RoomConfiguration.PrintsSource.owners;
            }

            private static void RemoveMessageMatching(DDSSaveClasses.DDSTreeSave tree, string name)
            {
                for (int i = tree.messages.Count - 1; i >= 0; i--)
                {
                    if (tree.messages[i].elementName == name)
                    {
                        tree.messages.RemoveAt(i);
                    }
                }
            }
        }
    }
}
