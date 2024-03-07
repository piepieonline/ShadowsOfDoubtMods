using BepInEx;
using UnityEngine;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppSystem;
using SOD.Common.Extensions;
using System.Linq;
using UniverseLib;
using BepInEx.Configuration;
using SOD.Common;
using SOD.Common.Helpers;
using System.Text.Json;
using System.IO;

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace ApartmentRent
{
    /* TODO
     *  Uses SODCommon, which doesn't work on Mono
     *  
     *  Somehow list the amount of rent and due dates for the player?
    */


    /*
     * Debug helpers:
ApartmentRent.ApartmentRentPlugin.GiveMoney(1300);
// ApartmentRent.ApartmentRentPlugin.ActionRentDue();
// ApartmentRent.ApartmentRentPlugin.PayToUnlock(Player.Instance.apartmentsOwned[0]);
    */

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
#if MONO
    public class ApartmentRentPlugin : BaseUnityPlugin
#elif IL2CPP
    public class ApartmentRentPlugin : BasePlugin
#endif
    {
        enum RentalOwingFrequencyChoices
        {
            Hourly,
            Daily,
            Weekly,
            Monthly
        }

        private static ConfigEntry<RentalOwingFrequencyChoices> RentalOwingFrequency;
        private static ConfigEntry<float> RentalPriceScalingFactor;
        private static ConfigEntry<int> MissedPaymentGraceAmount;
        private static ConfigEntry<bool> OweBackpayToUnlock;

        private static ApartmentRentPlugin instance;

        public static ManualLogSource PluginLogger;
#if MONO
        private void Awake()
        {
            PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif
            // Plugin startup logic

            instance = this;

            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");

            RentalOwingFrequency = Config.Bind("General", "How often should rent be charged?", RentalOwingFrequencyChoices.Weekly);
            RentalPriceScalingFactor = Config.Bind("General", "What percentage of the inital purchase cost should be charged as rent? (1 is 100% of the inital cost)", 0.1f);
            MissedPaymentGraceAmount = Config.Bind("General", "How many payments can be missed before the apartment is closed?", 1);
            OweBackpayToUnlock = Config.Bind("General", "Should payments keep being owed while locked out?", true);

            SOD.Common.Lib.Time.OnHourChanged += TimePassedCheck;
            SOD.Common.Lib.Time.OnDayChanged += TimePassedCheck;
            SOD.Common.Lib.Time.OnMonthChanged += TimePassedCheck;

            SOD.Common.Lib.SaveGame.OnAfterSave += OnAfterSave;
            SOD.Common.Lib.SaveGame.OnAfterLoad += OnAfterLoad;
        }

        // Debug helper
        public static void GiveMoney(int amount)
        {
            GameplayController.Instance.AddMoney(amount, false, $"Added money");
        }

        public static List<int> foreclosedAddresses = new List<int>();
        public static Dictionary<int, bool> foreclosedDoorIDs = new Dictionary<int, bool>();
        public static Dictionary<int, int> missedPayments = new Dictionary<int, int>();
        public static Dictionary<int, List<Interactable>> foreclosedInteractables = new Dictionary<int, List<Interactable>>();

        private static GameObject ApartmentClosedNoteRepayButtonTemplate;

        [HarmonyPatch(typeof(MainMenuController), nameof(MainMenuController.Start))]
        internal class MainMenuController_Start
        {
            [HarmonyPostfix]
            internal static void Postfix()
            {
                var credBut = GameObject.Find("MenuCanvas").transform.Find("MainMenu/MainMenuPanel/Credits");

                if (credBut != null)
                {
                    ApartmentClosedNoteRepayButtonTemplate = GameObject.Instantiate(credBut).gameObject;

                    var comp = ApartmentClosedNoteRepayButtonTemplate.GetComponent<UnityEngine.UI.Button>();
                    UnityEngine.Object.DestroyImmediate(comp);
                }
            }
        }

        private void TimePassedCheck(object sender, SOD.Common.Helpers.TimeChangedArgs args)
        {
            var rentalOwingFrequency = RentalOwingFrequency.Value;
            if (args.IsDayChanged && rentalOwingFrequency == RentalOwingFrequencyChoices.Daily)
            {
                ActionRentDue();
            }
            else if (args.IsDayChanged && rentalOwingFrequency == RentalOwingFrequencyChoices.Weekly)
            {
                if (args.Current.Day == 0)
                    ActionRentDue();
            }
            else if (args.IsMonthChanged && rentalOwingFrequency == RentalOwingFrequencyChoices.Monthly)
            {
                ActionRentDue();
            }
            else if (args.IsHourChanged && rentalOwingFrequency == RentalOwingFrequencyChoices.Hourly)
            {
                ActionRentDue();
            }
        }

        public static void ActionRentDue()
        {
            foreach (var apartment in Player.Instance.apartmentsOwned)
            {
                var rentalPrice = Mathf.RoundToInt(apartment.GetPrice(false) * RentalPriceScalingFactor.Value);

                var isForeclosed = foreclosedAddresses.Contains(apartment.residenceNumber);
                var playerHasMoney = GameplayController.Instance.money >= rentalPrice;

                if (playerHasMoney)
                {
                    // Just take the rent as normal, make sure nothing is marked as foreclosed
                    GameplayController.Instance.AddMoney(-rentalPrice, false, $"Rent paid for {apartment.name}");

                    InterfaceController.Instance.NewGameMessage(
                        newType: InterfaceController.GameMessageType.notification,
                        newNumerical: 0,
                        newMessage: $"Apartment rent paid: {apartment.name} - {rentalPrice}",
                        newIcon: InterfaceControls.Icon.door
                    );

                }
                else if (isForeclosed)
                {
                    // Keep track of how many payments were missed
                    missedPayments[apartment.residenceNumber]++;
                }
                else if (!isForeclosed)
                {
                    // No money, not foreclosed... So close it

                    // Don't remove access, just keep the door shut no matter what
                    // Technically, someone could get access via the vents, but that's probably fine
                    if (!missedPayments.ContainsKey(apartment.residenceNumber))
                    {
                        missedPayments[apartment.residenceNumber] = 0;
                    }
                    missedPayments[apartment.residenceNumber]++;

                    if (missedPayments[apartment.residenceNumber] > MissedPaymentGraceAmount.Value)
                    {
                        foreclosedAddresses.Add(apartment.residenceNumber);
                        LockOutApartment(apartment);
                        InterfaceController.Instance.NewGameMessage(
                            newType: InterfaceController.GameMessageType.notification,
                            newNumerical: 0,
                            newMessage: $"Rental payments missed, apartment locked out: {apartment.name}",
                            newIcon: InterfaceControls.Icon.door
                        );
                    }
                    else
                    {
                        InterfaceController.Instance.NewGameMessage(
                            newType: InterfaceController.GameMessageType.notification,
                            newNumerical: 0,
                            newMessage: $"Rental payments missed, {MissedPaymentGraceAmount.Value - (missedPayments[apartment.residenceNumber] - 2)} grace periods remain",
                            newIcon: InterfaceControls.Icon.door
                        );
                    }
                }
            }
        }

        private static void LockOutApartment(NewAddress apartment)
        {
            // Don't lock while a player is inside
            if(Player.Instance.currentGameLocation.thisAsAddress == apartment)
            {
                return;
            }

            foreach (NewNode.NodeAccess entrance in apartment.entrances)
            {
                if (entrance?.door != null && !foreclosedDoorIDs.GetValueOrDefault(entrance.door.GetInstanceID(), false))
                {
                    // entrance.door.SetLocked(true, null, false);
                    entrance.door.SetOpen(0, null, true);

                    var wedgeWorldPos = entrance.door.gameObject.transform.TransformPoint(new Vector3(0, 0, -0.11f));

                    // Create door wedge
                    var wedge = InteractableCreator.Instance.CreateWorldInteractable(
                        InteriorControls.Instance.doorWedge,
                        Player.Instance,
                        null,
                        null,
                        wedgeWorldPos,
                        entrance.door.peekInteractable.wEuler,
                        null,
                        null
                    );
                    wedge.locked = true;
                    entrance.door.SetJammed(true, wedge, true);

                    var note1WorldPos = entrance.door.gameObject.transform.TransformPoint(new Vector3(0, 1.5f, -0.11f));
                    var note2WorldPos = entrance.door.gameObject.transform.TransformPoint(new Vector3(0, 1.5f, 0.01f));

                    var note1 = InteractableCreator.Instance.CreateWorldInteractable(
                        InteriorControls.Instance.note,
                        Player.Instance,
                        null,
                        null,
                        note1WorldPos,
                        entrance.door.peekInteractable.wEuler + new Vector3(90, 0, 0),
                        null,
                        null,
                        "fdf6930b-0c20-434f-a181-dd4975944331"
                    );

                    var note2 = InteractableCreator.Instance.CreateWorldInteractable(
                        InteriorControls.Instance.note,
                        Player.Instance,
                        null,
                        null,
                        note2WorldPos,
                        entrance.door.peekInteractable.wEuler + new Vector3(270, 0, 0),
                        null,
                        null,
                        "fdf6930b-0c20-434f-a181-dd4975944331"
                    );

                    foreclosedInteractables[apartment.residenceNumber] = new List<Interactable>() { wedge, note1, note2 };
                    foreclosedDoorIDs[entrance.door.GetInstanceID()] = true;
                }
            }
        }

        public static void PayToUnlock(NewAddress apartment)
        {
            PluginLogger.LogInfo($"PayToUnlock");

            if (foreclosedAddresses.Contains(apartment.residenceNumber))
            {
                var owingAmount = Mathf.RoundToInt(apartment.GetPrice(false) * RentalPriceScalingFactor.Value) * (OweBackpayToUnlock.Value ? missedPayments[apartment.residenceNumber] : 1);

                if (GameplayController.Instance.money >= owingAmount)
                {
                    // Just take the rent as normal, make sure nothing is marked as foreclosed
                    GameplayController.Instance.AddMoney(-owingAmount, false, $"Backpaid rent owing for {apartment.name}");

                    InterfaceController.Instance.NewGameMessage(
                        newType: InterfaceController.GameMessageType.notification,
                        newNumerical: 0,
                        newMessage: $"Apartment unlocked: {apartment.name} - {owingAmount}",
                        newIcon: InterfaceControls.Icon.door
                    );

                    missedPayments[apartment.residenceNumber] = 0;
                    foreclosedAddresses.Remove(apartment.residenceNumber);

                    foreach (NewNode.NodeAccess entrance in apartment.entrances)
                    {
                        if (entrance?.door != null)
                        {
                            entrance.door.SetJammed(false);
                            foreclosedDoorIDs.Remove(entrance.door.GetInstanceID());
                        }
                    }

                    foreach (var interactable in foreclosedInteractables[apartment.residenceNumber])
                    {
                        interactable.SafeDelete();
                    }
                    foreclosedInteractables.Remove(apartment.residenceNumber);
                }
            }
        }

        // Prevent foreclosed doors from opening
        [HarmonyPatch(typeof(NewDoor), nameof(NewDoor.Barge))]
        internal class NewDoor_Barge
        {
            [HarmonyPrefix]
            internal static bool Prefix(NewDoor __instance, Actor barger)
            {
                if (foreclosedDoorIDs.ContainsKey(__instance.GetInstanceID()) && foreclosedDoorIDs[__instance.GetInstanceID()] && barger.isPlayer)
                {
                    PluginLogger.LogInfo($"Barging blocked: Foreclosed!");
                    AudioController.Instance.PlayWorldOneShot(AudioControls.Instance.bargeDoorContact, barger, barger.currentNode, barger.lookAtThisTransform.position);
                    Player.Instance.TransformPlayerController(GameplayControls.Instance.bargeDoorFail, null, __instance.doorInteractable, null);
                    InterfaceController.Instance.NewGameMessage(InterfaceController.GameMessageType.notification, 0, "The door is reinforced and jammed shut...", InterfaceControls.Icon.door);
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(ActionController), nameof(ActionController.OpenDoor))]
        internal class ActionController_OpenDoor
        {
            [HarmonyPrefix]
            internal static bool Prefix(Interactable what, Actor who)
            {
                if (what.objectRef != null)
                {
                    NewDoor newDoor = ((dynamic)what.objectRef).Cast<NewDoor>();

                    if (foreclosedDoorIDs.ContainsKey(newDoor.GetInstanceID()) && foreclosedDoorIDs[newDoor.GetInstanceID()] && who.isPlayer)
                    {
                        PluginLogger.LogInfo($"Door foreclosed, blocking door from opening!");
                        InterfaceController.Instance.NewGameMessage(InterfaceController.GameMessageType.notification, 0, "The door is jammed shut...", InterfaceControls.Icon.door);
                        return false;
                    }
                }

                return true;
            }
        }

        // Custom "pay missed rent" button in the evidence window
        [HarmonyPatch(typeof(InterfaceController), nameof(InterfaceController.SpawnWindow))]
        internal class InterfaceController_SpawnWindow
        {
            [HarmonyPostfix]
            internal static void Postfix(Interactable passedInteractable, InfoWindow __result)
            {
                if (passedInteractable == null)
                {
                    return;
                }

                foreach ((var residenceNumber, var interactables) in foreclosedInteractables)
                {
                    foreach (var interactable in interactables)
                    {
                        if (interactable.id == passedInteractable.id)
                        {
                            PluginLogger.LogInfo($"Found the interactable door note!");

                            var apartment = Player.Instance.apartmentsOwned.Where(apartment => apartment.residenceNumber == residenceNumber).First();

                            var owingAmount = Mathf.RoundToInt(apartment.GetPrice(false) * RentalPriceScalingFactor.Value) * (OweBackpayToUnlock.Value ? missedPayments[residenceNumber] : 1);

                            var button = GameObject.Instantiate(
                                ApartmentClosedNoteRepayButtonTemplate,
                                GameObject.Find("GameCanvas/WindowCanvas/Note/Page/Scroll View/Viewport/Summary/Page").transform
                            ).gameObject;

                            button.GetComponent<RectTransform>().anchoredPosition += new Vector2(15, 160);
                            button.transform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>().SetText($"Repay and unlock ${owingAmount}");

                            if (GameplayController.Instance.money < owingAmount)
                            {
                                // TODO: Disable button, don't just have no click function
                            }
                            else
                            {
                                var comp = button.gameObject.AddComponent<UnityEngine.UI.Button>();
                                comp.onClick.RemoveAllListeners();
                                comp.onClick.AddListener((System.Action)(() =>
                                {
                                    PayToUnlock(apartment);
                                    __result.CloseWindow();
                                }));
                            }
                        }
                    }
                }
            }
        }

        // Don't lock the apartment if the player is inside it
        [HarmonyPatch(typeof(Player), nameof(Player.OnGameLocationChange))]
        internal class Player_OnGameLocationChange
        {
            [HarmonyPostfix]
            internal static void Prefix()
            {
                if(Player.Instance?.previousGameLocation?.thisAsAddress != null && foreclosedAddresses.Contains(Player.Instance.previousGameLocation.thisAsAddress.residenceNumber))
                {
                    bool previouslyInApartment = Player.Instance.apartmentsOwned.Where(apartment => apartment == Player.Instance.previousGameLocation.thisAsAddress).FirstOrDefault() != null;
                    bool currentlyInApartment = Player.Instance.apartmentsOwned.Where(apartment => apartment == Player.Instance.currentGameLocation.thisAsAddress).FirstOrDefault() != null;

                    if (previouslyInApartment && !currentlyInApartment)
                    {
                        LockOutApartment(Player.Instance.previousGameLocation.thisAsAddress);
                    }
                }
            }
        }
        
        // Reloading from saved interactables
        private void OnAfterLoad(object sender, SaveGameArgs e)
        {
            string path = GetSavePath(e.FilePath);

            var saveFileContent = JsonSerializer.Deserialize<RentSaveFile>(File.ReadAllText(path));

            foreach (var addressSaveFile in saveFileContent.apartmentSaveFiles)
            {
                NewAddress playerApartment = Player.Instance.apartmentsOwned.Where(apartment => apartment.residenceNumber == addressSaveFile.residenceNumber).FirstOrDefault();

                if (playerApartment == null) { continue; }

                foreclosedAddresses.Add(addressSaveFile.residenceNumber);
                missedPayments[addressSaveFile.residenceNumber] = addressSaveFile.missedPayments;

                foreclosedInteractables[addressSaveFile.residenceNumber] = new List<Interactable>();
                foreach (var interactableId in addressSaveFile.interactableIds)
                {
                    foreclosedInteractables[addressSaveFile.residenceNumber].Add(CityData.Instance.savableInteractableDictionary[interactableId]);
                }

                foreach (var entrance in playerApartment.entrances)
                {
                    foreclosedDoorIDs[entrance.door.GetInstanceID()] = true;
                }
            }
        }

        private void OnAfterSave(object sender, SaveGameArgs e)
        {
            string path = GetSavePath(e.FilePath);

            var saveFileContent = new RentSaveFile();

            foreach (var addressResidenceNumber in foreclosedAddresses)
            {
                var addressSaveFile = new ApartmentSaveFile()
                {
                    residenceNumber = addressResidenceNumber,
                    missedPayments = missedPayments.ContainsKey(addressResidenceNumber) ? missedPayments[addressResidenceNumber] : 0
                };

                foreach (var interactable in foreclosedInteractables[addressResidenceNumber])
                {
                    addressSaveFile.interactableIds.Add(interactable.id);
                }

                saveFileContent.apartmentSaveFiles.Add(addressSaveFile);
            }

            var json = JsonSerializer.Serialize(saveFileContent, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private string GetSavePath(string savePath)
        {
            var hash = Lib.SaveGame.GetUniqueString(savePath);
            return Lib.SaveGame.GetSavestoreDirectoryPath(System.Reflection.Assembly.GetExecutingAssembly(), $"ApartmentRental_{hash}.json");
        }
    }
}