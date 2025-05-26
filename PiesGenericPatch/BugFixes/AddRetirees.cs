using HarmonyLib;
using UniverseLib;

namespace Pies_Generic_Patch.BugFixes
{
    /// <summary>
    /// In vanilla, the retired occupation is unused, which causes issues for some MurderMO and SideJob presets
    /// Using the existing retired preset, homed citizens who are unemployed at the end of generation are changed to be retired instead
    /// Homeless citizens are unaffected
    /// 
    /// Only works on new city generation
    /// </summary>
    public class AddRetirees
    {
        public static void DoPatch(Harmony harmony)
        {
            harmony.PatchAll(typeof(AddRetirees.CitizenCreator_SetComplete));
        }

        // We have to patch the virtual method as no real implementation exists
        [HarmonyPatch(typeof(Creator), nameof(Creator.SetComplete))]
        public class CitizenCreator_SetComplete
        {
            public static void Prefix(object __instance)
            {
                // Ensure that it's the CitizenCreator that is complete, not another inheriting class
                if (__instance.GetActualType() == typeof(CitizenCreator) &&
                    (CityConstructor.Instance.generateNew || false)
                )
                {
                    OverrideCitizensToRetired();
                }
            }
        }

        // In theory, this could be run on an existing city without issue
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
                    if(Pies_Generic_PatchPlugin.DebugLogging.Value)
                        Pies_Generic_PatchPlugin.Log.LogInfo($"Overriding {citizen.citizenName}: From unemployed to retired");

                    Occupation retired = new Occupation()
                    {
                        preset = CitizenCreator.Instance.retiredPreset,
                        employer = null,
                        paygrade = 0.0f
                    };
                    retired.name = Strings.Get("jobs", retired.preset.name);

                    citizen.SetJob(retired);
                }
            }
        }
    }
}
