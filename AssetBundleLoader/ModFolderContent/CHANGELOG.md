# 2.1.0

* Added creating Texture2D objects from PNG files on disk

# 2.0.4

* Updated to support fixes for DDSLoader patching

# 2.0.3

* Added an in-game error popup when missing content is detected

# 2.0.2

* Fixed for 39.10

# 2.0.1

* Removed some files that are no longer required

# 2.0.0

* Reworked JSON object loading with changes to allow for:
  * Self referencing
  * Introducing a delegate that removes the need to hook for all consumers
  * The delegate also tries to ensure that objects are fully loaded by all game systems

# 1.0.5

* Added JSON object loading

# 1.0.4

* Fixed for game version 34.05 (Addressables)

# 1.0.3

* Fixed an issue that could occur when loading multiple bundles fresh that reference the same asset

# 1.0.2

* Fixed the UniverseLib dependency (Both included DLL and missing initialisation)

# 1.0.1

* Fixed a bug with reloading bundles in the same session. Added BepInEx BE dependency properly.

# 1.0.0

* Initial Thunderstore (BepInEx BE) version