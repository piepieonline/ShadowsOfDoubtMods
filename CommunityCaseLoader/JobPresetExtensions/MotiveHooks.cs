using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UniverseLib;
using static SideJobController;

namespace CommunityCaseLoader.JobPresetExtensions
{
    /*
     * NOTES:
        * Can't intercept method, no calls that are interceptable (only SideJobController.MotivePass - except it uses ref and out params)
        * Can't intercept JobPickData constructor and rand followup, as it doesn't have one
        * Could intercept SetJobState and exclude all coworkers, but they would be exempted from all jobs
     * End result
        * For now, exclude coworkers if they are chosen for job creation only
     */
    internal class MotiveHooks
    {
        public static HashSet<string> PerpOnePerBusiness = new HashSet<string>() { "SideJobFinanceInvestigatorOffice" };

        // The game's lists can't be replaced, so we add to them and remember exactly what we added
        static readonly List<Human> addedPosterExemptions = new List<Human>();
        static readonly List<Human> addedPurpExemptions = new List<Human>();
        static bool insideJobCreationCheck;

        //[HarmonyPatch(typeof(SideJobController), nameof(SideJobController.JobCreationCheck))]
        public static class SideJobController_JobCreationCheck
        {
            public static void Prefix(SideJobController __instance)
            {
                RemoveOurExemptions(__instance);
                insideJobCreationCheck = true;
            }

            public static void Postfix(SideJobController __instance)
            {
                RemoveOurExemptions(__instance);
                insideJobCreationCheck = false;
            }
        }

        // Called once per preset at the top of JobCreationCheck, before that preset picks its poster/purp
        // [HarmonyPatch(typeof(JobPreset), nameof(JobPreset.GetFrequencyForSocialCreditLevel))]
        public static class JobPreset_GetFrequencyForSocialCreditLevel
        {
            public static void Postfix(JobPreset __instance)
            {
                if (!insideJobCreationCheck) return;

                var controller = SideJobController.Instance;
                RemoveOurExemptions(controller);

                if (!PerpOnePerBusiness.Contains(__instance.name)) return;

                var tracking = FindTracking(controller, __instance);
                if (tracking == null) return;

                for (int i = 0; i < tracking.activeJobs.Count; i++)
                {
                    var job = tracking.activeJobs[i];
                    if (job == null) continue;
                    ExemptCoworkers(controller, job.poster);
                    ExemptCoworkers(controller, job.purp);
                }
            }
        }

        static JobTracking FindTracking(SideJobController controller, JobPreset preset)
        {
            for (int i = 0; i < controller.jobTracking.Count; i++)
            {
                var tracking = controller.jobTracking[i];
                if (tracking != null && tracking.preset == preset) return tracking;
            }
            return null;
        }

        static void ExemptCoworkers(SideJobController controller, Human citizen)
        {
            if (citizen == null || citizen.job == null || citizen.job.employer == null) return;

            var roster = citizen.job.employer.companyRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var occupation = roster[i];
                if (occupation == null || occupation.employee == null) continue;

                if (!controller.exemptFromPosters.Contains(occupation.employee))
                {
                    controller.exemptFromPosters.Add(occupation.employee);
                    addedPosterExemptions.Add(occupation.employee);
                }

                if (!controller.exemptFromPurps.Contains(occupation.employee))
                {
                    controller.exemptFromPurps.Add(occupation.employee);
                    addedPurpExemptions.Add(occupation.employee);
                }
            }
        }

        static void RemoveOurExemptions(SideJobController controller)
        {
            for (int i = 0; i < addedPosterExemptions.Count; i++)
                controller.exemptFromPosters.Remove(addedPosterExemptions[i]);
            for (int i = 0; i < addedPurpExemptions.Count; i++)
                controller.exemptFromPurps.Remove(addedPurpExemptions[i]);

            addedPosterExemptions.Clear();
            addedPurpExemptions.Clear();
        }

        static JobPickData currentJobUnderConstruction = null;
        /*
        [HarmonyPatch(typeof(SideJob), MethodType.Constructor)]
        public class SideJob_Ctor
        {
            [HarmonyPrefix]
            public static bool Postfix(JobPreset newPreset, SideJobController.JobPickData newData, bool immediatePost)
            {
                InteractionController.Instance.nearbyInteractables.Where(ni => ((Interactable)ni.objectRef).name == "Door(Clone)");

                // CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"List added type: {item?.GetActualType().FullName}");
                if (PerpOnePerBusiness.Contains(__instance.presetStr))
                {
                    switch (__instance.state)
                    {
                        case SideJob.JobState.generated:
                        case SideJob.JobState.posted:
                            CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"Found sidejob to exclude coworkers of");
                            SideJobController.Instance.AddExemptFromPurpJob();
                            break;
                        case SideJob.JobState.ended:
                            CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"Removing ");
                            break;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SideJob), nameof(SideJob.SetJobState))]
        public class SideJob_SetJobState
        {
            [HarmonyPrefix]
            public static void Postfix(ref SideJob.JobState newState, SideJob __instance)
            {
                // CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"List added type: {item?.GetActualType().FullName}");
                if (PerpOnePerBusiness.Contains(__instance.presetStr))
                {
                    switch (__instance.state)
                    {
                        case SideJob.JobState.generated:
                        case SideJob.JobState.posted:
                            CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"Found sidejob to exclude coworkers of");
                            SideJobController.Instance.AddExemptFromPurpJob();
                            break;
                        case SideJob.JobState.ended:
                            CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"Removing ");
                            break;
                    }
                }
            }
        }
        */

        /*
        [HarmonyPatch(typeof(SideJob), nameof(SideJob.SetJobState))]
        public class SideJob_SetJobState
        {
            [HarmonyPrefix]
            public static void Postfix(SideJob.JobState newState, SideJob __instance)
            {
                // CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"List added type: {item?.GetActualType().FullName}");
                if (PerpOnePerBusiness.Contains(__instance.presetStr))
                {
                    switch (__instance.state)
                    {
                        case SideJob.JobState.generated:
                        case SideJob.JobState.posted:
                            CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"Found sidejob to exclude coworkers of");
                            SideJobController.Instance.AddExemptFromPurpJob();
                            break;
                        case SideJob.JobState.ended:
                            CommunityCaseLoaderPlugin.PluginLogger.LogWarning($"Removing ");
                            break;
                    }
                }
                return true;
            }
        }
        */
    }
}
