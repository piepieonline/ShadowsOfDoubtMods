using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealEstateListingCruncherApp
{
    internal class RealEstateListingCruncherHooks
    {
        [HarmonyPatch(typeof(MainMenuController), "Start")]
        public class MainMenuController_Start
        {
            static bool hasInit = false;

            public static void Prefix()
            { 
                if (hasInit) return;
                hasInit = true;

                var moddedAssetBundle = UniverseLib.AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "RealEstateListingCruncherAppBundle"));
                // This is literally the scriptableobject the game stores these in, so that works
                var newCruncherApp = moddedAssetBundle.LoadAsset<CruncherAppPreset>("ForSale");
                newCruncherApp.appContent[0].AddComponent<RealEstateCruncherAppContent>();

                foreach (var cruncher in UnityEngine.Resources.FindObjectsOfTypeAll<InteractablePreset>().Where(preset => preset.name.Contains("Cruncher")))
                {
                    cruncher.additionalApps.Insert(cruncher.additionalApps.Count - 2, newCruncherApp);
                } 
                RealEstateListingCruncherPlugin.Logger.LogInfo("Loading custom asset bundle complete");
            }
        }
    }
}
