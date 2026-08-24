# Bookcase office

An office whose open floor is filled with back-to-back bookcase islands as well as desk
cubicles, with its own company identity and its own occupation who works
standing at a bookcase rather than sitting at a desk.

## Files

| File | Type | Base (`copyFrom`) | Overrides |
|---|---|---|---|
| `ContentRemix_Archivist` | `OccupationPreset` | `OccupationPreset\|Accountant` | `jobAIPosition` (workPosition), `jobPostion` (workCounter), `ownsWorkPosition`, `jobFillPriority`, `preferredRooms`, `actionSetup` |
| `ContentRemix_BookcaseOfficeStructure` | `CompanyStructurePreset` | `CompanyStructurePreset\|MediumOffice` | `companyStructure` — full tree rewritten to insert `ContentRemix_Archivist` `[2-2]` under `OfficeManager` |
| `ContentRemix_BookcaseOfficeCompany` | `CompanyPreset` | `CompanyPreset\|MediumOffice` | `structure`, `suffixList` |
| `ContentRemix_BookcaseOffice` | `AddressPreset` | `AddressPreset\|MediumOffice` | `company`, `important`, `maxInstances`, `compatible`, `roomConfig` |
| `ContentRemix_1x1OfficeBookcase` | `FurnitureClass` | `FurnitureClass\|1x1OfficeCubicle` | `nodeRules` — all three redirected from the donor class to self |
| `ContentRemix_BookcaseWorkActions` | `InteractableActionsPreset` | `InteractableActionsPreset\|StackShelves` | `actions` — keeps `AI_StackShelves`, drops `AI_ShopForCroceries` |
| `ContentRemix_OfficeBookcaseInteractable` | `InteractablePreset` | `InteractablePreset\|Bookcase` | `specialCaseFlag` (3 = `workCounter`), `actionsPreset` |
| `ContentRemix_OfficeBookcase` | `FurniturePreset` | `FurniturePreset\|LargeBookcase` | `classes`, `universalDesignStyle`, `onlyAllowInFollowing`, `allowedInAddressesOfType`, `allowedRoomFilters`, `isWorkPosition`, `integratedInteractables` |
| `ContentRemix_BookcaseIslandX4` | `FurnitureCluster` | `FurnitureCluster\|OfficeCubicleIslandX4SpaceLeft` | `clusterElements` (4), `zeroNodeClasses` |
| `DDSContent/names.company.suffix.officerecords.csv` | — | — | `records` → company name suffix |
| `DDSContent/jobs.csv` | — | — | `contentremix_archivist` → "Archivist" |
| `DDSContent/names.rooms.csv` | — | — | `contentremix_bookcaseoffice` → "Office", the evidence window `Type` line |

All `.sodso.json`; every preset is a clone, nothing is patched in place.

## Why these particular overrides

- **`nodeRules` redirected to self.** A cloned `FurnitureClass` inherits rules pointing at the
  *original* class, so an unedited clone would be constrained against desks and never reward
  its own kind. Two of the three are `cantFeature`.
- **`universalDesignStyle` + `allowedRoomFilters`.** `LargeBookcase` ships styles and filters
  for homes and pawn shops; neither reaches an office. Both are load-bearing.
- **`onlyAllowInFollowing` + `allowedInAddressesOfType`.** Clusters have no address-preset
  field, so address scoping is done on the preset and propagates up through the class's empty
  valid pool — see *The transitive gate* in `HOW-IT-WORKS.md`.
- **`specialCaseFlag: workCounter`, not `workDesk`.** The ownership pass promotes any worker
  whose `jobPostion` matches a flag on the furniture, and removes anyone with a non-empty
  `preferredRooms` that doesn't. `workCounter` separates cleanly in both directions: office
  staff cannot claim bookcases and sit in mid-air, Archivists cannot claim cubicles.
- **Both actions presets on one interactable.** `LargeBookcase`'s prefab only proves
  controller `A` exists, so a second integrated entry would likely land at the object origin.
  One interactable carrying `Bookcase` *and* the work action avoids the problem and keeps
  TakeBook / ReturnBook working.
- **`StackShelves` reused rather than a cloned `WorkAtDesk`.** It already has the wanted shape
  — `stackingObjects` idle, no arms override, `facing: interactableSetting` — and matches the
  bookcase's standing `useSetting`.

## TODO

Convert to a lawyers office
