using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace DDSLoader
{
    internal class DDSLoaderHooks
    {
        static Dictionary<string, NewspaperArticle> ddsToArticle;

        [HarmonyPatch(typeof(Toolbox), "Start")]
        public class Toolbox_Start
        {
            public static void Postfix()
            {
                if (DDSLoaderPlugin.debugClearNewspaperArticles.Value)
                {
                    Toolbox.Instance.allArticleTrees.Clear();
                }

                foreach (var dir in DDSLoader.DDSLoaderPlugin.modsToLoadFrom)
                {
                    var treesPath = Path.Combine(dir.FullName, "DDS", "Trees");
                    var messagesPath = Path.Combine(dir.FullName, "DDS", "Messages");
                    var blocksPath = Path.Combine(dir.FullName, "DDS", "Blocks");

                    if (Directory.Exists(blocksPath))
                    {
                        foreach (var blockPath in Directory.GetFiles(blocksPath, "*.block"))
                        {
                            try
                            {
                                var block = JsonUtility.FromJson<DDSSaveClasses.DDSBlockSave>(File.ReadAllText(blockPath));
                                Toolbox.Instance.allDDSBlocks.Add(block.id, block);
                            }
                            catch (Exception exception)
                            {
                                DDSLoaderPlugin.PluginLogger.LogError($"Failed to load: {blockPath}");
                                DDSLoaderPlugin.PluginLogger.LogError(exception);
                            }
                        }

                        foreach (var blockPath in Directory.GetFiles(blocksPath, "*.block_patch"))
                        {
                            try
                            {
                                var patchedBlock = JsonUtility.FromJson<DDSSaveClasses.DDSBlockSave>(CreatePatchedJson(blockPath));
                                Toolbox.Instance.allDDSBlocks[patchedBlock.id] = patchedBlock;
                            }
                            catch (Exception exception)
                            {
                                DDSLoaderPlugin.PluginLogger.LogError($"Failed to load: {blockPath}");
                                DDSLoaderPlugin.PluginLogger.LogError(exception);
                            }
                        }
                    }

                    if (Directory.Exists(messagesPath))
                    {
                        foreach (var messagePath in Directory.GetFiles(messagesPath, "*.msg"))
                        {
                            try
                            {
                                var message = JsonUtility.FromJson<DDSSaveClasses.DDSMessageSave>(File.ReadAllText(messagePath));
                                Toolbox.Instance.allDDSMessages.Add(message.id, message);
                            }
                            catch (Exception exception)
                            {
                                DDSLoaderPlugin.PluginLogger.LogError($"Failed to load: {messagePath}");
                                DDSLoaderPlugin.PluginLogger.LogError(exception);
                            }
                        }

                        foreach (var messagePath in Directory.GetFiles(messagesPath, "*.msg_patch"))
                        {
                            try
                            {
                                var patchedMessage = JsonUtility.FromJson<DDSSaveClasses.DDSMessageSave>(CreatePatchedJson(messagePath));
                                Toolbox.Instance.allDDSMessages[patchedMessage.id] = patchedMessage;
                            }
                            catch (Exception exception)
                            {
                                DDSLoaderPlugin.PluginLogger.LogError($"Failed to load: {messagePath}");
                                DDSLoaderPlugin.PluginLogger.LogError(exception);
                            }
                        }
                    }

                    if (Directory.Exists(treesPath))
                    {
                        foreach (var treePath in Directory.GetFiles(treesPath, "*.tree"))
                        {
                            try
                            {
                                var tree = JsonUtility.FromJson<DDSSaveClasses.DDSTreeSave>(File.ReadAllText(treePath));

#if MONO
                                tree.messageRef = new Dictionary<string, DDSSaveClasses.DDSMessageSettings>();
#elif IL2CPP
                                tree.messageRef = new Il2CppSystem.Collections.Generic.Dictionary<string, DDSSaveClasses.DDSMessageSettings>();
#endif

                                foreach (var msg in tree.messages)
                                {
                                    tree.messageRef.Add(msg.instanceID, msg);
                                }

                                Toolbox.Instance.allDDSTrees.Add(tree.id, tree);

                                if (tree.treeType == DDSSaveClasses.TreeType.newspaper)
                                {
                                    DDSLoaderPlugin.PluginLogger.LogWarning($"Newspaper content is no longer supported - use the official editor");
                                    // LoadNewspaperArticle(tree, messagesPath);
                                }
                            }
                            catch (Exception exception)
                            {
                                DDSLoaderPlugin.PluginLogger.LogError($"Failed to load: {treePath}");
                                DDSLoaderPlugin.PluginLogger.LogError(exception);
                            }
                        }

                        foreach (var treePath in Directory.GetFiles(treesPath, "*.tree_patch"))
                        {
                            try
                            {
                                var patchedTree = JsonUtility.FromJson<DDSSaveClasses.DDSTreeSave>(CreatePatchedJson(treePath));


#if MONO
                                patchedTree.messageRef = new Dictionary<string, DDSSaveClasses.DDSMessageSettings>();
#elif IL2CPP
                                patchedTree.messageRef = new Il2CppSystem.Collections.Generic.Dictionary<string, DDSSaveClasses.DDSMessageSettings>();
#endif

                                foreach (var msg in patchedTree.messages)
                                {
                                    patchedTree.messageRef.Add(msg.instanceID, msg);
                                }

                                Toolbox.Instance.allDDSTrees[patchedTree.id] = patchedTree;
                            }
                            catch (Exception exception)
                            {
                                DDSLoaderPlugin.PluginLogger.LogError($"Failed to load: {treePath}");
                                DDSLoaderPlugin.PluginLogger.LogError(exception);
                            }
                        }
                    }

                    DDSLoaderPlugin.PluginLogger.LogInfo($"Loaded DDS Content and Patches For: {dir.Parent.Name}");

                    var selectedLanguagePath = Path.Combine(dir.FullName, "Strings", Game.Instance.language);
                    var englishLanguagePath = Path.Combine(dir.FullName, "Strings", "English");

                    // var StringsLoadIntoDictionaryMI = typeof(Strings).GetMethod("LoadIntoDictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var StringsLoadIntoDictionaryMI = Il2CppInterop.Runtime.Il2CppType.From(typeof(Strings)).GetMethod("LoadIntoDictionary", Il2CppSystem.Reflection.BindingFlags.NonPublic | Il2CppSystem.Reflection.BindingFlags.Static);

                    if(StringsLoadIntoDictionaryMI == null)
                    {
                        DDSLoaderPlugin.PluginLogger.LogError("Strings.LoadIntoDictionary not found!");
                    }
                    else
                    {
                        var stringsPath = Directory.Exists(selectedLanguagePath) ? selectedLanguagePath : Directory.Exists(englishLanguagePath) ? englishLanguagePath : "";
                        if (stringsPath != "")
                        {
                            foreach (var stringFile in Directory.GetFiles(stringsPath, "*.csv", SearchOption.AllDirectories))
                            {
                                var fileName = Path.GetFileNameWithoutExtension(stringFile);
                                foreach (var line in File.ReadAllLines(stringFile))
                                {
                                    Strings.ParseLine(line.Trim(), out var key, out var notes, out var display, out var alt, out var freq, out var suffix, out var misc);
                                    if (display != null && StringsLoadIntoDictionaryMI != null)
                                    {
                                        StringsLoadIntoDictionaryMI.Invoke(null, (new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>(new Il2CppSystem.Object[] { fileName, Strings.stringTable[fileName].Count + 1, key, display.Replace("\\r\\n", "\r\n"), alt, freq, suffix })));

                                        if (DDSLoaderPlugin.debugPrintLoadedStrings.Value)
                                        {
                                            DDSLoaderPlugin.PluginLogger.LogInfo($"{fileName}: {display.Replace("\\r\\n", "\r\n")}");
                                        }
                                    }
                                }
                            }

                            DDSLoaderPlugin.PluginLogger.LogInfo($"Loaded String Content For: {dir.Parent.Name}");
                        }
                    }
                }
            }

            static string CreatePatchedJson(string patchPath)
            {
                var patchFileInfo = new FileInfo(patchPath);
                var patchDirInfo = new DirectoryInfo(patchFileInfo.DirectoryName);

                var existingDDSContent = JToken.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "DDS", patchDirInfo.Name, patchFileInfo.Name.Split('_')[0])));
                var patchDDSContent = Tavis.PatchDocument.Parse(File.ReadAllText(patchPath));

                patchDDSContent.ApplyTo(existingDDSContent);

                return existingDDSContent.ToString();
            }

            static void LoadNewspaperArticle(DDSSaveClasses.DDSTreeSave tree, string messagesPath)
            {
                if (tree.treeType == DDSSaveClasses.TreeType.newspaper)
                {
                    /*
                    foreach (var articleDefinition in tree.messages)
                    {
                        var newspaperDefinition = Path.Combine(messagesPath, articleDefinition.msgID + ".newspaper");

                        if(!File.Exists(newspaperDefinition))
                        {
                            DDSLoaderPlugin.PluginLogger.LogError($"Newspaper article definition doesn't exist for: {articleDefinition.msgID}. Skipping.");
                            continue;
                        }

                        var newArticleSerailized = JsonConvert.DeserializeObject<CustomSerializableNewspaperArticle>(File.ReadAllText(newspaperDefinition));

                        var newArticle = ScriptableObject.CreateInstance<NewspaperArticle>();

                        newArticle.name = Toolbox.Instance.allDDSMessages[articleDefinition.msgID].name;
                        newArticle.disabled = newArticleSerailized.disabled;
                        newArticle.ddsReference = articleDefinition.msgID;
                        newArticle.category = (NewspaperArticle.Category)newArticleSerailized.category;
                        newArticle.context = (NewspaperArticle.ContextSource)newArticleSerailized.context;

                        if (ddsToArticle == null)
                        {
                            ddsToArticle = new Dictionary<string, NewspaperArticle>();
                            foreach (var article in Toolbox.Instance.allArticleTrees)
                            {
                                ddsToArticle[article.id] = article;
                            }
                        }

                        foreach (var articleDDS in newArticleSerailized.followupStories)
                        {
                            newArticle.followupStories.Add(ddsToArticle[articleDDS]);
                        }

                        foreach (var spriteName in newArticleSerailized.possibleImages)
                        {
                            // newArticle.possibleImages.Add(Resources.FindObjectsOfTypeAll<Spite>);
                        }
                        Toolbox.Instance.allArticleTrees.Add(newArticle);
                    }
                    */
                }
            }

            [System.Serializable]
            class CustomSerializableNewspaperArticle
            {
                public bool disabled;
                public int category;
                public string[] followupStories;
                public string[] possibleImages;
                public int context;
            }
        }
    }
}
