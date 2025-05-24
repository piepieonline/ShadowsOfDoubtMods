using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebugMod
{
    internal class StaticCityNameHook
    {
        [HarmonyPatch(typeof(MainMenuController), nameof(MainMenuController.RandomCityName))]
        public class MainMenuController_RandomCityName
        {
            public static bool Prefix()
            {
                RestartSafeController.Instance.cityName = "LocalModding";
                MainMenuController.Instance.OnChangeCityGenerationOption();
                return false;
            }
        }
    }
}
