# Piepieonline's MOManager

Allows for configuration of which MurderMOs appear and statistically how often.
Needs to be run once before the configuration options will appear, and then each MO can be individually adjusted. Setting an MO to 0 or below will disable it.

## Key Points

- Adjust the statistical likelihood of each Preset and MO.
- Disable specific Presets and MOs by setting their value to 0 or below.
- Requires one launch before configuration options appear.
- Doesn't work with BepInExConfigManager, but only causes warnings in the log.

## Configuration
- Install the mod and run the game once to generate the configuration
- Use either the configuration editor in your mod manager, or BepInExConfigManager in-game to configure the probabilities

### Mod Manager (R2modman or Thunderstore Mod Manager)
- In the left hand menu, find "Config Editor"
- Search for MOManager, click it, and click "Edit Config"
- Adjust the values as desired as per the explanation below
- Click "Save" in the top right

### BepInExConfigManager
- Launch the game to the main menu
- Open BepInExConfigManager (F6 by default)
- Find MOManager in the list, and adjust the values as desired as per the explanation below
- Click save preferences at the top of the window, and close it with the same key

## Explanation

The game selects killers in two stages:

1. **Preset Selection**  
   - Each preset is added to the selection pool `frequency` times.  
   - The game randomly selects one preset from this weighted pool.

2. **MO Selection**  
   - All MOs are scored using the `pickRandomScoreRange` (which you can configure in this mod), along with other factors related to the murderer.  
   - The highest-scoring MO is selected.  
   - Victims are chosen later, also using a scoring system.

# Manual Installation

* Ensure you have BepInEx BE installed
* Extract the mod to ".\BepInEx\plugins\", so you should have ".\BepInEx\plugins\MOManager\MOManager.dll"

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/MOManager
