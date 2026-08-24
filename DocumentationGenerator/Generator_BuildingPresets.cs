using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using static AssetBundleLoader.JsonLoader;

namespace DocumentationGenerator
{
    internal class Generator_BuildingPresets
    {
        public static void GenerateBuildingPreset(string BUILDING_PRESET_EXPORT_PATH, BuildingPreset buildingPreset)
        {
            var json = NewtonsoftExtensions.NewtonsoftJson.JToken_Parse(RestoredJsonUtility.ToJsonInternal((dynamic)buildingPreset, true));

            ReplaceFloorplanRefs(json, "floorLayouts", buildingPreset.floorLayouts);
            ReplaceFloorplanRefs(json, "basementLayouts", buildingPreset.basementLayouts);

            System.IO.File.WriteAllText(System.IO.Path.Join(BUILDING_PRESET_EXPORT_PATH, buildingPreset.name + ".json"), json.ToString());
        }

        private static void ReplaceFloorplanRefs(dynamic json, string layoutListName, Il2CppSystem.Collections.Generic.List<BuildingPreset.InteriorFloorSetting> layouts)
        {
            if (layouts == null) return;

            for (var i = 0; i < layouts.Count; i++)
            {
                ReplaceTextAssetRefsWithNames(json, $"{layoutListName}[{i}].blueprints", layouts[i].blueprints);
                ReplaceTextAssetRefsWithNames(json, $"{layoutListName}[{i}].controlRoomVariants", layouts[i].controlRoomVariants);
            }
        }

        private static void ReplaceTextAssetRefsWithNames(dynamic json, string jsonPath, Il2CppSystem.Collections.Generic.List<TextAsset> textAssets)
        {
            var token = json.SelectToken(jsonPath);
            if (token == null) return;

            var names = new List<string>();
            for (var i = 0; i < (textAssets?.Count ?? 0); i++)
            {
                names.Add(textAssets[i] == null ? "" : textAssets[i].name);
            }

            token.Replace(NewtonsoftExtensions.NewtonsoftJson.JToken_Parse("[" + string.Join(",", names.Select(n => $"\"{n}\"")) + "]"));
        }
    }
}
