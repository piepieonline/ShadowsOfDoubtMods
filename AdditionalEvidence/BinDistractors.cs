using HarmonyLib;
using System.Collections.Generic;

namespace AdditionalEvidence
{
    internal class BinDistractors
    {
        private static readonly string[] WorkplaceNotes =
        {
            "fc176bdd-2863-4a73-ae9c-29b2572f56d6", // Ad_KensingtonIndigo
            "93638625-4c42-4866-865d-9c184e75aea3", // Ad_CandorLeft
            "e4f8bd1a-0660-454a-a336-a4caabed3581", // Ad_CandorRight
            "b0f7447a-21bb-4b21-8a82-93cf816d944f", // Ad_ElGen
            "b6680143-76f0-4225-9f13-4be2ea203427", // Ad_Starch
            "28d6afd8-b697-4ce2-9bda-cd749e0d9487", // Starch_Kola_Sweepstake
            "2c7a5ba2-7e09-414f-9825-c54a82f2896f", // Ad_TheFields
            "4ee34c9f-be69-4967-9fe0-c762e9ee227b", // Ad_TheFields2
            "a28e5c18-c816-46fa-9e26-a926691dbcab", // Ad_TheFields3
            "87e82b27-6264-49e3-8c2a-3966057b0ac0", // Ad_TheFields4
            "ff83c30c-4b6c-4fb9-9b63-921e7a3e3e3b", // Rich_ElGenPartyInvite
            "9e743c68-86cb-40e9-8895-44ea50aba944", // Ad_Enforcers
            "f3e499aa-48c3-4d4b-add1-d78198eaf28f", // Ad_Hospital
            "d24b0eb3-bfc8-400f-807c-1396ff58d1bf", // Ad_Diner
            "b17ca66c-d293-4602-8055-65b8a169a294", // LEM_Note1
            "de6e4e79-a360-463c-b1d1-f6a9c31b5799", // RedGums_Leaflet
            "675caa15-322b-4d6c-8228-4245b1378454", // RedGums_Leaflet_2
        };

        private static readonly string[] HomeNotes =
        {
            "8d4aa9ac-dad4-4a8e-ae33-17e21f261731", // Poor_DailyAffirmations
            "86e8e0ea-9cb5-4162-90b8-a8c9c5bf8170", // Shopping_List_Crumpl
            "1eba88ee-8802-4bd5-98b7-09470f8cdb93", // Shopping_List
            "7e5242f2-6aa2-44bf-914c-452a7c48e961", // Bad_Poetry
            "978b6bf8-9763-4ecd-959a-a143ec2f9ef3", // Crumpled_Writing
            "8d8d1f69-59d3-4639-ae67-d29f4485fb4f", // Poor_HalfWrittenLetter
            "8f4b811d-ae54-4da1-93cc-4ebd621db819", // Rich_DinnerNote
            "991ea124-05c1-4700-8e11-ecfa765ee559", // Crumpled_Drawing
            "13d6be7d-ed80-4263-a20a-eb783c30b9d1", // Crumpled_Blank
            "66d2a5c6-bbaa-49c9-b4e3-abe321c6091e", // LEM_PersonalLetter
            "a5e7b0f5-3b93-43dc-af84-ffbdfb825864", // LEM_PersonalLetter2
            "45849b08-2951-40e0-ab7c-f0d5892737d3", // LEM_PersonalLetter3
            "7d0c840e-ec58-4ef7-bea4-5d9e2e78cd52", // Rich_PersonalLetterRedGums
            "de8d1d15-25a6-4f24-ae06-172f631fa589", // DebtCollectionLetter
            "ea869950-7590-4ca2-b472-9811dd583e57", // BillFinalNotice
            "e29e63cb-2e64-4e49-9b92-b18cf4425447", // Candor_Subscription
            "65bd6dd2-3302-4e78-884a-9b23804b09a4", // Ad_Kaizen
            "417f9758-001b-48ed-a32f-42e37294c306", // Ad_Gemsteader
            "8c83d3f7-0f1a-4cf2-9f85-2487f3a4dcee", // Ad_BarNewManagement
            "1d64e27b-9992-4688-90ce-eab60bf88dd9", // Chapter01_CrumpledFlyer
        };

        private static readonly string[] HotelNotes =
        {
            "0fd11355-3a7a-4586-87cd-07f4ae514936", // Hotel_Notice_Bar
            "2a82a770-13d9-45a9-86f2-3d3755874e84", // Hotel_Notice_Shoes
        };

        static void Log(object data, BepInEx.Logging.LogLevel logLevel = BepInEx.Logging.LogLevel.Info)
        {
            if (AdditionalEvidencePlugin.BinPasscodes_DebugLogging.Value || logLevel <= BepInEx.Logging.LogLevel.Error)
                AdditionalEvidencePlugin.PluginLogger.Log(logLevel, data);
        }

        // Runs once the city is built, so the passcode notes have already claimed their bins and chaff fills in
        // around them rather than crowding them out.
        internal static void OnAfterNewGame(object sender, System.EventArgs e)
        {
            if (!AdditionalEvidencePlugin.BinPasscodes_Enabled.Value) return;

            var note = InteriorControls.Instance.binNote;
            if (note == null)
            {
                Log("Unable to find the BinNote preset, no distractors will be binned", BepInEx.Logging.LogLevel.Error);
                return;
            }

            var chance = AdditionalEvidencePlugin.BinPasscodes_DistractorChancePerBin.Value;
            int bins = 0, filled = 0;

            foreach (var address in CityData.Instance.addressDirectory)
            {
                if (address == null) continue;

                var notes = NotesFor(address);
                var writers = WritersFor(address);

                BinPasscodes.ForEachBin(address, (room, bin, contents) =>
                {
                    bins++;

                    if (Toolbox.Instance.SeedRand(0f, 1f) > chance) return;

                    var tree = notes[Toolbox.Instance.SeedRand(0, notes.Count)];
                    var writer = writers[Toolbox.Instance.SeedRand(0, writers.Count)];

                    BinPasscodes.BinNoteInto(bin, contents, note, writer, tree);
                    filled++;
                });
            }

            Log($"Binned distractors in {filled} of {bins} bins");
        }

        private static List<string> NotesFor(NewAddress address)
        {
            var output = new List<string>();

            if (address.company != null)
            {
                output.AddRange(WorkplaceNotes);

                if (address.building != null && address.building.preset != null && address.building.preset.name == "Hotel")
                    output.AddRange(HotelNotes);
            }
            else
            {
                output.AddRange(HomeNotes);
            }

            return output;
        }

        // Chaff still resolves names and favourite items off its writer, so it wants somebody who belongs here.
        private static List<Human> WritersFor(NewAddress address)
        {
            var output = new List<Human>();

            foreach (var inhabitant in address.inhabitants)
            {
                if (inhabitant != null) output.Add(inhabitant);
            }

            if (output.Count <= 0)
            {
                var citizens = CityData.Instance.citizenDirectory;
                if (citizens.Count > 0) output.Add(citizens[Toolbox.Instance.SeedRand(0, citizens.Count)]);
            }

            return output;
        }
    }
}
