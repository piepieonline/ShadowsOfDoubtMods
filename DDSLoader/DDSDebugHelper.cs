using BepInEx.Configuration;
using HarmonyLib;

using UnityEngine;
using System.Linq;

#if MONO
using System.Collections.Generic;
#elif IL2CPP
using Il2CppSystem.Collections.Generic;
#endif

namespace DDSLoader
{
    public class DDSDebugHelper
    {
        public static void StartConversation(string ddsTreeId, string participantA, string participantB)
        {
            StartConversation(ddsTreeId, new string[] {participantA, participantB});
        }

        public static void StartConversation(string ddsTreeId, string[] participants)
        {
            Human mainParticipant = null;
            List<Human> otherParticipants = new List<Human>();

            foreach (var participantName in participants)
            {
                if(mainParticipant == null)
                {
                    mainParticipant = GameObject.Find(participantName).GetComponent<Human>();
                }
                else
                {
                    otherParticipants.Add(GameObject.Find(participantName).GetComponent<Human>());
                }
            }

            mainParticipant.ExecuteConversationTree(Toolbox.Instance.allDDSTrees[ddsTreeId], otherParticipants);
        }
    }

    [HarmonyPatch(typeof(Human), "ExecuteConversationTree")]
    public class Human_ExecuteConversationTree
    {
        public static void Postfix(Human __instance, DDSSaveClasses.DDSTreeSave newTree, List<Human> otherParticipants)
        {
            if (DDSLoaderPlugin.debugLogConversations.Value)
            {
                DDSLoaderPlugin.PluginLogger.LogInfo($"Conversation started: {newTree.id} started by {__instance.name} and including {string.Join(", ", otherParticipants.ToArray().Select(other => other.name))}");

            }
            if (DDSLoaderPlugin.debugPauseTreeGUID.Value == newTree.id)
            {
                InterfaceController.Instance.ToggleNotebookButton();
            }
        }
    }
}
