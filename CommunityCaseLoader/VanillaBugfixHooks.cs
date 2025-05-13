using System.Linq;
using HarmonyLib;


namespace CommunityCaseLoader
{
    internal class VanillaBugfixHooks
    {
        [HarmonyPatch]
        public class Toolbox_NewVmailThread
        {
            public static bool LoggingEnabled = false;
            public static MurderMO CurrentMurderMO = null;

            [HarmonyTargetMethod]
            internal static System.Reflection.MethodBase CalculateMethod()
            {
                var mi = typeof(Toolbox).GetMethods().Where(mi => mi.Name == "NewVmailThread" && mi.GetParameters().Length == 7).First();
                return mi;
            }

            public static bool Prefix(Human from, Il2CppSystem.Collections.Generic.List<Human> otherParticipiants, string treeID, float timeStamp, int progress, StateSaveData.CustomDataSource overrideDataSource, int newDataSourceID)
            {
                Human to1 = null;
                Human to2 = null;
                Human to3 = null;
                var cc = new Il2CppSystem.Collections.Generic.List<Human>();

                // This section is wrong in vanilla
                if (otherParticipiants.Count >= 1)
                    to1 = otherParticipiants[0];
                if (otherParticipiants.Count >= 2)
                    to2 = otherParticipiants[1];
                if (otherParticipiants.Count >= 3)
                    to3 = otherParticipiants[2];
                
                if (otherParticipiants.Count >= 4)
                {
                    for (int index = 4; index < otherParticipiants.Count; ++index)
                        cc.Add(otherParticipiants[index]);
                }
                Toolbox.Instance.NewVmailThread(from, to1, to2, to3, cc, treeID, timeStamp, progress, overrideDataSource, newDataSourceID);

                return false;
            }
        }
    }
}
