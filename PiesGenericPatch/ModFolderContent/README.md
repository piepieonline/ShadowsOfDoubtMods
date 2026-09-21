# Pie's Generic Patch

Various bugfix patches that I'm adding to/removing from as required.

Current fixes (\* requires a new city to be generated):

- Fixes group meetups \*
  - Groups and dates will actually meet up and in more locations than just the diners
  - Group flyers are fixed to actually gives times and members as they should
- Adds retirees \*
  - The 'retired' occupation is unused by default in SOD, but there are jobs and cases that assume it exists. This ensures that there are people in that occupation.
- Fixes NPCs needing the bathroom too often (and other needs)
  - A citizen talking or interacting caused their needs to increase too quickly, resulting in lots of extra bathroom usage. This prevents the double counting that was occurring
- Fixes Type E handwriting
  - The Type E handwriting preset pointed at the Denise handwriting font instead of Halogen
- Fixes shopkeepers asking for passwords they don't need
  - "Buy something" and "Opening hours" could fail (and fall through to the "need a password" response) when a sync disk lowered dialog success chance, or when the shopkeeper was involved in an active side job
- Fixes signatures
  - When sales ledgers were obfuscated, signatures were globally impacted. This changes them back to always be "F. Lastname" as a signature on legal documents. Ledgers are not affected

# Manual Installation

- Ensure you have BepInEx BE and dependencies installed
- Extract the mod to `.\BepInEx\plugins\`, so you should have `.\BepInEx\plugins\Pies_Generic_Patch\Pies_Generic_Patch.dll`

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/PiesGenericPatch
