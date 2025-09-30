using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdditionalEvidence
{
    internal class MurderGunDropOverhaul
    {
        [HarmonyPatch(typeof(MurderController.Murder), nameof(MurderController.Murder.WeaponDisposal))]
        public class Murder_WeaponDisposal
        {
            public static void Prefix(MurderController.Murder __instance)
            {
                // TODO: Override to put it in a bin at the scene, or even a dumpster outside
                if(AdditionalEvidencePlugin.GunForensics_ChanceToDropGunsAtScene.Value > 0)
                {
                    __instance.dropChance = AdditionalEvidencePlugin.GunForensics_ChanceToDropGunsAtScene.Value;
                }
            }
        }
    }
}
