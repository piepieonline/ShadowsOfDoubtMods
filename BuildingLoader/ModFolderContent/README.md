# Piepieonline's CommunityCaseLoader

Load custom cases and side jobs from JSON files, which can be constructed either by hand or by using my basic tool: https://www.piepieonline.com/ShadowsOfDoubt-CaseEditor/

Technically, it can load any kind of scriptable object from JSON, but only cases and side jobs are supported right now.

## Using VMails as evidence for SideJobs

VMails spawned by SideJobs using `spawnItems` can now be used as resolve evidence
* Create your `spawnItems` entry, making sure to provide `vmailThread` and set an `itemTag`
* Add your resolve question by setting: `inputType` to `item`, `automaticAnswers` to `spawnedItemTag`, and `tag` to be the same as `itemTag` from above

Important notes:
* Each SideJob requires a unique `vmailThread` - you can't reuse the same ID across to jobs or tags
* **Currently it only works with vmails sent by the purp!**

# Manual Installation

* Ensure you have BepInEx BE installed
* Extract the mod to ".\BepInEx\plugins\", so you should have ".\BepInEx\plugins\CommunityCaseLoader\CommunityCaseLoader.dll"

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/CommunityCaseLoader
