using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvidenceObfuscation
{
    internal class MurderWeaponObfuscation
    {
        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix(ref Toolbox __instance)
            {
                if(EvidenceObfuscationPlugin.MurderWeapon_GunTypeRemoved.Value)
                {
                    SOD.Common.Lib.DdsStrings.AddOrUpdateEntries("dds.blocks",
                        ("0917036a-5eca-4a45-84c9-bb65133678fb", "violently shot to death."),
                        ("1270e698-7641-4fd4-95f8-d8fc7fbe7ea9", "violently shot to death."),
                        ("4f651c7a-82a3-401b-8faa-7cb51ecc68f6", "violently shot to death.")
                    );
                }

                if (EvidenceObfuscationPlugin.MurderWeapon_MeleeTypeRemoved.Value)
                {
                    SOD.Common.Lib.DdsStrings.AddOrUpdateEntries("dds.blocks",
                        ("9e504f7a-50f8-4f80-ba2c-08aa8b9d78d0", "violently killed."),
                        ("fc9c3aa9-2b34-4344-a712-c2fab79a4f32", "violently killed.")
                    );
                }
            }
        }

        [HarmonyPatch(typeof(ActionController), nameof(ActionController.Inspect))]
        public class ActionController_Inspect
        {
            public static bool Prefix(Interactable what)
            {
                if(what?.preset?.spawnEvidence?.subClass == "Wound")
                {
                    what.MarkInspected();
                    return false;
                }

                return true;
            }
        }
    }
}
