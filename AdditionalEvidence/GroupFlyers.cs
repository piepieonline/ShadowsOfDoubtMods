using HarmonyLib;
using System.Collections.Generic;

namespace AdditionalEvidence
{
    internal class GroupFlyers
    {
        private const string BoardFlyerPresetName = "IP_ClubMeetFlyer";
        private const string LooseFlyerPresetName = "IP_ClubMeetFlyerLoose";

        // Pinned to a noticeboard: eateries only, since that is where a club board belongs.
        private static readonly HashSet<string> EateryRoomConfigurations = new HashSet<string>()
        {
            "AmericanDiner",
            "BarDiningRoom",
            "AsianDiningRoom",
            "FastFoodDiningRoom",
            "RooftopBar"
        };

        // Left lying on a table or counter: anywhere the public passes through.
        private static readonly HashSet<string> PublicRoomConfigurations = new HashSet<string>()
        {
            "AmericanDiner",
            "BarDiningRoom",
            "AsianDiningRoom",
            "FastFoodDiningRoom",
            "RooftopBar",
            "CorporateLobby",
            "Ballroom"
        };

        static void Log(object data, BepInEx.Logging.LogLevel logLevel = BepInEx.Logging.LogLevel.Info)
        {
            if (AdditionalEvidencePlugin.GroupFlyers_DebugLogging.Value || logLevel <= BepInEx.Logging.LogLevel.Error)
                AdditionalEvidencePlugin.PluginLogger.Log(logLevel, data);
        }

        // The GroupPreset clue only ever posts a flyer at the group's own meeting place. This spreads
        // additional copies to the city's other public venues so a flyer can point somewhere the player isn't.
        [HarmonyPatch(typeof(GroupsController), nameof(GroupsController.CreateGroups))]
        internal class GroupsController_CreateGroups
        {
            public static void Postfix(GroupsController __instance)
            {
                if (!AdditionalEvidencePlugin.GroupFlyers_Enabled.Value) return;

                var boardChance = AdditionalEvidencePlugin.GroupFlyers_ChancePerNoticeBoard.Value;
                var looseChance = AdditionalEvidencePlugin.GroupFlyers_ChancePerSurface.Value;
                var cap = AdditionalEvidencePlugin.GroupFlyers_MaxExtraFlyersPerGroup.Value;
                if (cap <= 0 || (boardChance <= 0f && looseChance <= 0f)) return;

                InteractablePreset boardFlyer;
                if (!Toolbox.Instance.LoadDataFromResources<InteractablePreset>(BoardFlyerPresetName, out boardFlyer) || boardFlyer == null)
                {
                    Log($"Unable to find {BoardFlyerPresetName}, no extra club flyers will be placed", BepInEx.Logging.LogLevel.Error);
                    return;
                }

                InteractablePreset looseFlyer;
                if (!Toolbox.Instance.LoadDataFromResources<InteractablePreset>(LooseFlyerPresetName, out looseFlyer))
                {
                    looseFlyer = null;
                }

                var advertisable = GetAdvertisableGroups(__instance, boardFlyer);
                if (advertisable.Count <= 0) return;

                // One cap shared by both passes, so a club cannot exceed it by picking up one of each.
                var placedPerGroup = new Dictionary<int, int>();

                var boards = PlacePass(boardFlyer, GetVenues(EateryRoomConfigurations), advertisable, placedPerGroup, cap, boardChance, "noticeboard");
                var loose = PlacePass(looseFlyer, GetVenues(PublicRoomConfigurations), advertisable, placedPerGroup, cap, looseChance, "surface");

                Log($"Posted {boards} noticeboard and {loose} surface club flyers across the city");
            }
        }

        private static int PlacePass(InteractablePreset flyer, List<NewAddress> venues, List<GroupsController.SocialGroup> advertisable, Dictionary<int, int> placedPerGroup, int cap, float chance, string label)
        {
            if (flyer == null || chance <= 0f) return 0;

            var placed = 0;

            foreach (var address in venues)
            {
                if (Toolbox.Instance.SeedRand(0f, 1f) > chance) continue;

                var group = PickGroupForAddress(advertisable, placedPerGroup, cap, address);
                if (group == null) continue;

                FurnitureLocation pickedFurniture;
                address.PlaceObject(flyer, null, null, null, out pickedFurniture, true,
                    Interactable.PassedVarType.groupID, group.id, false, 0,
                    InteractablePreset.OwnedPlacementRule.nonOwnedOnly, 0, null, false, null, null, null, "", false);

                // Counted per attempt, not per spawn: during city gen this goes to the address'
                // placement pool and resolves later, so there is nothing to check here yet.
                placedPerGroup[group.id] = (placedPerGroup.ContainsKey(group.id) ? placedPerGroup[group.id] : 0) + 1;
                placed++;
                Log($"Posted a {group.preset} {label} flyer in {address.name}");
            }

            return placed;
        }

        private static List<GroupsController.SocialGroup> GetAdvertisableGroups(GroupsController controller, InteractablePreset flyer)
        {
            var output = new List<GroupsController.SocialGroup>();

            foreach (var group in controller.groups)
            {
                GroupPreset preset;
                if (!Toolbox.Instance.groupsDictionary.TryGetValue(group.preset, out preset)) continue;
                if (preset == null || preset.groupType != GroupPreset.GroupType.interestGroup) continue;
                if (!preset.enableMeetUps || !UsesFlyer(preset, flyer)) continue;
                if (group.GetMeetingPlace() == null) continue;

                output.Add(group);
            }

            return output;
        }

        // Only clubs whose preset was patched to carry the flyer get extra copies, which keeps story
        // groups like the Red Gums off public noticeboards.
        private static bool UsesFlyer(GroupPreset preset, InteractablePreset flyer)
        {
            foreach (var clue in preset.clues)
            {
                if (clue.preset == flyer) return true;
            }

            return false;
        }

        // CreateGroups runs in the generateRelationships stage, before generateInteriors places any
        // furniture, so there are no noticeboards to find yet. Select venues by room configuration and
        // let the address placement pool resolve the actual slot once interiors exist.
        private static List<NewAddress> GetVenues(HashSet<string> roomConfigurations)
        {
            var output = new List<NewAddress>();

            foreach (var address in CityData.Instance.addressDirectory)
            {
                if (address == null) continue;

                foreach (var room in address.rooms)
                {
                    if (room.preset == null || !roomConfigurations.Contains(room.preset.name)) continue;

                    output.Add(address);
                    break;
                }
            }

            return output;
        }

        private static GroupsController.SocialGroup PickGroupForAddress(List<GroupsController.SocialGroup> groups, Dictionary<int, int> placedPerGroup, int cap, NewAddress address)
        {
            var candidates = new List<GroupsController.SocialGroup>();

            foreach (var group in groups)
            {
                if (placedPerGroup.ContainsKey(group.id) && placedPerGroup[group.id] >= cap) continue;
                if (group.GetMeetingPlace() == address) continue;

                candidates.Add(group);
            }

            if (candidates.Count <= 0) return null;

            return candidates[Toolbox.Instance.SeedRand(0, candidates.Count)];
        }
    }
}
