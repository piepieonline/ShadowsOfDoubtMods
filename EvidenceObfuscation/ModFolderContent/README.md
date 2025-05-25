# Piepieonline's EvidenceObfuscation

Replaces CityDirectoryPhoneNumbers.

Modifies the evidence presented in various places to be more obfuscated. Each change can be turned on and off separately.

* Change the city directory to list phone numbers instead of addresses. (Not all addresses have phones attached - in these cases, mostly businesses, the address will show as per normal)
  * Optionally, it can be changed to provide both pieces of information instead.
* Remove fingerprints from employee records, both printed from crunchers and found in filling cabinets (Toggled separately, unmodified by default).
* Remove fingerprints from printed government records (Unmodified by default)
* Remove details from the time of death evidence, alongside removing the entry wound evidence (Toggled seperately for guns and melee, both unmodified by default. Entry wounds are also unmodified by default)
* ~~Change sales ledgers to contain various detials instead of always being the first initialed name (first name, initials, initialed name and workplace).~~ No longer required, this was added to the base game.

# Manual Installation

* Ensure you have BepInEx BE installed
* Extract the mod to ".\BepInEx\plugins\", so you should have ".\BepInEx\plugins\EvidenceObfuscation\EvidenceObfuscation.dll"

# Source:

https://github.com/piepieonline/ShadowsOfDoubtMods/tree/master/EvidenceObfuscation
