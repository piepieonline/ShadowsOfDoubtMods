using HarmonyLib;
using System.Collections.Generic;

namespace AdditionalEvidence
{
    internal class BinPasscodes
    {
        private const string NotePresetName = "IP_BinPasscodeNote";
        private const string OfficeMemoPresetName = "BinNote";
        private const string EmployeesOfficeCode = "5012b526-cfb0-4ff3-b39c-4effb810e86a";
        private const string PasscodeReminder = "970c4114-def0-4e04-8982-da36e01f4905";

        private static readonly string[] ModNotePresets = { NotePresetName, OfficeMemoPresetName };

        // The preset declares who writes one and how often, but the pool would store it in the address' first bin,
        // so the queue is intercepted here and the notes are placed once the pool has settled.
        private static readonly Dictionary<int, List<Human>> pendingNotes = new Dictionary<int, List<Human>>();
        private static InteractablePreset notePreset;

        static void Log(object data, BepInEx.Logging.LogLevel logLevel = BepInEx.Logging.LogLevel.Info)
        {
            if (AdditionalEvidencePlugin.BinPasscodes_DebugLogging.Value || logLevel <= BepInEx.Logging.LogLevel.Error)
                AdditionalEvidencePlugin.PluginLogger.Log(logLevel, data);
        }

        // Anything binned is a page of the bin's evidence rather than an object in the world, so it can only be
        // reached by walking those pages back to their meta objects.
        internal static void ForEachBin(NewGameLocation location, System.Action<NewRoom, Interactable, EvidenceMultiPage> onBin)
        {
            foreach (var room in location.rooms)
            {
                foreach (var furniture in room.individualFurniture)
                {
                    foreach (var spawned in furniture.spawnedInteractables)
                    {
                        var contents = spawned.evidence != null ? spawned.evidence.TryCast<EvidenceMultiPage>() : null;
                        if (contents != null) onBin(room, spawned, contents);
                    }
                }
            }
        }

        private static int ForEachBinnedNote(NewGameLocation location, string presetName, System.Action<NewRoom, Interactable, MetaObject> onFound)
        {
            var found = 0;

            ForEachBin(location, (room, bin, contents) =>
            {
                foreach (var page in contents.pageContent)
                {
                    if (page.meta <= 0) continue;

                    var meta = CityData.Instance.FindMetaObject(page.meta);
                    if (meta == null || meta.preset != presetName) continue;

                    found++;
                    if (onFound != null) onFound(room, bin, meta);
                }
            });

            return found;
        }

        private static bool HoldsModNote(EvidenceMultiPage contents)
        {
            foreach (var page in contents.pageContent)
            {
                if (page.meta <= 0) continue;

                var meta = CityData.Instance.FindMetaObject(page.meta);
                if (meta == null) continue;

                foreach (var preset in ModNotePresets)
                {
                    if (meta.preset == preset) return true;
                }
            }

            return false;
        }

        // attemptToStoreInFolder always takes the first bin the address happens to list, so everything stored that
        // way shares one basket with the vanilla receipts. Picking the bin here spreads notes across the premises.
        private static Interactable PickFreeBin(NewGameLocation location, EvidencePreset binEvidence)
        {
            var free = new List<Interactable>();

            ForEachBin(location, (room, bin, contents) =>
            {
                if (bin.evidence.preset != binEvidence) return;
                if (HoldsModNote(contents)) return;

                free.Add(bin);
            });

            if (free.Count <= 0) return null;

            return free[Toolbox.Instance.SeedRand(0, free.Count)];
        }

        // Does by hand what the folder branch of PlaceObject does, minus its choice of bin. The passed variable is
        // what carries the tree, so the note can reuse a vanilla document without a preset of its own.
        internal static Interactable BinNote(NewGameLocation location, InteractablePreset preset, Human writer, string ddsTree)
        {
            if (preset.attemptToStoreInFolder == null)
            {
                Log($"{preset.name} is not stored in a folder, so it cannot be binned", BepInEx.Logging.LogLevel.Error);
                return null;
            }

            var bin = PickFreeBin(location, preset.attemptToStoreInFolder);
            if (bin == null) return null;

            var contents = bin.evidence.TryCast<EvidenceMultiPage>();
            if (contents == null) return null;

            BinNoteInto(bin, contents, preset, writer, ddsTree);

            return bin;
        }

