using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using SOD.Common.BepInEx;
using SOD.Common;
using SOD.Common.Helpers;
using UnityEngine;
using static AssetBundleLoader.JsonLoader.NewtonsoftExtensions;
using System.Reflection;
using System.Linq;

namespace Snailpocalypse
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class SnailpocalypsePlugin : PluginController<SnailpocalypsePlugin>
    {
        private static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> DebugLogging;
        public static ConfigEntry<bool> DebugOutlining;

        public static ConfigEntry<bool> DoubleSnailsPerSpawn;
        public static ConfigEntry<float> SnailSizeMultiplier;
        public static ConfigEntry<int> MinutesBetweenSnails;
        public static ConfigEntry<float> SnailSpeed;
        public static ConfigEntry<float> SnailKillRadius;

        public static ConfigEntry<float> SnailGroundOffset;
        
        // Tracks how many in-game minutes remain until the next snail spawn.
        public int RemainingMinutesUntilNextSnail { get; private set; }
        public static List<SnailController> SpawnedSnails = new List<SnailController>();
        
        public override void Load()
        {
            // Plugin startup logic

            Enabled = Config.Bind("General", "Enabled", true, "Is the mod enabled at all? (Game restart required)");
            DebugLogging = Config.Bind("Debugging", "Logging", false, "Is debug logging enabled?");
            DebugOutlining = Config.Bind("Debugging", "Outlining", false, "Are the snails outlined for testing?");
            
            MinutesBetweenSnails = Config.Bind("General", "Minutes between snail spawns", 60, "How many minutes should pass between snail spawns?");
            // DoubleSnailsPerSpawn = Config.Bind("General", "Double snails per spawn", false, "Should the number of spawned snails be doubled per spawn??");
            SnailSizeMultiplier = Config.Bind("General", "Snail size multiplier", 4f);
            SnailGroundOffset = Config.Bind("General", "Offset to re-ground the snail after scaling", 0f);
            SnailSpeed = Config.Bind("General", "Snail speed", 0.175f, "Default: 0.175");
            SnailKillRadius = Config.Bind("General", "Snail kill radius", 0.2f);
            
            if (Enabled.Value)
            {
                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

                var harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}");
                harmony.PatchAll();

                Lib.SaveGame.OnAfterNewGame += OnAfterNewGame;
                Lib.SaveGame.OnAfterSave += OnAfterSave;
                Lib.SaveGame.OnAfterLoad += OnAfterLoad;
                Lib.SaveGame.OnAfterDelete += OnAfterDelete;

                Lib.Time.OnMinuteChanged += OnMinuteChanged;

                SnailSizeMultiplier.SettingChanged += (object sender, EventArgs e) => UpdateSnailSizes();
                DebugOutlining.SettingChanged += (object sender, EventArgs e) => UpdateSnailOutlines();
                SnailSpeed.SettingChanged += (object sender, EventArgs e) => UpdateSnailSpeeds();

                Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is patched!");
            }
        }

        private void UpdateSnailSizes()
        {
            if (DebugLogging.Value)
                Log.LogInfo($"Applying snail size multiplier change: {SnailSizeMultiplier.Value}");
            if (ModifiersController.Instance.currentSnail)
            {
                ModifiersController.Instance.currentSnail.transform.localScale = Vector3.one * SnailSizeMultiplier.Value;
            }
            foreach (var snail in SpawnedSnails)
            {
                snail.transform.localScale = Vector3.one * SnailSizeMultiplier.Value;
            }
        }

        private void UpdateSnailOutlines()
        {
            if (DebugLogging.Value)
                Log.LogInfo($"Applying snail outline change: {DebugOutlining.Value}");
            if (ModifiersController.Instance.currentSnail)
            {
                UpdateSnailOutline(ModifiersController.Instance.currentSnail.gameObject);
            }
            foreach (var snail in SpawnedSnails)
            {
                UpdateSnailOutline(snail.gameObject);
            }
        }
        
        private static void UpdateSnailOutline(GameObject snailObject)
        {
            snailObject.layer = DebugOutlining.Value ? LayerMask.NameToLayer("Outline") : LayerMask.NameToLayer("Ignore Raycast");
        }

        private void UpdateSnailSpeeds()
        {
            if (DebugLogging.Value)
                Log.LogInfo($"Applying snail speed change: {SnailSpeed.Value}");
            if (ModifiersController.Instance.currentSnail)
            {
                UpdateSnailSpeed(ModifiersController.Instance.currentSnail);
            }
            foreach (var snail in SpawnedSnails)
            {
                UpdateSnailSpeed(snail);
            }
        }

        private static void UpdateSnailSpeed(SnailController snail)
        {
            snail.snailSpeed = SnailSpeed.Value;
        }
        
        private void OnAfterNewGame(object sender, EventArgs e)
        {
            SpawnedSnails.Clear();
            RemainingMinutesUntilNextSnail = MinutesBetweenSnails.Value;
        }

        private void OnMinuteChanged(object sender, TimeChangedArgs e)
        {
            // Don't do anything if we are recovering from KO
            if (Player.Instance.playerKOInProgress)
                return;
            
            if (ModifiersController.Instance.snailNemesisModifierEnabled)
            {
                RemainingMinutesUntilNextSnail -= 1;
                if (RemainingMinutesUntilNextSnail <= 0)
                {
                    RemainingMinutesUntilNextSnail = MinutesBetweenSnails.Value;
                    CreateSnail();
                }
            }
        }

        private void OnAfterDelete(object sender, SaveGameArgs e)
        {
            try
            {
                var path = GetSavePath(e.FilePath);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    if (DebugLogging.Value)
                    {
                        Log.LogInfo($"Deleted Snailpocalypse save file: {path}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to delete Snailpocalypse data: {ex}");
            }
        }

        private void OnAfterLoad(object sender, SaveGameArgs e)
        {
            SpawnedSnails.Clear();
            string path = GetSavePath(e.FilePath);

            if(!File.Exists(path))
            {
                return;
            }

            var saveFileContent = NewtonsoftJson.JToken_Parse(File.ReadAllText(path)).ToObject<SnailpocalypseSaveFile>();
            if (saveFileContent == null)
            {
                return;
            }

            RemainingMinutesUntilNextSnail = Mathf.Max(0, saveFileContent.minutesTillNextSnail);

            if (saveFileContent.snails != null)
            {
                foreach (var snailData in saveFileContent.snails)
                {
                    CreateSnail(snailData);
                }
            }

            if (DebugLogging.Value)
            {
                Log.LogInfo($"Loaded {saveFileContent.snails?.Count ?? 0} snail(s). MinutesUntilNextSnail={RemainingMinutesUntilNextSnail}");
            }
        }

        private void OnAfterSave(object sender, SaveGameArgs e)
        {
            try
            {
                var path = GetSavePath(e.FilePath);

                var saveData = new System.Collections.Generic.List<SnailpocalypseSaveFile.SnailSaveData>();

                foreach (var snail in SpawnedSnails)
                {
                    var gameSnailSave = snail.GetSaveData();
                    var data = new SnailpocalypseSaveFile.SnailSaveData
                    {
                        pos = new SnailpocalypseSaveFile.SnailSaveData.SerializedVector3(
                            snail.transform.position
                        ),
                        rot = new SnailpocalypseSaveFile.SnailSaveData.SerializedQuaternion(
                            snail.transform.rotation
                        ),
                        // TODO: Map inAirVent and duct
                        inAirVent = gameSnailSave.inAirVent,
                        duct = gameSnailSave.duct
                    };

                    saveData.Add(data);
                }

                var save = new SnailpocalypseSaveFile
                {
                    minutesTillNextSnail = RemainingMinutesUntilNextSnail,
                    snails = saveData
                };
                
                var json = NewtonsoftJson.JObject_FromObject(save).ToString();
                File.WriteAllText(path, json);

                if (DebugLogging.Value)
                {
                    Log.LogInfo($"Saved {save.snails.Count} snail(s). MinutesUntilNextSnail={save.minutesTillNextSnail} -> {path}");
                    // Log.LogInfo($"Save JSON: {json}");
                }
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to save Snailpocalypse data: {ex}");
            }
        }

        private string GetSavePath(string savePath)
        {
            var hash = Lib.SaveGame.GetUniqueString(savePath);
            return Lib.SaveGame.GetSavestoreDirectoryPath(System.Reflection.Assembly.GetExecutingAssembly(), $"Snailpocalypse_{hash}.json");
        }
        
        public void CreateSnail(SnailpocalypseSaveFile.SnailSaveData loadedData = null)
        {
            if (!ModifiersController.Instance.snailNemesisModifierEnabled)
                return;
            
            if (loadedData == null)
            {
                // Vector3 spawnPos = Player.Instance.gameObject.transform.position + (Player.Instance.gameObject.transform.forward * 2.5f
                var randomStreet = CityData.Instance.streetDirectory[new System.Random().Next(CityData.Instance.streetDirectory.Count)];
                Vector3 spawnPos = CityData.Instance.citizenDirectory[0].FindSafeTeleport(randomStreet).position;
                loadedData = new SnailpocalypseSaveFile.SnailSaveData()
                {
                    pos = new SnailpocalypseSaveFile.SnailSaveData.SerializedVector3(spawnPos),
                    rot = new SnailpocalypseSaveFile.SnailSaveData.SerializedQuaternion(Quaternion.identity),
                    inAirVent = false,
                    duct = -1
                };

                if (DebugLogging.Value)
                {
                    Log.LogInfo($"Spawning new snail at {spawnPos} ({randomStreet.name})");
                }
            }
            
            var s = GameObject.Instantiate(ModifiersController.Instance.snailPrefab, loadedData.pos.ToVector3(), loadedData.rot.ToQuaternion());
            s.transform.localScale *= SnailSizeMultiplier.Value;
            UpdateSnailOutline(s);
            UpdateSnailSpeed(s.GetComponent<SnailController>());
            SpawnedSnails.Add(s.GetComponent<SnailController>());
            s.name = s.name + SpawnedSnails.Count;
        }
        
        [HarmonyPatch(typeof(SnailController), nameof(SnailController.Update))]
        public class SnailController_Update
        {
            public static void Postfix(SnailController __instance)
            {
                // if (SpawnedSnails.Contains(__instance))
                // {
                    var snailDistance = Vector3.Distance(
                        new Vector3(__instance.transform.position.x, 0, __instance.transform.position.z),
                        new Vector3(Player.Instance.gameObject.transform.position.x, 0, Player.Instance.gameObject.transform.position.z)
                    );
                    var snailVerticalPosition = __instance.transform.position.y - SnailGroundOffset.Value;
                    Log.LogInfo($"Snail {__instance.name} is {snailDistance} ({SnailKillRadius.Value}) away from the player (vertical {Player.Instance.gameObject.transform.position.y - 1f} < {snailVerticalPosition} < {Player.Instance.gameObject.transform.position.y + 1f}");
                    if (
                        snailDistance < SnailKillRadius.Value &&
                        (snailVerticalPosition >= Player.Instance.gameObject.transform.position.y - 1f && snailVerticalPosition <= Player.Instance.gameObject.transform.position.y + 1f)
                    )
                    {
                        Player.Instance.KillPlayer();
                    }
                // }
            }
        }

        [HarmonyPatch(typeof(SnailController), nameof(SnailController.MoveSnail))]
        public class SnailController_MoveSnail
        {
            private static float lastOffset = 0;
            public static void Prefix(SnailController __instance)
            {
                __instance.transform.position -= Vector3.up * lastOffset;
            }
            
            public static void Postfix(SnailController __instance)
            {
                lastOffset = SnailGroundOffset.Value;
                __instance.transform.position += Vector3.up * lastOffset;
            }
        }
        
        [HarmonyPatch]
        class SnailController_SetupNewSnail
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                return AccessTools.GetDeclaredMethods(typeof(SnailController))
                    .Where(m => m.Name == "SetupNewSnail");
            }

            public static void Postfix()
            {
                Log.LogInfo("Applying new snail size multiplier in SetupNewSnail");
                ModifiersController.Instance.currentSnail.gameObject.transform.localScale *= SnailSizeMultiplier.Value;
                UpdateSnailOutline(ModifiersController.Instance.currentSnail.gameObject);
                UpdateSnailSpeed(ModifiersController.Instance.currentSnail);
            }
        }
    }
}