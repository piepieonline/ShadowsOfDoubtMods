﻿using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using SOD.Common.Extensions;

namespace WhiteCollarSideJobs
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class WhiteCollarSideJobs : BasePlugin
    {
        public static ManualLogSource PluginLogger;

        public static Dictionary<int, WhiteCollarVMailThreadData> threadData = new Dictionary<int, WhiteCollarVMailThreadData>();

        public const string VMailID_Guilty_JobPreset_SideJobFinanceInvestigatorOffice = "ad52f2e8-a66e-4670-a1e4-07f7f138279d";
        public const string VMailID_NotGuilty_JobPreset_SideJobFinanceInvestigatorOffice = "fb7c0f68-0dad-497f-90ec-534fa1678788";
        public const string VMailID_BossReminder_JobPreset_SideJobFinanceInvestigatorOffice = "488c7cb9-5229-45fe-b35a-a08f213136ab";

        public override void Load()
        {
            PluginLogger = Log;

            // Plugin startup logic
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched and has injected types!");
        }
    }

    [HarmonyPatch]
    public class Toolbox_NewVmailThread
    {
        [HarmonyTargetMethod]
        internal static System.Reflection.MethodBase CalculateMethod()
        {
            var mi = typeof(Toolbox).GetMethods().Where(mi => mi.Name == "NewVmailThread" && mi.GetParameters().Length == 10).First();
            return mi;
        }

        public static void Prefix(StateSaveData.MessageThreadSave __result, Human from, ref Human to1, string treeID, ref float timeStamp, int progress)
        {
            switch(treeID)
            {
                case WhiteCollarSideJobs.VMailID_Guilty_JobPreset_SideJobFinanceInvestigatorOffice:
                    // TODO: Adjust timeStamp to within working hours. For both the original vmail and the others
                    // Default sidejob calculation: SessionData.Instance.gameTime + Toolbox.Instance.Rand(-48f, -12f)
                    var employees = from.job.employer.companyRoster.Select(occ => occ.employee).ToList();
                    var seed = $"{treeID}_{from.humanID}";
                    to1 = SelectRandomCoworker(employees, new List<int>() { from.humanID }, new List<string>() { "Receptionist" }, ref seed);
                    CreateDistractionVmails(from, employees, timeStamp, ref seed);
                    break;
            }
        }

        private static void CreateDistractionVmails(Human perp, List<Human> coworkers, float timeStamp, ref string seed)
        {
            var employer = perp.job.employer;
            var excludedTitles = new List<string>() { "Receptionist" };
            var excludedCoworkers = new List<int>() { perp.humanID, employer.director.humanID };
            // TODO: Modify timestamp, potentially just send these vmails globally scheduled, rather than as distractors

            /*
            var daysOpen = employer.daysOpen;
            // We could calculate this, but it's not worth doing
            SessionData.Instance.GetNextOrPreviousGameTimeForThisHour(
                ref daysOpen,
                9, // employer.preset.workHours.shifts.Where(shift => shift.shiftType == OccupationPreset.ShiftType.dayShift).First().decimalHours.x,
                17 // employer.preset.workHours.shifts.Where(shift => shift.shiftType == OccupationPreset.ShiftType.dayShift).First().decimalHours.y
            );

            SessionData.Instance.ParseTimeData(timeStamp, out float hour,)
            */

            // Send reminder email from boss
            Toolbox.Instance.NewVmailThread(perp.job.employer.director, perp.job.employer.director, null, null, perp.job.employer.companyRoster.Select(occupation => occupation.employee).ToListIl2Cpp(), WhiteCollarSideJobs.VMailID_BossReminder_JobPreset_SideJobFinanceInvestigatorOffice, timeStamp + Toolbox.Instance.Rand(-48f, -12f));

            // Send other invoices around the office
            int maxDistractors = Math.Min(3, ((coworkers.Count - excludedCoworkers.Count) / 2) - 1);
            for(int i = 0; i < maxDistractors; i++)
            {
                var selectedSender = SelectRandomCoworker(coworkers, excludedCoworkers, excludedTitles, ref seed);
                excludedCoworkers.Add(selectedSender.humanID);
                var selectedReceiver = SelectRandomCoworker(coworkers, excludedCoworkers, excludedTitles, ref seed);
                excludedCoworkers.Add(selectedReceiver.humanID);
                Toolbox.Instance.NewVmailThread(selectedSender, selectedReceiver, null, null, null, WhiteCollarSideJobs.VMailID_NotGuilty_JobPreset_SideJobFinanceInvestigatorOffice, timeStamp + Toolbox.Instance.Rand(-36f, 36f));
            }
        }

        private static Human SelectRandomCoworker(List<Human> employees, List<int> ineligibleEmployeeIds, List<string> ineligibleTitles, ref string seed)
        {
            var filteredEmployees = employees.Where(employee => !ineligibleEmployeeIds.Contains(employee.humanID) && !ineligibleTitles.Contains(employee.job.preset.presetName)).ToList();
            var selectedIndex = Toolbox.Instance.GetPsuedoRandomNumberContained(0, filteredEmployees.Count, ref seed);
            return filteredEmployees[selectedIndex];
        }
    }

    [HarmonyPatch(typeof(Strings), nameof(Strings.GetContainedValue))]
    public class Strings_GetContainedValue
    {
        public static bool Prefix(ref string __result, string withinScope, string newValue, object baseObject)
        {
            if (newValue.StartsWith("custom_pie_whitecollar_") && UniverseLib.ReflectionExtensions.GetActualType(baseObject).Name == "VmailParsingData")
            {
                var thread = baseObject.TryCast<VMailApp.VmailParsingData>().thread;
                var threadId = thread.threadID;
                var isIllegalVmail = thread.treeID == WhiteCollarSideJobs.VMailID_Guilty_JobPreset_SideJobFinanceInvestigatorOffice;
                var seed = $"{threadId}_{thread.recievers[0]}_{newValue}";

                WhiteCollarVMailThreadData threadData;

                if (!WhiteCollarSideJobs.threadData.TryGetValue(threadId, out threadData))
                {
                    if (isIllegalVmail)
                    {
                        threadData = new WhiteCollarVMailThreadData()
                        {
                            generatedInvoiceNumber = Toolbox.Instance.GetPsuedoRandomNumberContained(1000, 9999, ref seed), // Invalid invoice number TODO Make the valid/invalid a random length
                            generatedInvoiceCost = Toolbox.Instance.GetPsuedoRandomNumberContained(500, 2500, ref seed), // Lower cost
                            generatedInvoicePayeeName = SelectRandomCompanyOfPreset("LoanShark", ref seed, CityData.Instance.citizenDictionary[thread.recievers[0]].job.employer.companyID).name
                        };
                    }
                    else
                    {
                        threadData = new WhiteCollarVMailThreadData()
                        {
                            generatedInvoiceNumber = Toolbox.Instance.GetPsuedoRandomNumberContained(10000, 99999, ref seed),
                            generatedInvoiceCost = Toolbox.Instance.GetPsuedoRandomNumberContained(1000, 9999, ref seed),
                            generatedInvoicePayeeName = SelectRandomCompanyOfPreset("MediumOffice", ref seed, CityData.Instance.citizenDictionary[thread.recievers[0]].job.employer.companyID).name
                        };
                    }
                }

                switch (newValue)
                {
                    case "custom_pie_whitecollar_invoicenumber":
                        __result = threadData.generatedInvoiceNumber + "";
                        return false;
                    case "custom_pie_whitecollar_invoicecost":
                        __result = threadData.generatedInvoiceCost + "";
                        return false;
                    case "custom_pie_whitecollar_payee":
                        __result = threadData.generatedInvoicePayeeName;
                        return false;
                    case "custom_pie_whitecollar_invoicelength":
                        __result = "five";
                        return false;
                }
            }

            return true;
        }

        private static Company SelectRandomCompanyOfPreset(string presetName, ref string seed, int notThisCompanyId = -1)
        {
            var companies = CityData.Instance.companyDirectory.Where(company => company.preset.presetName == presetName && company.companyID != notThisCompanyId).ToList();
            var selectedIndex = Toolbox.Instance.GetPsuedoRandomNumberContained(0, companies.Count, ref seed);
            return companies[selectedIndex];
        }
    }

    [Serializable]
    public class WhiteCollarVMailThreadData
    {
        public int threadID;

        public int generatedInvoiceNumber;
        public int generatedInvoiceCost;
        public string generatedInvoicePayeeName;
    }
}