        internal static void BinNoteInto(Interactable bin, EvidenceMultiPage contents, InteractablePreset preset, Human writer, string ddsTree)
        {
            var passed = new Il2CppSystem.Collections.Generic.List<Interactable.Passed>();
            passed.Add(new Interactable.Passed(Interactable.PassedVarType.ddsOverride, 0f, ddsTree));

            contents.AddContainedMetaObjectToNewPage(new MetaObject(preset, writer, writer, writer, passed));
        }

        [HarmonyPatch(typeof(NewGameLocation), nameof(NewGameLocation.AddToPlacementPool))]
        internal class NewGameLocation_AddToPlacementPool
        {
            public static bool Prefix(NewGameLocation __instance, InteractablePreset interactable, Human writer)
            {
                if (interactable == null || interactable.name != NotePresetName) return true;

                if (!AdditionalEvidencePlugin.BinPasscodes_Enabled.Value || writer == null) return false;

                if (Toolbox.Instance.SeedRand(0f, 1f) > AdditionalEvidencePlugin.BinPasscodes_ChancePerCitizen.Value) return false;

                List<Human> pending;
                if (!pendingNotes.TryGetValue(__instance.GetInstanceID(), out pending))
                {
                    pending = new List<Human>();
                    pendingNotes[__instance.GetInstanceID()] = pending;
                }

                // The preset's own per address limit is applied by the method being skipped here
                if (pending.Count > 0) return false;

                notePreset = interactable;
                pending.Add(writer);
                return false;
            }
        }

        [HarmonyPatch(typeof(NewGameLocation), nameof(NewGameLocation.PlaceObjects))]
        internal class NewGameLocation_PlaceObjects
        {
            public static void Postfix(NewGameLocation __instance)
            {
                List<Human> pending;
                if (!pendingNotes.TryGetValue(__instance.GetInstanceID(), out pending)) return;

                pendingNotes.Remove(__instance.GetInstanceID());

                foreach (var writer in pending)
                {
                    var bin = BinNote(__instance, notePreset, writer, PasscodeReminder);
                    if (bin == null)
                    {
                        Log($"No free bin at {__instance.name} to take {writer.GetCitizenName()}'s passcode note");
                        continue;
                    }

                    Log($"{writer.GetCitizenName()} binned their passcode note in the {bin.GetName()} in {(bin.node != null ? bin.node.room.name : "?")}");
                }
            }
        }

        // A company's door code belongs to an owned room rather than to the address, so this mirrors the branch
        // of PickPassword that writes the same memo onto a desk. Running here rather than during the object pool
        // is what lets it pick its own bin, as by now every pool placement has settled.
        [HarmonyPatch(typeof(NewRoom), nameof(NewRoom.PickPassword))]
        internal class NewRoom_PickPassword
        {
            public static void Postfix(NewRoom __instance)
            {
                if (!AdditionalEvidencePlugin.BinPasscodes_Enabled.Value) return;
                if (!__instance.passcode.used) return;
                if (__instance.belongsTo == null || __instance.belongsTo.Count <= 0) return;

                var address = __instance.gameLocation != null ? __instance.gameLocation.thisAsAddress : null;
                if (address == null || address.company == null) return;

                if (Toolbox.Instance.SeedRand(0f, 1f) > AdditionalEvidencePlugin.BinPasscodes_CompanyChancePerOffice.Value) return;

                // Larger premises have several coded offices, and one discarded memo per company is plenty
                if (ForEachBinnedNote(address, OfficeMemoPresetName, null) > 0) return;

                var note = InteriorControls.Instance.binNote;
                if (note == null)
                {
                    Log("Unable to find the BinNote preset, no office memos will be discarded", BepInEx.Logging.LogLevel.Error);
                    return;
                }

                var owner = __instance.belongsTo[0];

                var bin = BinNote(address, note, owner, EmployeesOfficeCode);
                if (bin == null)
                {
                    Log($"No free bin at {address.name} to take {owner.GetCitizenName()}'s office code memo");
                    return;
                }

                Log($"{owner.GetCitizenName()} binned the code for {__instance.name} in the {bin.GetName()} at {address.name}");
            }
        }
    }
}
