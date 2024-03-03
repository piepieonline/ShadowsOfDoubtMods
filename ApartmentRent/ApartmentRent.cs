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

#if MONO
using BepInEx.Unity.Mono;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
#endif

namespace ApartmentRent
{
    /* TODO
     *  Saving and loading 
     *  Uses SODCommon, which doesn't work on Mono
     *  Config
     *  
     *  What if rent comes due while inside
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

        private static RentalOwingFrequencyChoices RentalOwingFrequency = RentalOwingFrequencyChoices.Hourly;
        private static int RentalPriceScalingFactor = 1;
        private static int MissedPaymentGraceAmount = 0;
        private static bool OweBackpayToUnlock = true;

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

            SOD.Common.Lib.Time.OnHourChanged += TimePassedCheck;
            SOD.Common.Lib.Time.OnDayChanged += TimePassedCheck;
            SOD.Common.Lib.Time.OnMonthChanged += TimePassedCheck;

            SOD.Common.Lib.SaveGame.OnAfterLoad += AfterGameStarted;
            SOD.Common.Lib.SaveGame.OnAfterNewGame += AfterGameStarted;
        }

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
                    PluginLogger.LogInfo($"credBut");

                    ApartmentClosedNoteRepayButtonTemplate = GameObject.Instantiate(credBut).gameObject;

                    var comp = ApartmentClosedNoteRepayButtonTemplate.GetComponent<UnityEngine.UI.Button>();
                    UnityEngine.Object.DestroyImmediate(comp);
                }
            }
        }

    private void AfterGameStarted(object sender, System.EventArgs args)
        {
            // "MenuCanvas/MainMenu/MainMenuPanel/Credits/";
            PluginLogger.LogInfo($"After game started");
            // var credBut = Resources.FindObjectsOfTypeAll<ButtonController>().Where(ele => ele.name == "Credits" && ele.transform.parent.name == "MainMenu").First();
            /*
            var credBut = GameObject.Find("MenuCanvas").transform.Find("MainMenu").Find("MainMenuPanel").Find("Credits");

            if (credBut != null)
            {
                PluginLogger.LogInfo($"credBut");
                credBut.GetComponent<UnityEngine.UI.Button>().onClick.RemoveAllListeners();
                credBut.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
                {
                    PayToUnlock(Player.Instance.apartmentsOwned[0]);
                });
            }
            */
            // GameObject.Find("MenuCanvas/MainMenu/MainMenuPanel/Credits").
        }

        private void TimePassedCheck(object sender, SOD.Common.Helpers.TimeChangedArgs args)
        {
            if(args.IsDayChanged && RentalOwingFrequency == RentalOwingFrequencyChoices.Daily)
            {
                ActionRentDue();
            }
            else if (args.IsDayChanged && RentalOwingFrequency == RentalOwingFrequencyChoices.Weekly)
            {
                if (args.Current.Day == 0)
                    ActionRentDue();
            }
            else if (args.IsMonthChanged && RentalOwingFrequency == RentalOwingFrequencyChoices.Monthly)
            {
                ActionRentDue();
            }
            else if (args.IsHourChanged && RentalOwingFrequency == RentalOwingFrequencyChoices.Hourly)
            {
                ActionRentDue();
            }
        }

        public static void ActionRentDue()
        {
            foreach (var apartment in Player.Instance.apartmentsOwned)
            {
                var rentalPrice = Mathf.RoundToInt(apartment.GetPrice(false) * RentalPriceScalingFactor);

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
                else if(!isForeclosed)
                {
                    // No money, not foreclosed... So close it

                    // Don't remove access, just keep the door shut no matter what
                    // Technically, someone could get access via the vents, but that's probably fine
                    if(!missedPayments.ContainsKey(apartment.residenceNumber))
                    {
                        missedPayments[apartment.residenceNumber] = 0;
                    }
                    missedPayments[apartment.residenceNumber]++;

                    if (missedPayments[apartment.residenceNumber] > MissedPaymentGraceAmount)
                    {
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
                            newMessage: $"Rental payments missed, {MissedPaymentGraceAmount - (missedPayments[apartment.residenceNumber] - 2)} grace periods remain",
                            newIcon: InterfaceControls.Icon.door
                        );
                    }
                }
            }
        }

        private static void LockOutApartment(NewAddress apartment)
        {
            foreach (NewNode.NodeAccess entrance in apartment.entrances)
            {
                foreclosedAddresses.Add(apartment.residenceNumber);

                if (entrance?.door != null)
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
                        entrance.door.doorInteractable.wEuler,
                        null,
                        null
                    );
                    wedge.locked = true;
                    entrance.door.SetJammed(true, wedge, true);

                    var noteWorldPos = entrance.door.gameObject.transform.TransformPoint(new Vector3(0, 1.5f, -0.11f));

                    var note = InteractableCreator.Instance.CreateWorldInteractable(
                        InteriorControls.Instance.note,
                        Player.Instance,
                        null,
                        null,
                        noteWorldPos,
                        entrance.door.doorInteractable.wEuler + new Vector3(90, 0, 0),
                        null,
                        null,
                        "fdf6930b-0c20-434f-a181-dd4975944331"
                    );

                    foreclosedInteractables[apartment.residenceNumber] = new List<Interactable>() { wedge, note };
                    foreclosedDoorIDs[entrance.door.GetInstanceID()] = true;
                }
            }
        }

        public static void PayToUnlock(NewAddress apartment)
        {
            PluginLogger.LogInfo($"PayToUnlock");

            if (foreclosedAddresses.Contains(apartment.residenceNumber))
            {
                var owingAmount = Mathf.RoundToInt(apartment.GetPrice(false) * RentalPriceScalingFactor) * (OweBackpayToUnlock ? missedPayments[apartment.residenceNumber] : 1);

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

                    foreach(var interactable in foreclosedInteractables[apartment.residenceNumber])
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
                if(what.objectRef != null)
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

        // Enable the tiredness effect
        /*
        // Broken, causing startup CTD?
        [HarmonyPatch(typeof(Human), nameof(Human.AddWellRested))]
        internal class Human_AddAlertness
        {
            [HarmonyPostfix]
            internal static void Postfix(Human __instance, float addVal)
            {
                if (__instance.isPlayer && __instance.alertness < 0.1f && addVal < 0)
                {
                    __instance.AddEnergy(addVal / 2f); // Called twice per frame (bug?), so halve it
                }
            }
        }
        */

        // Alternate method to get past the crashing hook
        [HarmonyPatch(typeof(CitizenBehaviour), nameof(CitizenBehaviour.GameWorldCheck))]
        internal class CitizenBehaviour_GameWorldCheck
        {
            [HarmonyPrefix]
            internal static void Prefix(CitizenBehaviour __instance, ref float __state)
            {
                __state = SessionData.Instance.gameTime - __instance.timeOnLastGameWorldUpdate;
            }

            [HarmonyPostfix]
            internal static void Postfix(ref float __state)
            {
                // Copied from decomp
                if (Player.Instance.spendingTimeMode && InteractionController.Instance.lockedInInteraction != null && InteractionController.Instance.lockedInInteraction.preset.specialCaseFlag == InteractablePreset.SpecialCase.sleepPosition)
                {}
                else
                {
                    if (!Game.Instance.disableSurvivalStatusesInStory || !Toolbox.Instance.IsStoryMissionActive(out Chapter _, out int _))
                    {
                        float addVal = GameplayControls.Instance.playerTirednessRate * -__state;
                        if (Player.Instance.alertness < 0.1f && addVal < 0)
                        {
                            Player.Instance.AddEnergy(addVal); // Called twice per frame (bug?), so halve it
                        }
                    }
                }
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

                PluginLogger.LogInfo($"Opened interactable: {passedInteractable.name}");

                foreach((var residenceNumber, var interactables) in foreclosedInteractables)
                {
                    foreach(var interactable in interactables)
                    {
                        if(interactable.id == passedInteractable.id)
                        {
                            PluginLogger.LogInfo($"Found the interactable door note!");

                            var apartment = Player.Instance.apartmentsOwned.Where(apartment => apartment.residenceNumber == residenceNumber).First();

                            var owingAmount = Mathf.RoundToInt(apartment.GetPrice(false) * RentalPriceScalingFactor) * (OweBackpayToUnlock ? missedPayments[residenceNumber] : 1);

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
                                comp.onClick.AddListener((System.Action)(() => {
                                    PayToUnlock(apartment);
                                    __result.CloseWindow();
                                }));
                            }
                        }
                    }
                }
            }
        }
    }
}