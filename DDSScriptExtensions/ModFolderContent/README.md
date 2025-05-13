# Piepieonline's DDSScriptExtensions

Lua driven script extensions for the DDS system to enable dynamic scripted text. Looks for JSON files named `ddsscripts.sod.json` in the Bepinex mods directory and subdirectories.

# ddsscripts.sod.json Format

Subset taken from the default ddsscripts file that comes with this mod
```
{
  "values": {
    "citizen": {
      "selected_random": {
        "seed": "tonumber(inputObject.seed)",
        "script": "return 'Random number is: ' .. math.random(0, 10)"
      },
      "owner": {
        "script": "return 'Current location owner is: ' .. inputObject.currentGameLocation.thisAsAddress.owners[0].citizenName"
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
    },
    "time": {
      "twohourslater": {
        "script": "return SessionData.TimeString(tonumber(CSToString(inputObject)) + 2, true)"
      }
    }
  },
  "scopes": {
    "citizen": {
      "vmail_participant_a": {
        "scope": "citizen",
        "script": "return inputObject.thread.participantA > -1 and CityData.citizenDictionary[inputObject.thread.participantA] or nil"
      }
    }
  }
}
```

* At the top level, scripts are split into scopes (contain other scopes and values) and values
* Each script is placed into an existing scope, which means that the relevent scope object will be passed in as inputObject (So for a letter, it'll have the evidence scope).
* The next level are keys that will be called from the DDS system, for example:
  * values
    * |writer.custom_selected_random| - Will generate a random number for each civilian, the same each time it is called
    * |writer.custom_selected_random_1| - Will call the above function, but generate a different number with an adjusted seed
    * |writer.custom_owner| - Will show the name of the owner of the current location of the writer
    * |random.custom_alpha6| - Will generate a random 6 digit hex code each time the text is shown
    * |city.custom_player_name| - Will show the player's citizen name
    * |killer.lastmurder.time.custom_twohourslater| - Will show the time (in 24 hour time) 2 hours after the previous murder was committed. Time is represented as a float where the integer component is hours since world start.
  * scopes
    * |customscope_vmail_participant_a.fullname| - Will set the scope to the first participant in the vmail chain, and then get the fullname value (Only valid in a vmail scope, which is based on the citizen scope)
 
When referencing scope scripts from the DDS system, each needs to be prefixed with 'customscope_'.
When referencing value scripts from the DDS system, each needs to be prefixed with 'custom_'. If a numeric postfix is attached, the generated seed will be offset by this number.

# Debugging

There is a debugging configuration flag to log a lot of information from each script execution.
Scripts can also be reloaded at runtime by running `DDSScriptExtensions.DDSScriptExtensionsPlugin.ReloadScriptList()` in UnityExplorer

# Manual Installation

* Ensure you have BepInEx BE installed
* Extract the mod to ".\BepInEx\plugins\", so you should have ".\BepInEx\plugins\DDSScriptExtensions\DDSScriptExtensions.dll"

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/DDSScriptExtensions