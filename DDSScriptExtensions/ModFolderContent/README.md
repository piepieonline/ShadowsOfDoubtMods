# Piepieonline's DDSScriptExtensions

Lua driven script extensions for the DDS system to enable dynamic scripted text. Looks for JSON files named `ddsscripts.sod.json` in the Bepinex mods directory.

# ddsscripts.sod.json Format

```
{
    "citizen": {
        "selected_random": {
            "seed": "tonumber(inputObject.seed)",
            "script": "return 'Random number is: ' .. math.random(0, 10)"
        },
		"owner": {
            "script": "return 'Owner is: ' .. inputObject.currentGameLocation.thisAsAddress.owners[0].citizenName"
        }
    },
    "random": {
        "alpha6": {
            "script": "return string.format('#%06X', math.random(0, 0xFFFFFF))"
        }
    },
    "city": {
        "player_name": {
            "script": "return Player.citizenName"
        }
    }
}
```

* The top level objects are categories from the DDS system, and provide context for the custom script.
* The next level are keys that will be called from the DDS system, for example:
* * |writer.custom_selected_random| - Will generate a random number for each civilian, the same each time it is called
* * |writer.custom_selected_random_1| - Will call the above function, but generate a different number with an adjusted seed
* * |writer.custom_owner| - Will show the name of the owner of the current location of the writer
* * |random.custom_alpha6| - Will generate a random 6 digit hex code each time the text is shown
* * |city.custom_player_name| - Will show the player's citizen name

When referencing scripts from the DDS system, each key needs to be prefixed with 'custom_'. If a numeric postfix is attached, the generated seed will be offset by this number.

# Debugging

There is a debugging configuration flag to log a lot of information from each script execution.
Scripts can also be reloaded at runtime by running `DDSScriptExtensions.DDSScriptExtensionsPlugin.ReloadScriptList()` in UnityExplorer

# Manual Installation

* Ensure you have BepInEx BE installed
* Extract the mod to ".\BepInEx\plugins\", so you should have ".\BepInEx\plugins\DDSScriptExtensions\DDSScriptExtensions.dll"

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/DDSScriptExtensions