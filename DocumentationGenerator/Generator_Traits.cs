using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentationGenerator
{
    internal class Generator_Traits
    {
        public static void GenerateTraitsWiki(string MEDIAWIKI_DOC_EXPORT_PATH, IEnumerable<string> traitList)
        {
            var sortedTraits = new SortedDictionary<string, SortedSet<string>>();

            string mediawikiTable = "{{Generated}}\n";
            mediawikiTable += $"\n";
            mediawikiTable += $"Last generated for game version {Game.Instance.buildID}.\n";
            mediawikiTable += $"\n";

            foreach (var trait in traitList)
            {
                // Some traits have double -, but it's always the type
                var traitCat = trait.Substring(0, trait.LastIndexOf('-'));
                var traitName = trait.Substring(trait.LastIndexOf('-') + 1);

                if(!sortedTraits.ContainsKey(traitCat))
                    sortedTraits.Add(traitCat, new SortedSet<string>());

                sortedTraits[traitCat].Add(traitName);
            }

            foreach(var key in sortedTraits.Keys)
            {
                // Simple collapsable table with the type as the heading and a sorted list of traits in that type
                mediawikiTable += "\n";
                mediawikiTable += "{| class=\"mw-collapsible mw-collapsed wikitable\"\n";
                mediawikiTable += "|-\n";
                mediawikiTable += $"! {key}\n";
                mediawikiTable += "|-\n";
                mediawikiTable += "|\n";

                foreach(var trait in sortedTraits[key])
                {
                    mediawikiTable += $"* {trait}\n";
                }

                mediawikiTable += "|-\n";
                mediawikiTable += "|}\n";
            }

            System.IO.File.WriteAllText(System.IO.Path.Join(MEDIAWIKI_DOC_EXPORT_PATH, "MediaWiki_Traits.txt"), mediawikiTable);
        }
    }
}
