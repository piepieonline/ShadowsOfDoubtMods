# Piepieonline's DDSLoader

Library. Loader for both new and patch based overrides DDS content (Trees/Messages/Blocks) and localised strings

# Content Layout

Content is loaded from any folder named `DDSContent`, in one of two ways:

* Folder scan (default): files are found by where they sit, eg `DDSContent/DDS/Trees/*.tree`, `DDSContent/Strings/English/*.csv`
* Manifest: if `DDSContent/ddsmanifest.json` exists, only the files it lists are loaded, from wherever they actually sit

The manifest's `files` keys are file paths relative to the manifest (so a bare filename is a sibling), and the values are the folder the file would have sat in if it were a folder layout:

```json
{
  "enabled": true,
  "files": {
    "MyConversation.tree": "DDS/Trees",
    "Messages/MyConversation.msg": "DDS/Messages",
    "MyStrings.csv": "Strings/English/DDS"
  }
}
```

Setting `enabled` to false skips that DDSContent folder entirely.

# Debugging

The library provides some useful features for debugging

* Logging of all conversations that begin
* Pausing when a specific conversation begins

For executing in UnityExplorer: 

* DDSLoader.DDSDebugHelper.StartConversation("DDS_Tree_ID", new string[] { "Human_1", "Human_2" });

# Manual Installation

* Ensure you have BepInEx BE installed
* Extract the mod to ".\BepInEx\plugins\", so you should have ".\BepInEx\plugins\DDSLoader\DDSLoader.dll"

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/DDSLoader