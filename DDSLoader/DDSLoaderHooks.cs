﻿using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DDSLoader
{
    internal class DDSLoaderHooks
    {
        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                foreach(var dir in DDSLoader.DDSLoaderPlugin.modsToLoadFrom)
                {
                    var treesPath = Path.Combine(dir.FullName, "DDS", "Trees");
                    var messagesPath = Path.Combine(dir.FullName, "DDS", "Messages");
                    var blocksPath = Path.Combine(dir.FullName, "DDS", "Blocks");

                    if (Directory.Exists(blocksPath))
                    {
                        foreach (var blockPath in Directory.GetFiles(blocksPath, "*.block"))
                        {
                            var block = JsonUtility.FromJson<DDSSaveClasses.DDSBlockSave>(File.ReadAllText(blockPath));
                            Toolbox.Instance.allDDSBlocks.Add(block.id, block);
                        }
                        
                        foreach (var blockPath in Directory.GetFiles(blocksPath, "*.block_patch"))
                        {
                            var patchedBlock = JsonUtility.FromJson<DDSSaveClasses.DDSBlockSave>(CreatePatchedJson(blockPath));
                            Toolbox.Instance.allDDSBlocks[patchedBlock.id] = patchedBlock;
                        }
                    }
                    
                    if (Directory.Exists(messagesPath))
                    {
                        foreach (var messagePath in Directory.GetFiles(messagesPath, "*.msg"))
                        {
                            var message = JsonUtility.FromJson<DDSSaveClasses.DDSMessageSave>(File.ReadAllText(messagePath));
                            Toolbox.Instance.allDDSMessages.Add(message.id, message);
                        }

                        foreach (var messagePath in Directory.GetFiles(messagesPath, "*.msg_patch"))
                        {
                            var patchedMessage = JsonUtility.FromJson<DDSSaveClasses.DDSMessageSave>(CreatePatchedJson(messagePath));
                            Toolbox.Instance.allDDSMessages[patchedMessage.id] = patchedMessage;
                        }
                    }

                    if(Directory.Exists(treesPath) )
                    {
                        foreach (var treePath in Directory.GetFiles(treesPath, "*.tree"))
                        {
                            var tree = JsonUtility.FromJson<DDSSaveClasses.DDSTreeSave>(File.ReadAllText(treePath));
                            tree.messageRef = new Il2CppSystem.Collections.Generic.Dictionary<string, DDSSaveClasses.DDSMessageSettings>();

                            foreach(var msg in tree.messages)
                            {
                                tree.messageRef.Add(msg.instanceID, msg);
                            }

                            Toolbox.Instance.allDDSTrees.Add(tree.id, tree);
                        }

                        foreach (var treePath in Directory.GetFiles(treesPath, "*.tree_patch"))
                        {
                            var patchedTree = JsonUtility.FromJson<DDSSaveClasses.DDSTreeSave>(CreatePatchedJson(treePath));
                            patchedTree.messageRef = new Il2CppSystem.Collections.Generic.Dictionary<string, DDSSaveClasses.DDSMessageSettings>();

                            foreach (var msg in patchedTree.messages)
                            {
                                patchedTree.messageRef.Add(msg.instanceID, msg);
                            }

                            Toolbox.Instance.allDDSTrees[patchedTree.id] = patchedTree;
                        }
                    }

                    DDSLoaderPlugin.Logger.LogInfo($"Loaded DDS Content and Patches For: {dir.Parent.Name}");

                    var selectedLanguagePath = Path.Combine(dir.FullName, "Strings", Game.Instance.language);
                    var englishLanguagePath = Path.Combine(dir.FullName, "Strings", "English");

                    var stringsPath = Directory.Exists(selectedLanguagePath) ? selectedLanguagePath : Directory.Exists(englishLanguagePath) ? englishLanguagePath : "";
                    if(stringsPath != "")
                    {
                        foreach(var stringFile in Directory.GetFiles(stringsPath, "*.csv", SearchOption.AllDirectories))
                        {
                            var fileName = Path.GetFileNameWithoutExtension(stringFile);
                            foreach (var line in File.ReadAllLines(stringFile))
                            {
                                var lineSplit = line.Split(",");
                                Strings.LoadIntoDictionary(fileName, Strings.stringTable[fileName].Count + 1, lineSplit[0], lineSplit[2], lineSplit[3], int.TryParse(lineSplit[4], out var frequency) ? frequency : 0, bool.TryParse(lineSplit[5], out var requiresSuffix) ? requiresSuffix : false);
                            }
                        }

                        DDSLoaderPlugin.Logger.LogInfo($"Loaded String Content For: {dir.Parent.Name}");
                    }
                }
            }

            static string CreatePatchedJson(string patchPath)
            {
                var patchFileInfo = new FileInfo(patchPath);
                var patchDirInfo = new DirectoryInfo(patchFileInfo.DirectoryName);

                var existingDDSContent = JToken.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "DDS", patchDirInfo.Name, patchFileInfo.Name.Split("_")[0])));
                var patchDDSContent = Tavis.PatchDocument.Parse(File.ReadAllText(patchPath));

                patchDDSContent.ApplyTo(existingDDSContent);

                return existingDDSContent.ToString();
            }
        }
    }
}