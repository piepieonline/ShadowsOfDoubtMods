using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UniverseLib;
using SOD.Common.Extensions;
using static AssetBundleLoader.JsonLoader;

namespace DocumentationGenerator
{
    internal class Generator_DDS
    {
        public static Dictionary<string, dynamic> GenerateLookup(string DDS_GAME_PATH)
        {
            var ddsMap = new Dictionary<string, dynamic>();
            ddsMap["ReverseIdMap"] = new Dictionary<string, List<string>>();
            ddsMap["IdNameMap"] = new Dictionary<string, string>();
            var reverseIdMap = ddsMap["ReverseIdMap"];
            var idNameMap = ddsMap["IdNameMap"];
            foreach (var directoryName in new string[] { "Trees", "Messages", "Blocks" })
            {
                string folderPath = System.IO.Path.Combine(DDS_GAME_PATH, directoryName);
                ddsMap[directoryName] = new List<string>();

                foreach (var filePath in System.IO.Directory.EnumerateFiles(folderPath))
                {
                    var id = filePath.Substring(filePath.LastIndexOf(System.IO.Path.DirectorySeparatorChar) + 1).Split(".")[0];
                    ddsMap[directoryName].Add(id);

                    var json = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(System.IO.File.ReadAllText(filePath));

                    if (json["name"] != null)
                    {
                        idNameMap[id] = json.Value<string>("name");
                    }

                    switch (directoryName)
                    {
                        case "Trees":
                            foreach (var message in json["messages"])
                            {
                                if (message["msgID"] == null) continue;
                                string msgId = message.Value<string>("msgID");
                                if (msgId == "") continue;
                                if (!reverseIdMap.ContainsKey(msgId)) reverseIdMap[msgId] = new List<string>();
                                reverseIdMap[msgId].Add(id);
                            }
                            break;
                        case "Messages":
                            foreach (var message in json["blocks"])
                            {
                                if (message["blockID"] == null) continue;
                                string blockID = message.Value<string>("blockID");
                                if (blockID == "") continue;
                                if (!reverseIdMap.ContainsKey(blockID)) reverseIdMap[blockID] = new List<string>();
                                reverseIdMap[blockID].Add(id);
                            }
                            break;
                        case "Blocks":
                            foreach (var message in json["replacements"])
                            {
                                if (message["replaceWithID"] == null) continue;
                                string replacementID = message.Value<string>("replaceWithID");
                                if (replacementID == "") continue;
                                if (!reverseIdMap.ContainsKey(replacementID)) reverseIdMap[replacementID] = new List<string>();
                                reverseIdMap[replacementID].Add(id);
                            }
                            break;
                    }
                }
            }

            return ddsMap;
        }

        public static void GenerateScopes(string DOC_EXPORT_PATH, string MEDIAWIKI_DOC_EXPORT_PATH)
        {
            // Generate JSON map
            var scopeMap = new SortedDictionary<string, DDSScope_Serialised>();
            string mediawikiTable = "{{Generated}}\n";
            mediawikiTable += "\n";
            mediawikiTable += "[[Category:DDS]]\n";
            mediawikiTable += $"\n";
            mediawikiTable += $"Last generated for game version {Game.Instance.buildID}.\n";
            mediawikiTable += $"\n";

            foreach (DDSScope uo in RuntimeHelper.FindObjectsOfTypeAll<DDSScope>())
            {
                var scopeName = uo.name;
                if (scopeName == "") continue;

                // Duplicates exist - maybe addressables related?
                // Using the first one seems good enough for now
                if (scopeMap.ContainsKey(scopeName))
                {
                    continue;
                }

                scopeMap.Add(scopeName, new DDSScope_Serialised()
                {
                    global = uo.isGlobal,
                    containedScopes = uo.containedScopes.ToList().Aggregate(new Dictionary<string, string>(), (dict, containedScope) =>
                    {
                        dict[containedScope.name] = containedScope.type.name;
                        return dict;
                    }),
                    containedValues = uo.containedValues.ToList()
                });
            }

            // Generate Miraheze table
            foreach (var key in scopeMap.Keys)
            {
                // Heading
                mediawikiTable += $"== {key}{(scopeMap[key].global ? " (Global)" : "")} ==\n";

                // Table
                mediawikiTable += "{| class=\"mw-collapsible mw-collapsed wikitable\"\n";

                // Table heading row
                mediawikiTable += $"|-\n";
                mediawikiTable += $"! Scope\n";
                mediawikiTable += $"! Contained Scopes\n";
                mediawikiTable += $"! Contained Values\n";

                // First col (Scope)
                mediawikiTable += $"|-\n";
                mediawikiTable += $"| {key}\n";
                
                // Second col (Contained Scopes)
                mediawikiTable += $"|\n";
                foreach(var containedScope in scopeMap[key].containedScopes)
                {
                    mediawikiTable += $"* {containedScope.Key} ({containedScope.Value})\n";
                }
                
                // Third col (Contained Values)
                mediawikiTable += $"|\n";
                foreach(var containedValue in scopeMap[key].containedValues)
                {
                    mediawikiTable += $"* {containedValue}\n";
                }

                // End the table
                mediawikiTable += $"|-\n";
                mediawikiTable += "|}\n";
            }

            // Export
            System.IO.File.WriteAllText(System.IO.Path.Join(DOC_EXPORT_PATH, "ddsScopeMap.json"), NewtonsoftExtensions.NewtonsoftJson.JObject_FromObject(scopeMap).ToString());
            System.IO.File.WriteAllText(System.IO.Path.Join(MEDIAWIKI_DOC_EXPORT_PATH, "MediaWiki_DDSScopes.txt"), mediawikiTable);
        }

        class DDSScope_Serialised
        {
            public bool global;
            public Dictionary<string, string> containedScopes;
            public List<string> containedValues;
        }
    }
}
