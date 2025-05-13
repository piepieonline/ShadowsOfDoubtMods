# Piepieonline's AssetBundleLoader

Various methods for loading content into Shadows of Doubt, including asset bundles, raw JSON scriptable objects, and more
* Load AssetBundles built against previous game versions, by correcting referenced PathIDs.
* Load new ScriptableObjects from raw JSON files with text based referencing
* Create Texture2D from PNG files on the file system (Uses BigGustave library to replace stripped Unity and System methods)

# Manual Installation

* Ensure you have BepInEx BE installed
* Extract the mod to ".\BepInEx\plugins\", so you should have ".\BepInEx\plugins\AssetBundleLoader\AssetBundleLoader.dll"
* Ensure the rest of the *.dll files are along side the AssetBundleLoader.dll file

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/AssetBundleLoader