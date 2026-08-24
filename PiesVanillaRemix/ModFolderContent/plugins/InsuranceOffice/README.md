# Insurance office

A highrise office with its own company identity and a full insurance-firm roster in place of
the generic office one. No furniture, cluster or room changes — every new occupation is a
clone of a shipped desk job, so it inherits the donor's `jobAIPosition` / `jobPostion` /
`preferredRooms` and claims an ordinary office cubicle.

## Files

| File | Type | Base (`copyFrom`) | Overrides |
|---|---|---|---|
| `ContentRemix_ClaimsManager` | `OccupationPreset` | `OccupationPreset\|OfficeManager` | `jobFillPriority` |
| `ContentRemix_ClaimsAdjuster` | `OccupationPreset` | `OccupationPreset\|Accountant` | `jobFillPriority` |
| `ContentRemix_ClaimsInvestigator` | `OccupationPreset` | `OccupationPreset\|QATechnician` | `jobFillPriority` |
| `ContentRemix_Underwriter` | `OccupationPreset` | `OccupationPreset\|Accountant` | `jobFillPriority` |
| `ContentRemix_InsuranceAgent` | `OccupationPreset` | `OccupationPreset\|MarketingExecutive` | `jobFillPriority` |
| `ContentRemix_InsuranceOfficeStructure` | `CompanyStructurePreset` | `CompanyStructurePreset\|MediumOffice` | `companyStructure` — full tree rewritten |
| `ContentRemix_InsuranceOfficeCompany` | `CompanyPreset` | `CompanyPreset\|MediumOffice` | `structure`, `suffixList` |
| `ContentRemix_InsuranceOffice` | `AddressPreset` | `AddressPreset\|MediumOffice` | `company`, `important`, `maxInstances`, `compatible`, `roomConfig` |
| `ContentRemix_PolicyLedgerEvidence` | `EvidencePreset` | `EvidencePreset\|SalesRecords` | `ddsDocumentID`, `useWriter`, `factSetup`, `keyMergeOnDiscovery` |
| `ContentRemix_PolicyLedger` | `InteractablePreset` | `InteractablePreset\|SalesLedger` | `spawnEvidence`, `findEvidence`, `autoPlacement`, `subObjectClasses`, `backupClasses`, `onlyInRooms`, `limitPerAddress` |
| `DDSContent/DDS/…` | — | — | the policy schedule: one tree, two messages, nine blocks |
| `DDSContent/ddsscripts.sod.json` | — | — | six policyholder scopes, five policy values |
| `DDSContent/policyholder.lua` | — | — | the shared pick, one slot per caller |
| `DDSContent/dds.blocks.csv` | — | — | the document's block text |
| `DDSContent/evidence.names.csv` | — | — | `contentremix_policyledgerevidence` → "Policy Schedule" |
| `DDSContent/names.company.suffix.insurance.csv` | — | — | six company name suffixes |
| `DDSContent/jobs.csv` | — | — | display name per new occupation |
| `DDSContent/names.rooms.csv` | — | — | `contentremix_insuranceoffice` → "Office", the evidence window `Type` line |

All `.sodso.json`; every preset is a clone, nothing is patched in place.

## The roster

```
CompanyDirector                        [1-1]  1.0    shipped
├─ ContentRemix_ClaimsManager          [1-1]  0.836
│   ├─ ContentRemix_ClaimsAdjuster     [2-2]  0.45
│   ├─ ContentRemix_ClaimsInvestigator [1-1]  0.52
│   └─ Receptionist                    [1-1]  0.0    shipped
└─ ContentRemix_Underwriter            [1-1]  0.62
    └─ ContentRemix_InsuranceAgent     [1-1]  0.4
```

## Why these particular overrides

- **Headcount is 8, exactly `MediumOffice`'s.** The tree is the headcount, and every one of
  these eight needs a desk. Going over the donor's proven number risks
  `… doesn't have a desk at …`, which fires the surplus citizen and replaces the job with
  unemployment.
- **Every range is `[n, n]`.** `Toolbox.Rand(int, int)` is max-exclusive, so `[n, n+1]`
  collapses to `n` — the shape that made `DataAnalyst` and `CustomerServiceTechnician` dead
  content in the donor structure. Both are dropped here rather than carried over broken.
- **`jobFillPriority: 4`.** `CitizenCreator` sorts the free-job pool by priority descending
  and fills from the top; new content left at the donor's priority is what goes unfilled
  when the citizen pool runs short.
- **No `preferredRooms` override.** The Archivist needed one because it moved to a
  `workCounter`; these sit at the same desks their donors do, so the inherited list is
  already correct and re-stating it could only narrow it.
- **`CompanyDirector` and `Receptionist` left shipped.** Neither reads as insurance-specific,
  and `Receptionist` carries its own reception-desk work position that a clone would have to
  re-earn.
- **`compatible` is `OfficeHighrise` only**, with the union `roomConfig` of the presets on
  that layout — same reasoning as the bookcase office, including `BathroomEmployees`, whose
  absence would produce a dead `nullConfig` bathroom.

## The policy schedule

A ledger in an office drawer, one per branch via `AddressPreset.specialItems`, listing six
life policies. Multipage is data-only: `usePages: true` on the body element makes the
evidence window page the text, no code mod involved.

`policyholder.lua` draws from `assignedJobsDirectory` plus `deadCitizensDirectory`, all
seeded from the branch's object id:

| Weight | Setting | Measured |
|---|---|---|
| Salary band | 60 / 30 / 10 top quintile / middle / open | 63 / 34 / 4 vs uniform 20 / 40 / 40 |
| District affinity | 70% prefer the branch's own district | 73% vs a 20% baseline over five districts |
| Dead insured | 25% of ledgers carry exactly one | 26%, never more than one |

Salary is the axis rather than job type — it already encodes seniority, and a type whitelist
would silently miss modded occupations, including this folder's own five. Each entry replays
the same draw sequence and stops at its own slot, so the six are duplicate-free with no
cached state to go stale across a reload.

## Note on `important`

`ContentRemix_BookcaseOffice` is also `important` on `OfficeHighrise`. The important queue
picks uniformly among unsatisfied presets, so two guaranteed types consume two highrise units
before either is certain — fine in a city with several, worth demoting one in a small city.

## TODO:

Custom vmails
