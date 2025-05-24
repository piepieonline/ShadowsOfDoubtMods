using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Linq;

#if MONO
using BepInEx.Unity.Mono;
using System.Collections.Generic;
#elif IL2CPP
using BepInEx.Unity.IL2CPP;
using Il2CppSystem.Collections.Generic;
#endif

using UnityEngine;
using AssetBundleLoader;
using UniverseLib;
using BepInEx.Configuration;
using System.Reflection;

namespace DebugMod
{
#if MONO
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class DebugModPlugin : BaseUnityPlugin
#elif IL2CPP
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class DebugModPlugin : BasePlugin
#endif
    {
        public static ManualLogSource PluginLogger;

        public static UniverseLib.AssetBundle customAssets;
        public static UnityEngine.Object[] allCustomAssets;

        public static ConfigEntry<bool> EnableGameLog;

        public static ConfigEntry<string> GameLogFilter;
        public static ConfigEntry<string> GameLogInverseFilter;
        
        public static ConfigEntry<string> ActorLogName;
        public static ConfigEntry<LoggingHelpers.HumanDebugOverloaded> ActorLogCategory;

#if MONO
    public void Awake()
    {
        PluginLogger = Logger;
#elif IL2CPP
        public override void Load()
        {
            PluginLogger = Log;
#endif
            if (!Config.Bind("General", "Enabled", true).Value)
            {
                PluginLogger.LogInfo($"Plugin {DebugMod.MyPluginInfo.PLUGIN_GUID} is disabled.");
                return;
            }

            EnableGameLog = Config.Bind("Game Logging", "Enable Game.Log output", false);
            GameLogFilter = Config.Bind("Game Logging", "Filter Game.Log output (Must contain)", "");
            GameLogInverseFilter = Config.Bind("Game Logging", "Filter Game.Log output (Must not contain)", "");

            ActorLogName = Config.Bind("Actor Logging", "Log actions for this actor name", "");
            ActorLogCategory = Config.Bind("Actor Logging", "Log these actions", LoggingHelpers.HumanDebugOverloaded.none);

            EnableGameLog.SettingChanged += EnableGameLog_SettingChanged;

            // Plugin startup logic
            PluginLogger.LogInfo($"Plugin {DebugMod.MyPluginInfo.PLUGIN_GUID} is loaded!");
            var harmony = new Harmony($"{DebugMod.MyPluginInfo.PLUGIN_GUID}");
            harmony.PatchAll();
            PluginLogger.LogInfo($"Plugin {DebugMod.MyPluginInfo.PLUGIN_GUID} is patched!");

            // ClassInjector.RegisterTypeInIl2Cpp<MonoTest>();

            Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<CanvasScreenShot>();

            PluginLogger.LogInfo($"Plugin {DebugMod.MyPluginInfo.PLUGIN_GUID} has added custom types!");

        }

        private static void EnableGameLog_SettingChanged(object sender, System.EventArgs e)
        {
            Game.Instance.collectDebugData = EnableGameLog.Value;
            Game.Instance.printDebug = EnableGameLog.Value;
        }

        // Filter resolutions
        [HarmonyPatch(typeof(DropdownController), "AddOptions")]
        public class DropdownController_AddOptions
        {
            public static void Prefix(DropdownController __instance, ref List<string> newOptions)
            {
                if (__instance.name == "ScreenResolutionDropdown")
                {
                    newOptions.Clear();
                    newOptions.Add("1920 x 1080 @ 60Hz");
                    newOptions.Add("2560 x 1440 @ 60Hz");
                }
            }
        }

        /* 
         * not working
        // Skip splash screens
        [HarmonyPatch(typeof(SplashController), nameof(SplashController.Update))]
        public class SplashController_Awake
        {
            public static void Prefix(ref SplashController __instance)
            {
                __instance.splashes.Clear();
                __instance.fadeOut = true;
            }
        }
        */

        /*
        // Not running early enough as a patch
        [HarmonyPatch(typeof(PlayerPrefsController), "GetSettingInt")]
        public class PlayerPrefsController_GetSettingInt
        {
            public static bool Prefix(ref int __result, string id)
            {
                if(id == "width")
                {
                    __result = 1920;
                    return false;
                }
                else if (id == "height")
                {
                    __result = 1080;
                    return false;
                }
                return true;
            }
        }
        */

        /*
         [HarmonyPatch(typeof(NewAIGoal), nameof(NewAIGoal.UpdateNextGroupTimes))]
         public class NewAIGoal_UpdateNextGroupTimes
         {
             public static void Postfix(NewAIGoal __instance)
             {
                 DialogAdditionPlugin.PluginLogger.LogInfo($"UpdateNextGroupTimes: {__instance?.passedGroup?.id} : {__instance?.passedGroup?.preset} @ {__instance?.triggerTime}");
             }
         }
         */

        // [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.AITick))]
        public class NewAIController_AITick
        {
            public static void Postfix(NewAIController __instance)
            {
                if (__instance != null && __instance.currentGoal != null && __instance.currentGoal.name.Contains("MeetUpEvent"))
                {
                    PluginLogger.LogInfo($"{__instance.currentGoal.name}: {__instance.currentGoal.triggerTime} @ {__instance?.human?.citizenName} at {__instance?.currentGoal?.gameLocation?.name}");
                    __instance.human.outline.SetOutlineActive(true);
                }
                else
                {
                    __instance.human.outline.SetOutlineActive(false);
                }
            }
        }

        /*
        [HarmonyPatch(typeof(InterfaceController), nameof(InterfaceController.ToggleNotebook))]
        public class InterfaceController_ToggleNotebook
        {
            public static void Postfix()
            {
                PluginLogger.LogInfo("Resuming after opening notebook");
                SessionData.Instance.ResumeGame();
            }
        }
        */  
        
        [HarmonyPatch(typeof(ComputerController), nameof(ComputerController.OnPlayerControlChange))]
        public class ComputerController_OnPlayerControlChange
        {
            public static void Postfix(ComputerController __instance)
            {
                if(__instance.playerControlled && __instance.appLoaded && (__instance.currentApp.name == "GovDatabase" || __instance.currentApp.name == "EmployeeDatabase"))
                    __instance.SetPlayerCrunchingDatabase(true);
            }
        }

        [HarmonyPatch(typeof(InputController), nameof(InputController.Update))]
        public class InputController_Update
        {
            static bool state;

            public static void Prefix(InputController __instance)
            {
                if (__instance.player != null && (__instance.player.GetButtonDown("CaseBoard") || __instance.player.GetButtonDown("Notebook")))
                {
                    state = Player.Instance.autoTravelActive;
                    Player.Instance.autoTravelActive = true;
                }
            }

            public static void Postfix(InputController __instance)
            {
                if (__instance.player != null && (__instance.player.GetButtonDown("CaseBoard") || __instance.player.GetButtonDown("Notebook")))
                {
                    PluginLogger.LogInfo("Resuming after opening notebook");
                    Player.Instance.autoTravelActive = state;
                }
            }
        }

        // [HarmonyPatch]
        public class Toolbox_NewVmailThread
        {
            [HarmonyTargetMethod]
            internal static System.Reflection.MethodBase CalculateMethod()
            {
                PluginLogger.LogInfo("Finding method...");

                var mi = typeof(Toolbox).GetMethods().Where(mi => mi.Name == "NewVmailThread" && mi.GetParameters().Length == 7).First();

                return mi;
            }

            public static bool Prefix(Human from, List<Human> otherParticipiants, string treeID, float timeStamp, int progress, StateSaveData.CustomDataSource overrideDataSource, int newDataSourceID)
            {
                Human to1 = (Human)null;
                Human to2 = (Human)null;
                Human to3 = (Human)null;
                List<Human> cc = new List<Human>();
                if (otherParticipiants.Count > 1)
                    to1 = otherParticipiants[1];
                if (otherParticipiants.Count > 2)
                    to2 = otherParticipiants[2];
                if (otherParticipiants.Count > 3)
                    to3 = otherParticipiants[3];
                if (otherParticipiants.Count > 4)
                {
                    for (int index = 4; index < otherParticipiants.Count; ++index)
                        cc.Add(otherParticipiants[index]);
                }

                /*
                if(treeID == "e28209c7-c53c-4c13-8f86-c76fafbabf38")
                {
                    to1 = MurderController.Instance.currentVictim;
                    to2 = MurderController.Instance.currentMurderer;
                }
                */


                Toolbox.Instance.NewVmailThread(from, to1, to2, to3, cc, treeID, timeStamp, progress, overrideDataSource, newDataSourceID);

                return false;
            }
        }        

        // [HarmonyPatch(typeof(SideJob), nameof(SideJob.AddDialogOption))]
        public class SideJob_AddDialogOption
        {
            public static void Prefix(SideJob __instance, Human person, Evidence.DataKey key, DialogPreset newPreset)
            {
                PluginLogger.LogInfo($"DebugMod: {newPreset.msgID}");
            }
        }

        
        public static void CreateCorkboard(GameObject parentObj)
        {
            //First, we create an array of vector3's. Each vector3 will 
            //represent one vertex in our mesh. Our shape will be a half 
            //cube (probably the simplest 3D shape we can make.

            var g = new GameObject("CorkboardContent");

            var newVertices = new Vector3[4];

            newVertices[0] = new Vector3(0, 0, 0);
            newVertices[1] = new Vector3(0.8f, 0, 0);
            newVertices[2] = new Vector3(0, 0.6f, 0);
            newVertices[3] = new Vector3(0.8f, 0.6f, 0);

            //Next, we create an array of integers which will represent 
            //triangles. Triangles are built by taking integers in groups of 
            //three, with each integer representing a vertex from our array of 
            //vertices. Note that the integers are in a certain order. The order 
            //of integers determines the normal of the triangle. In this case, 
            //connecting 021 faces the triangle out, while 012 faces the 
            //triangle in.

            var newTriangles = new int[6];

            newTriangles[0] = 0;
            newTriangles[1] = 2;
            newTriangles[2] = 1;

            newTriangles[3] = 0;
            newTriangles[4] = 1;
            newTriangles[5] = 3;


            //We instantiate our mesh object and attach it to our mesh filter
            var mesh = new Mesh();
            var meshFilter = g.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            g.AddComponent<MeshRenderer>();

            //We assign our vertices and triangles to the mesh.
            mesh.vertices = newVertices;
            mesh.triangles = newTriangles;
        }

        /*
        private delegate bool DLoadImage(System.IntPtr tex, System.IntPtr data, bool markNonReadable);

        private static DLoadImage _iCallLoadImage;

        private static bool LoadImage(Texture2D tex, byte[] data, bool markNonReadable)
        {
            _iCallLoadImage ??= IL2CPP.ResolveICall<DLoadImage>("UnityEngine.ImageConversion::LoadImage");

            var il2CPPArray = (Il2CppStructArray<byte>)data;

            return _iCallLoadImage.Invoke(tex.Pointer, il2CPPArray.Pointer, markNonReadable);
        }
        */


        [HarmonyPatch(typeof(MainMenuController), "Start")]
        public class MainMenuController_Start
        {
            static bool hasInit = false;

            public static void Prefix()
            {
                EnableGameLog_SettingChanged(null, null);

                GameObject.Find("GameController").AddComponent<CanvasScreenShot>();

                return;
                if (hasInit) return;
                hasInit = true;

                // var moddedAssetBundle = UniverseLib.AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "realestatelistingcruncherappbundle"));
                var moddedAssetBundle = BundleLoader.LoadBundle(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "tophat"), stable: true);

                // This is literally the scriptableobject the game stores these in, so that works
                var tophat = moddedAssetBundle.LoadAsset<ClothesPreset>("HatTopHat");
                
                // Toolbox.Instance.ProcessLoadedScriptableObject(tophat);

                foreach(var job in Toolbox.Instance.allJobs)
                {
                    job.workOutfit.Clear();
                    job.workOutfit.Add(tophat);
                }

                PluginLogger.LogInfo("Loading custom asset bundle complete");
            }
        }

        /*
        [HarmonyPatch(typeof(Interactable), nameof(Interactable.SetOwner))]
        public class Interactable_SetOwner
        {
            static bool hasSet = false;
            public static bool Prefix(Interactable __instance, Human newOwner, bool updateName)
            {
                if (newOwner?.name == "John Foster")
                {
                    Logger.LogInfo($"Assigning {__instance.name} ({__instance.id} - {__instance.p}) to {newOwner.name}");

                    if(!hasSet)
                    {
                    UnityExplorer.InspectorManager.Inspect(__instance);
                        // hasSet = true;
                    }

                    if (__instance.name == "")
                        return false;
                }
                return true;
            }
        }
        */

        /*
        [HarmonyPatch]
        internal static class DataCompressionController_CompressAndSaveDataAsync
        {

            [HarmonyTargetMethod]
            internal static Il2CppSystem.Reflection.MethodBase CalculateMethod()
            {
                PluginLogger.LogInfo("Finding method...");

                var mi = typeof(DataCompressionController).GetMethod("CompressAndSaveDataAsync").MakeGenericMethod(typeof(StateSaveData));

                var mi2 = Il2CppSystem.Type.GetType("DataCompressionController").GetMethod("CompressAndSaveDataAsync").MakeGenericMethod(Il2CppSystem.Type.GetType("DataCompressionController"));

                return mi2.;
            }

            internal static void Prefix()
            {
                PluginLogger.LogInfo("Patch was successful");
            }
        }
        */

        public static T FindByTypeAndName<T>(string name) where T : UnityEngine.Object
        {
            if (!typeof(UnityEngine.GameObject).IsAssignableFrom(typeof(T)))
                throw new System.ArgumentException();

            return Resources.FindObjectsOfTypeAll<T>().Where(obj => obj.name == name).FirstOrDefault();
        }

        public static Company FindNearestThatSells(string itemName)
        {
            // return Toolbox.Instance.FindNearestThatSells(FindByTypeAndName<InteractablePreset>(itemName), Resources.FindObjectsOfTypeAll<Player>()[0].currentGameLocation);
            return Toolbox.Instance.FindNearestThatSells(Resources.FindObjectsOfTypeAll<InteractablePreset>().Where(obj => obj.name == itemName).FirstOrDefault(), Resources.FindObjectsOfTypeAll<Player>()[0].currentGameLocation);
        }

        public static void SendVMailToPlayer(string citizenFrom, string treeDDS = "1dcc4503-b39b-4d27-9b6d-264e8dcfabc8")
        {
            var others = new List<Human>();
            others.Add(GameObject.Find("Fred Red").GetComponent<Human>());
            Toolbox.Instance.NewVmailThread(GameObject.Find(citizenFrom).GetComponent<Citizen>(), others, treeDDS, 1, 0);
        }
    }
}