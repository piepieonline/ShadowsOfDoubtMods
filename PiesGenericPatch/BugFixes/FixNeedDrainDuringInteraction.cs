using HarmonyLib;

namespace Pies_Generic_Patch.BugFixes
{
    /// <summary>
    /// Stops citizen needs increasing more than they should while they are interacting or talking
    ///
    /// In vanilla, NewAIController.AITick returns early if the citizen is interacting or in conversation,
    /// before reaching the lastUpdated = gameTime assignment at the end of the method.
    /// </summary>
    public class FixNeedDrainDuringInteraction
    {
        public static void DoPatch(Harmony harmony)
        {
            harmony.PatchAll(typeof(FixNeedDrainDuringInteraction.NewAIController_AITick));
        }

        [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.AITick))]
        public class NewAIController_AITick
        {
            public static void Postfix(NewAIController __instance)
            {
                var human = __instance.human;

                // Other early exits that don't need to change lastUpdated
                if (human == null || human.isDead || Game.Instance.pauseAI)
                    return;

                if (human.interactingWith != null || human.inConversation)
                    __instance.lastUpdated = SessionData.Instance.gameTime;
            }
        }
    }
}
