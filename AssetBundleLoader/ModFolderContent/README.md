# Piepieonline's AssetBundleLoader

Various methods for loading content into Shadows of Doubt, including asset bundles, raw JSON scriptable objects, and more
* Load AssetBundles built against previous game versions, by correcting referenced PathIDs.
* Load new ScriptableObjects from raw JSON files with text based referencing
  * Patch ScriptableObjects from partial JSON representation
* Create Texture2D from PNG files on the file system (Uses BigGustave library to replace stripped Unity and System methods)
* Load building floorplans from FloorSaveData JSON files, referenced from a BuildingPreset as `"blueprints": [ "FLOOR:MyFloorplan" ]`
  * The path is relative to the folder holding the mod's manifest, and the file name becomes the floor's name in the city's floor data, so prefix it to avoid clashing with other mods
* Reference assets inside a mod's AssetBundle from JSON as `"prefab": "BUNDLE:MyBundle.ab|MyPrefab"`, for the fields `REF:` can't reach - prefabs, meshes, textures and the like
  * The bundle path is relative to the folder holding the mod's manifest, and the asset name is the name it has inside the bundle
  * A BuildingPreset's model is finished off on load: prefabs keep their own tags, and anything left untagged is treated as a lit part of the building model, with the bundle's copies of the game's shaders swapped for the game's own
* Reference assets the game already ships from JSON as `"prefab": "VANILLA:BuildingPreset|CityHall.prefab"`, for prefabs, meshes and materials that no `REF:` can name because they aren't presets themselves
  * The reference is a preset, then the path of fields to walk from it, with list entries written as `floorLayouts[0].blueprints[0]`
  * A material can be copied the same way, as `"copyFrom": "VANILLA:MaterialGroupPreset|Brick.material"` in a prefab's material block

# Manual Installation

* Ensure you have BepInEx BE installed
* Extract the mod to ".\BepInEx\plugins\", so you should have ".\BepInEx\plugins\AssetBundleLoader\AssetBundleLoader.dll"
* Ensure the rest of the *.dll files are along side the AssetBundleLoader.dll file

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/AssetBundleLoader