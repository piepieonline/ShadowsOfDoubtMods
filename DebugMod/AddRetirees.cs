using HarmonyLib;
using SOD.Common.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UniverseLib;

namespace DebugMod
{
    public class AddRetirees
    {
        [HarmonyPatch(typeof(Creator), nameof(Creator.SetComplete))]
        public class CitizenCreator_SetComplete
        {
            public static void Prefix(object __instance)
            {
                if (__instance.GetActualType() == typeof(CitizenCreator) &&
                    (CityConstructor.Instance.generateNew || false)
                ) 
                {
                    OverrideCitizensToRetired();
                }
            }
        }

        public static void OverrideCitizensToRetired()
        {
            Citizen citizen;
            for (int i = CityData.Instance.citizenDirectory.Count - 1; i >= 0; i--)
            {
                citizen = CityData.Instance.citizenDirectory[i];
                if (
                    !citizen.isHomeless &&
                    citizen.job.preset.presetName == CitizenCreator.Instance.unemployedPreset.presetName
                )
                {
                    DebugModPlugin.PluginLogger.LogInfo($"Overriding {citizen.citizenName}: From unemployed to retired");

                    Occupation retired = new Occupation()
                    {
                        preset = CitizenCreator.Instance.retiredPreset,
                        employer = (Company)null,
                        paygrade = 0.0f
                    };
                    retired.name = Strings.Get("jobs", retired.preset.name);

                    citizen.SetJob(retired);
                }
            }
        }
    }
}
