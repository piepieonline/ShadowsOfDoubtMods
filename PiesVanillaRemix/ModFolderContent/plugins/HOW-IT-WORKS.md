# The content generation chain

Reference for ContentRemix. The mod's premise is recombining content the game already
ships, so the recurring question is never *"can this be built"* but *"which object in the
chain do I actually have to touch, and what does that cost."*

This file describes the chain stage by stage: what each object does, what keys off what,
where you can gate behaviour, and how each stage fails. [Recipes](#recipes) maps common
intents onto the minimum set of objects. The bookcase office in `BookcaseOffice/` is the
worked example throughout — see `plan.md` for its design rationale. `ShopPatch/` is a
smaller second example, patching two shipped objects in place.

Line references are into the decompiled `Assembly-CSharp`.

---

## The chain at a glance

```
LayoutConfiguration          the unit's shape — what rooms exist at all
   └─ AddressPreset          competes for the unit; owns company + room mapping
        ├─ CompanyPreset     naming, hours, salary band, headcount
        │    └─ CompanyStructurePreset → OccupationPreset   who works here
        └─ RoomConfiguration (list, matched by roomType)
             └─ RoomClassPreset        what furniture filters key off
                  └─ FurnitureCluster  arrangements of slots
                       └─ FurnitureClass    one slot's placement rules
                            └─ FurniturePreset   the actual model
                            └─ integratedInteractables   what can be *done* with it
                                 └─ InteractablePreset → InteractableActionsPreset
                                      └─ AIActionPreset   behaviour + animation
```

Five things resolve in order, and **each stage is committed before the next one runs**.
That ordering is the single most important fact in this document — most "why can't I just…"
questions are answered by it.

| Stage | Decides | Entry point |
|---|---|---|
| 1. Address | which purpose a unit serves | `NewAddress.AssignPurpose()` `NewAddress.cs:385` |
| 2. Rooms | which config each room gets | `NewAddress.GenerateRoomConfigs()` `NewAddress.cs:55` |
| 3. Clusters | where furniture *slots* go | `GetBestFurnitureClusterLocation()` `GenerationController.cs:4112` |
| 4. Furniture | which model fills each slot | `NewRoom.AddFurniture()` `NewRoom.cs:2810` |
| 5. Interactables | what can be done with that model | `FurnitureLocation.CreateInteractables()` `FurnitureLocation.cs:444` |

---

## Stage 1 — Address

### What competes

Every `AddressPreset` whose `compatible` list contains the unit's `LayoutConfiguration`
enters the running. Score is:

```
baseScore + jitter(0..0.1)
  + 5                                    if nodes fit fitsUnitSizeMin/Max
  − (existingInstances × baseScoreFrequencyPenalty)
  + (1 − |footfall − idealFootfall|) × footfallMultiplier
  + addressRules[district].scoreModifier
```

Highest score wins. Hard filters run first: `minMaxFloors`, `limitToBuildings`, and
`hardSizeLimits` (which turns the size check from a bonus into a requirement).

### Forcing the outcome

Three escape hatches, in priority order — `forcePick` → the *important* queue → the scored
list:

| Field | Effect |
|---|---|
| `forcePick` | `break`s the loop on first match and claims **every** compatible unit. Almost never what you want. |
| `important` | While zero instances exist, the preset is put in a separate queue that **beats the entire scored list**. Guarantees the first compatible unit, then reverts to normal scoring. |
| `maxInstances` | Hard cap. Checked after the `important` branch, so it never blocks the guaranteed first one. |

`important` is the idiomatic "at least one per city" lever. It only helps if the layout
itself generates — it cannot conjure a unit that the city never built.

### Company

`AddressPreset.company` drives naming, `workHours`, salary band and headcount. It must be
non-null for a workplace. The chain below it:

- **`CompanyPreset`** — `mainNamingList` / `prefixList` / `suffixList` are *keys into
  string tables*, not literals. `structure` points at the org tree.
  `minimumNumber` / `maximumNumber` / `cityPopRatio` do **not** set headcount: they are read
  only for `isSelfEmployed && autoCreate` presets, to decide how many instances of that
  one-person company the city spawns (`CitizenCreator.cs:171`). For everything else they are
  inert.
- **`CompanyStructurePreset`** — a recursive tree of `occupation` + `positionsMinimum` /
  `positionsMaximum` + `payGrade`. **This tree is the headcount.** `Company.Setup`
  (`Company.cs:56`) walks every node breadth-first and instantiates
  `Rand(positionsMinimum, positionsMaximum)` occupations for each, with no cap and no
  reference to the company preset's numbers. Subordinate nodes are rolled once for the whole
  company, not once per parent, and each new occupation picks a random boss from the parent
  node's instances.
- **`OccupationPreset`** — `jobAIPosition`, `ownsWorkPosition`, and
  `preferredRooms`, which is a `List<RoomConfiguration>` — **a list of configurations, not
  room classes**. This is why introducing a new `RoomConfiguration` quietly breaks worker
  placement: shipped occupations don't list it, so nobody prefers to work there.

### Failure modes

| Symptom | Cause |
|---|---|
| `No address preset shortlist for …` | nothing compatible with the layout; falls back to lobby preset |
| `… doesn't have a desk at …. Removing from position...` (`InteriorCreator.cs:392`) | headcount exceeds the work positions actually placed — the citizen is **fired** and replaced with an unemployed job |
| Company named with no suffix, `CityGen: Cannot find … random word list` | a naming key that isn't registered in a strings file |

---

## Stage 2 — Rooms

The layout generates rooms carrying a `RoomTypePreset`. `GenerateRoomConfigs()` walks the
address preset's `roomConfig` list and matches each entry to a room **by `roomType`**.

Unmatched rooms fall back to `roomType.forceConfiguration`; failing that they log
`Unable to find room for … setting as null` and get `nullConfig` — a dead, unfurnished
room. Surplus configs in the list are harmless and skipped silently.

So `roomConfig` must **cover every room type the layout can generate**. The practical way
to get that right is to union the lists of the presets already shipping on that layout.

Three objects are easy to confuse:

| Object | What it is |
|---|---|
| `RoomTypePreset` | what the *layout* generated — the matching key |
| `RoomConfiguration` | the address preset's treatment of that type: lighting, materials, security, and the `roomClass` |
| `RoomClassPreset` | the identity **furniture filters key off** |
| `RoomTypeFilter` | a named *set* of `RoomClassPreset`s, referenced by furniture and clusters |

The `RoomConfiguration → roomClass` hop is the one that matters downstream. Two address
types can hand the same `roomType` completely different treatments — `HighriseOffice` maps
`OfficeSpace` to `RoomConfiguration|Office`, `Laboratory` maps the same type to
`RoomConfiguration|Laboratory`. Everything about how the floor furnishes follows from that
choice, which is why **reusing a shipped `RoomConfiguration` keeps the entire downstream
chain working for free.**

---

## Stage 3 — Clusters

### Selection

Rooms are processed in `decorationPriority` order (`GenerationController.cs:3271`). For
each room, candidates are drawn from the **global** `Toolbox.Instance.allFurnitureClusters`
(`3431`) — a new cluster enters every room's pool automatically, with no registration step
and no `RoomConfiguration` edit.

Candidates are filtered on the cluster's own fields (room size, `allowedRoomFilters`,
districts, wealth, grub, inhabitants), then roll `placementChance`. Survivors are ranked by
`roomPriority` + jitter into **two queues**: `essentialFurniture` clusters, and everything
else. The essential queue is fully drained first.

The attempt budget per room is `ceil(roomSizeClusterAttemptMultiplier × nodeCount)`, or
`overridenMaxFurniture` when the room config sets `overrideMaxFurnitureClusters`. Each
success can fire `addClustersOnSuccess` / `removeClustersOnSuccess`; each failure
`removeClustersOnFail`. `limitPerRoom` / `maximumPerRoom` cap repeats.

### Placement is class-space only

A cluster is a list of `clusterElements`, each naming a **`FurnitureClass`** — never a
preset — plus a placement offset, a `facing`, and `importantToCluster`.

`FurnitureLocation` is constructed from a `List<FurnitureClass>` with no preset
(`FurnitureLocation.cs:12`). **No preset exists yet at this stage.** Consequences:

- Cluster rules cannot react to which model gets picked.
- `onlyValidIfPreviousObjectPlaced` resolves through a flag (`4476`, set at `5413`) that
  records only that the previous element's *class* found a node.
- Two elements can share a tile. This is how desks get their under-desk drawers — and the
  trap described in [Traps](#traps).

`facing` is `0=down/180°`, `1=up/0°`, `2=left/270°`, `3=right/90°`
(`GenerationController.cs:5502`), applied on top of the cluster's own rotation.

### FurnitureClass — one slot's rules

Geometry and placement legality live here, not on the preset: `objectSize`, `tall`,
`wallPiece`, `minimum`/`maximumZeroNodeWallCount`, `wallRules`, `nodeRules`,
`blockedAccess`, `awayFromClasses`, per-room/address/floor limits, and ownership
assignment.

`nodeRules` use `FurnitureRuleOption` (`FurnitureClass.cs:370`):
`0=mustFeature, 1=cantFeature, 2=canFeature`. Only `canFeature` is scoring — the other two
are **hard constraints**. Shipped classes routinely *self-reference* here, which matters
enormously when cloning: a clone inherits rules pointing at the **original** class, so it
gets constrained against the thing it was meant to replace and never rewards its own kind.
Redirect them.

---

## Stage 4 — Furniture

`AddFurniture` picks a preset per `FurnitureLocation` via `PickFurniture`
(`GenerationController.cs:5785`). The **candidate list** is cached per room per class; the
draw itself is a fresh uniform pick each time. There is no weighting.

`GetValidFurniture` (`5832`) builds that list from
`Toolbox.furnitureDesignStyleRef[room.designStyle]`, then filters on wealth,
`minimumRoomSize`, class membership, furniture group, building, district, address type,
and finally room filters against `room.preset.roomClass`.

**Presets are picked independently.** Nothing carries between slots except
`shareColours` → `matKey` (`NewRoom.cs:2871`) and `copyFromPreviouslyPlacedInCluster`,
which despite its name copies **owners only** (`NewRoom.cs:2887`). There is no way to say
"this slot's model depends on its neighbour's" — if you need correlation between slots,
it has to be expressed in stage 3, in class space.

---

## Stage 5 — Interactables

Stages 1–4 place a *model*. Everything a citizen can do with it — work at it, sit at it,
hide in it — comes from the interactables the preset carries, created in
`FurnitureLocation.CreateInteractables()` (`FurnitureLocation.cs:444`).

```
FurniturePreset
   └─ integratedInteractables[]      preset + pairToController + belongsTo
        └─ InteractablePreset             specialCaseFlag, useSetting
             └─ InteractableActionsPreset (list; actions merged, InteractablePreset.cs:11)
                  └─ InteractionAction
                       └─ AIActionPreset   what the AI does — and what it looks like
```

| Field on `integratedInteractables` | Meaning |
|---|---|
| `preset` | the `InteractablePreset` to create |
| `pairToController` | which `InteractableController` in the **prefab** supplies position + rotation |
| `belongsTo` | `nobody` / `everybody` / `person0…person3` — an index into the furniture's owner map |

`pairToController` is the hard constraint. `none` skips the entry entirely (`:459`); anything
else is looked up by `id` among the prefab's `InteractableController` components (`:532`).
A miss is **not** fatal — it logs `Unable for find corresponding controller for integrated
interactable on <name>` (`:559`) and creates the interactable at the furniture's origin with
zero rotation. Prefabs can't be authored from JSON, so the usable ids are whatever the donor
prefab happens to carry, and the donor's own `integratedInteractables` list is the only
readable inventory of them:

```
OfficeCubicle       A → OfficeCubicle (person0)   hidingPlace → HidingPlace   B → Lean
HotelDesk           A → HotelDesk (person0)       B → HotelDesk (person1)
LargeBookcase       A → Bookcase (nobody)
StreetVendorCart    A → NoodleStand (person0)     B → StreetVendor (person0)   C → ShopCounterBuy   D → Lean
```

Note `integratedInteractables` **replaces wholesale** like `clusterElements` — overriding it
to add one entry means re-listing the donor's.

### Registration

`NewNode.AddInteractable` indexes each interactable twice:

- by `specialCaseFlag` into `room.specialCaseInteractables` (`NewNode.cs:497`)
- by every `AIActionPreset` it exposes, into `room.actionReference` and
  `gameLocation.actionReference` (`:512`, `:531`), sorted by `AIPriority` — but **only for
  actions with `usableByAI: true`** (`:506`)

Those two indices are the entirety of what AI search sees. Nothing else registers.

### Work positions

Three routes exist, and they differ in who does the finding:

| Route | `jobAIPosition` | `ownsWorkPosition` | How the position is found |
|---|---|---|---|
| Owned desk | `workPosition` | true | assigned during city gen, cached on `Human.workPosition` |
| Free counter | `workPosition` | false | `FindNearestWithAction` every time the goal runs |
| Self-employed | `passedCompanyPosition` | true | `CreateSelfEmployed` at interior gen (`InteriorCreator.cs:355`) |

Across the 52 shipped occupations: 22 own a `workDesk` and 2 share one, 10 share a
`workCounter` or `workKitchen`, 1 is the self-employed street vendor, and the remaining 17
have no work position at all — mostly `jobAIPosition: random`, which just wanders the
premises.

**Owned.** During stage 4's ownership pass, `isWorkPosition` on the *furniture preset*
promotes job-holders to the front of the candidate list (`NewRoom.cs:3003`), but only those
whose `job.preset.preferredRooms` contains this room's `RoomConfiguration` **and** whose
`jobPostion` matches a `specialCaseFlag` present in `integratedInteractables`. Anyone with a
non-empty `preferredRooms` that doesn't match is removed outright. Owners are then written
into `ownerMap` in order, and `UpdateIntegratedObjectOwnership` (`FurnitureLocation.cs:655`)
walks the integrated list: when an interactable's `belongsTo` resolves to a human whose
`jobPostion` equals its `specialCaseFlag`, that human's `workPosition` is set (`:730`).

So an owned work position needs all four of:

| Object | Field |
|---|---|
| `FurniturePreset` | `isWorkPosition: true`, plus the entry in `integratedInteractables` with `belongsTo: person0` |
| `InteractablePreset` | `specialCaseFlag` = the occupation's `jobPostion` (`workDesk` = 2) |
| `FurnitureClass` | `assignBelongsToOwners ≥ 1`, `ownershipClass: desk`, `ownershipSource: addressInhabitants` |
| `OccupationPreset` | `preferredRooms` contains the `RoomConfiguration` this furniture lands in |

`ownershipClass` is deduplicating: a human already owning furniture of the same class
anywhere in the location is struck from the pool. One `desk` per worker per address, no
matter how many desk-like objects exist.

Two positions on one object is just two entries — `HotelDesk` pairs the same
`InteractablePreset` to controllers `A` and `B` with `belongsTo: person0` / `person1`, and
its class `3x1LobbyDesk` sets `assignBelongsToOwners: 2`. The count on the class and the
highest `personN` used must agree, or `UpdateIntegratedObjectOwnership` logs
`Could not find interactable owner for index N`.

**Free.** No ownership at all. `NewAIGoal` calls `FindNearestWithAction`
(`NewAIGoal.cs:805` → `Toolbox.cs:1686`) with the occupation's *first* action and
`mustBeSpecial = jobPostion`, and takes the first interactable that is indexed under that
action, carries that `specialCaseFlag`, is unused, and hasn't been moved by the player
(`Toolbox.cs:1952`). This is much the cheaper route — an interactable with the right flag
and action anywhere in the workplace is picked up with no furniture, class or occupation
edits whatsoever. It's how `CashRegister` (`workCounter`) and `Cooker` / `KitchenCounter`
(`workKitchen`) work.

**Self-employed.** `FurniturePreset.createSelfEmployed` spawns an entire `Company` around the
object at interior-gen time, pulling a housed unemployed citizen (`CityConstructor.cs:1983`).
`workPositionID` names which controller id holds the work position. Only fires if the
furniture has **no** owners yet. `StreetVendorCart`, `NoodleStand` and `FoodTruck` are the
only shipped users.

### Animation

There is no animation asset to point at. `SetIdleAnimationState` pushes an int into the
citizen Animator's `IdleAnimationState` parameter (`CitizenAnimationController.cs:234`) and
`SetArmsBoolState` does the same for the arms layer (`:55`). Both enums are compiled, and the
clips live in an Animator controller inside the game's bundles. Authoring animation from
JSON means **choosing a different existing value**, nothing more.

The two axes are independent layers:

- `IdleAnimationState` — 22 values: `none`, `sitting`, `sweeping`, `warmingHands`,
  `telephone`, `washingHands`, `cleaningBar`, `bargingDoor`, `cookingChopping`,
  `cookingFrying`, `sitAgainstWall`, `leanAgainstWall`, `showering`, `rubbingEyes`,
  `cowering`, `checkPulse`, `brushingTeeth`, `pickUpFromFloor`, `danceTwist`, `danceWatusi`,
  `stackingObjects`, `stackingObjectsCrouching`
- `ArmsBoolSate` — 12 values: `none`, `armsResting`, `armsTyping`, `armsUse`, `armsLocking`,
  `armsCuffed`, `armsConsuming`, `armsOneShotUse`, `armsSmoking`, `armsSmokingPipe`,
  `armsReading`, `armsFleeing`

Both are set from the `AIActionPreset` at four moments — activate, arrival, deactivate,
complete — each behind its own `change…` bool. `WorkAtDesk` is the canonical shape:

```
changeIdleOnActivate: true   idleAnimationOnActivate: none      ← stand up, walk over
changeIdleOnArrival:  true   idleAnimationOnArrival:  sitting   ← sit down
changeArmsOnArrival:  true   armsAnimationOnArrival:  armsTyping
changeIdleOnDeactivate: true idleAnimationOnDeactivate: none    ← stand back up
facing: interactableSetting                                     ← use useSetting.facingOffset
```

The `change…` bool is what matters, not the value: `changeIdleOnActivate` defaults to `true`,
so leaving `idleAnimationOnActivate` at `none` is an active instruction to stand.

**The action is chosen by the occupation, not the object.** `NewAIGoal` reads
`job.preset.actionSetup` and picks one preset at random from each entry's `actions` list
(`NewAIGoal.cs:930`); the object is only passed in as `passedInteractable`. The object's own
action list matters solely as a filter — `WorkAtDesk` has `confirmActionLocation: true`, so
the assigned work position must expose that exact `AIActionPreset` or the action falls
through to a search. Consequence: giving one object type a different work animation means a
new `AIActionPreset`, and therefore a new `OccupationPreset` to reference it, and therefore a
new `CompanyStructurePreset` and `CompanyPreset`. Patching the shipped `WorkAtDesk` instead
is one file, but changes every desk worker in the city.

Object-side levers, for when the model and the clip disagree:

| Lever | Effect |
|---|---|
| `useSetting.useSittingOffset` | shifts the citizen's Y to line the *sitting* clip up with the model (`Interactable.cs:6574`) |
| `useSetting.useArmsStandingOffset` | same for standing counter work (`:6579`) |
| `useSetting.usageOffset` | where the citizen stands, relative to the paired controller transform |
| `useSetting.facingOffset` | what they look at, when the action's `facing` is `interactableSetting` |
| `specialCaseFlag: forceStanding` | overrides idle to `none` on activate and arrival (`NewAIAction.cs:1327`, `:3126`) |

`forceStanding` is a `specialCaseFlag`, and `specialCaseFlag` holds exactly one value — so it
can never be combined with `workDesk`. Standing at a work position has to come from the
action preset.

Two things are out of reach entirely: which clips pin the citizen in place (an animation
event in the clip calls `CitizenAnimationEvents.SetStaticAnimation`, with a 2.5 s safety
timer in `NewAIController`), and the carry animations, which are raw ints
(`overrideCarryAnimation`, `aiCarryAnimation`) indexing the same compiled controller.

---

## Where you can gate what

The asymmetry here drives most design decisions:

| Gate | `FurniturePreset` | `FurnitureCluster` |
|---|---|---|
| Room class (`allowedRoomFilters`) | yes | yes |
| Building / district | yes | yes |
| Wealth | yes | yes |
| Room size | yes | yes |
| Design style | **yes** | no |
| **Address preset** | **yes** (`onlyAllowInFollowing` / `banInFollowing`) | **no** |

Clusters have no address-preset field at all. That looks fatal for "this arrangement only
appears in my new address type" — and there's a way around it.

### The transitive gate

Before any geometry is attempted, each cluster element is pre-checked
(`GenerationController.cs:4353`):

```csharp
flag = this.GetValidFurniture(furnitureClusterRule.furnitureClass, room, false, out list2, false, false, null);
...
else if (furnitureClusterRule.importantToCluster) { return furnitureClusterLocation; }
```

`furnitureClusterLocation` is initialised `null` (`4127`), so returning it *is* failure.

Chain it: **a class whose only member is an address-gated preset** has an empty valid pool
everywhere else → the element is invalid → `importantToCluster` → the whole cluster aborts.
Address gating on a *preset* propagates up to *cluster* placement.

This is the general mechanism for address-scoping any arrangement, and it's why a
cluster's `allowedRoomFilters` can stay broad without leaking.

---

## Recipes

### Patching a shipped object instead of cloning it

Every other recipe here clones with `copyFrom`. The alternative is to edit a shipped asset in
place, which is the right call when the change should apply everywhere the original is
already used — and the only call when the thing you need to reach isn't referenced by
anything you can clone around.

Name the file `<AssetName>.sodso_patch.json` rather than `.sodso.json`. CommunityCaseLoader
keys on that extension to pass `IsNewFile: false`, which makes `JsonLoader` look the target
up in `ScriptableObjectIDMap` by `fileType|name` and deserialise onto the **existing**
instance instead of creating one. The `fileOrder` entry stays bare (`REF:Shop`) — the loader
appends `.sodso.json` first and falls back to `.sodso_patch.json`.

```json
{ "presetName": "Shop", "name": "Shop",
  "type": "CompanyStructurePreset", "fileType": "CompanyStructurePreset",
  "companyStructure": { … } }
```

`name` must match the shipped asset exactly; there is no `copyFrom`. Fields you omit are left
alone — but **only at the top level**. The apply goes through Unity's overwrite deserialiser,
which merges nested objects field-by-field and replaces lists wholesale, so any list you
touch must be written out in full. That is the same wholesale-replacement rule as
`clusterElements` and `integratedInteractables`, reached by a different route.

Blast radius is the thing to weigh. Patching `CompanyStructurePreset|Shop` hits all four
companies that use it — Chemist, HardwareStore, PawnShop, Supermarket. Scoping to one of them
means cloning the structure and repointing that company's `CompanyPreset` at the clone, which
costs 2 files instead of 1.

### Different furniture in existing slots

**1 file.** A `FurniturePreset` cloning whatever carries the model you want, with
`classes` set to the slot class you're targeting and `onlyAllowInFollowing` +
`allowedInAddressesOfType` set to your address type.

Cheapest possible change — no new class, cluster, room or address logic. The existing
arrangement stays, and your model joins the uniform draw for that slot alongside whatever
else already occupies it.

Check three things or it silently won't appear:

- **`universalDesignStyle: true`**, or a `designStyles` list overlapping the target room's
  style. Cloned presets bring the *donor's* styles, which frequently don't overlap the
  destination. Symptom: `Unable to pick furniture of class …`.
- **`allowedRoomFilters`** must include a filter containing the destination `roomClass` —
  again, the donor's filters usually don't.
- **Co-located cluster elements** — see [Traps](#traps).

### Same furniture, different arrangement

**+2 files.** A `FurnitureClass` (clone the slot class you're mimicking, redirect
`nodeRules` to itself) and a `FurnitureCluster` (clone a similar one, override
`clusterElements` and `zeroNodeClasses`).

Make the new class the sole home of your address-gated preset and mark elements
`importantToCluster` — that buys the transitive gate, and guarantees uniformity within the
arrangement, since a one-member class has nothing else to draw.

Note `clusterElements` replaces wholesale: every field on every element must be written
out, or omitted fields default to zero — `chanceOfPlacementAttempt: 0` means *never place*.

### A new address type with its own company

**2 files, or 3 if the org chart changes.** This is the common case.

1. **`AddressPreset`** — clone the closest shipped type. Override `company`, and
   `compatible` if you want a different layout. Keep the donor's `roomConfig` list unless
   you changed layout, in which case widen it to cover the new layout's room types.
   Set `important` for a guaranteed spawn, `maxInstances` to cap while testing.
2. **`CompanyPreset`** — clone the donor's. `copyFrom` carries `structure`, `workHours`
   and the salary band across, so overriding just the naming lists is enough for a
   distinct identity.
3. **`CompanyStructurePreset`** — *only if the roles differ*. If you're reusing the donor's
   occupations, skip it: a clone that changes nothing but its name is pure noise.

Then add furniture with recipe 1 or 2, gated on the new address preset.

**Keep the shipped `RoomConfiguration`s.** Everything keyed off room class — materials,
lighting, generic furnishing, and `OccupationPreset.preferredRooms` — keeps working with
zero edits. This is what makes a new address type cheap.

### A new work position on an object

**1 file, if you can borrow a flag.** The cheapest version targets a *shared* position
(`workCounter`, `workKitchen`): an `InteractablePreset` cloning `CashRegister` or
`KitchenCounter`, added to a furniture preset's `integratedInteractables`. No ownership, no
occupation edits — `FindNearestWithAction` picks it up as long as the furniture lands
somewhere inside the workplace and the occupation already searches for that flag.

**2–3 files for an owned desk.** Add the entry to `integratedInteractables` (re-listing the
donor's), set `isWorkPosition: true` on the `FurniturePreset`, and give the `FurnitureClass`
`assignBelongsToOwners: 1` / `ownershipClass: desk`. Then confirm the occupations you expect
to sit there list this room's `RoomConfiguration` in `preferredRooms` — if they don't, you
need an `OccupationPreset` clone too, and the org chart above it.

Three things to check or it silently won't take:

- **A free `pairToController` id in the donor prefab.** You can't add controllers from JSON.
  Read the donor's existing `integratedInteractables` for the ids it proves exist; anything
  else lands at the object's origin.
- **`integratedInteractables` replaces wholesale.** Omitting the donor's entries deletes its
  hiding place, its lean spot and its lamp.
- **Headcount.** Headcount comes from `CompanyStructurePreset`, and is decided before any
  furniture exists. Adding desks doesn't hire anyone; removing them fires people
  (`InteriorCreator.cs:392`).

### A different animation at a work position

**4 files plus the company chain, or 1 with global blast radius.** Animation comes from the `AIActionPreset`, which
is chosen by the occupation, not the object — so scoping a new animation to one object type
means cloning the whole way up:

1. **`AIActionPreset`** — clone `WorkAtDesk`, override `idleAnimationOnArrival` /
   `armsAnimationOnArrival` (and the matching `change…` bools).
2. **`InteractableActionsPreset`** — clone `DeskWork`, point its action at the clone.
3. **`InteractablePreset`** — clone the work-position interactable, point `actionsPreset` at
   the clone. Keep `specialCaseFlag` as-is; that's what the occupation matches on.
4. **`OccupationPreset`** — clone, and put the new action in `actionSetup`. `WorkAtDesk` has
   `confirmActionLocation: true`, so occupation and object must name the *same* asset. Then
   `CompanyStructurePreset` + `CompanyPreset` to route the occupation into your address type.

Skipping to one file — editing the shipped `WorkAtDesk` in place — is legitimate if the
change is meant to be citywide. There is no middle ground: the object cannot override the
occupation's choice of action.

If the new idle is standing where the old one sat (or vice versa), flip
`useSetting.useSittingOffset` on the interactable to match, or the citizen floats.

### A new room class

The expensive route: `RoomClassPreset` + `RoomConfiguration` + `RoomTypeFilter`, plus
edits to every `OccupationPreset.preferredRooms` that should target it, plus room filters
on every piece of furniture that should appear there. Avoid unless the room genuinely needs
different lighting, materials or security.

---

## Worked example: the bookcase office

An office whose open floor is filled with back-to-back bookcase islands instead of desk
cubicles, plus its own company identity. Recipe 3 + recipe 2. Six files in
`BookcaseOffice/`:

| File | Type | Clones | Overrides |
|---|---|---|---|
| `ContentRemix_BookcaseOfficeCompany` | `CompanyPreset` | `MediumOffice` | `mainNamingList`, `suffixList` |
| `ContentRemix_BookcaseOffice` | `AddressPreset` | `MediumOffice` | `company`, `important`, `maxInstances`, `compatible`, `roomConfig` |
| `ContentRemix_1x1OfficeBookcase` | `FurnitureClass` | `1x1OfficeCubicle` | `nodeRules` |
| `ContentRemix_OfficeBookcase` | `FurniturePreset` | `LargeBookcase` | `classes`, `universalDesignStyle`, `onlyAllowInFollowing`, `allowedInAddressesOfType`, `allowedRoomFilters` |
| `ContentRemix_BookcaseIslandX4` | `FurnitureCluster` | `OfficeCubicleIslandX4SpaceLeft` | `clusterElements`, `zeroNodeClasses` |
| `murdermanifest` | — | — | load order |

How each general point above lands here:

- **Layout.** `compatible` is `OfficeHighrise` only, so it competes with `HighriseOffice`
  and `Laboratory`. `roomConfig` is the union of those two presets' lists — the donor
  `MediumOffice` sits on the `Office` layout and lacked `BathroomEmployees`, which would
  have produced a dead `nullConfig` bathroom.
- **Room class.** `OfficeSpace` → `RoomConfiguration|Office` → `RoomClassPreset|OfficeSpace`,
  reused untouched. Nothing downstream needed editing.
- **Why a new class.** It does two jobs: one member means every island slot is a bookcase
  by construction, and an empty pool elsewhere aborts the cluster (the transitive gate).
  It clones `1x1OfficeCubicle` rather than a bookcase class because the *slot rules* must
  stay cubicle-shaped — real bookcase classes require a wall
  (`1x1BookcaseLarge` has `minimumZeroNodeWallCount: 1`) and can't stand free in an island.
- **`nodeRules`.** All three self-referenced and were redirected. Two are `cantFeature`,
  so left inherited a bookcase would have been *forbidden* from sitting diagonally in front
  of a **desk** — a spurious coupling that would have made bookcase islands fail near desk
  islands — while never earning the `(0,-1) canFeature +1` back-to-back reward.
- **Design style.** `LargeBookcase` ships `EarlyCentury`/`MidCentury`/`60s70s`/`80sModern`;
  `OfficeCubicle` ships `Basement`/`EarlyCentury`/`Industrial`/`MidCentury`/`Street`.
  Two of five overlap, so `universalDesignStyle: true` is load-bearing.
- **Room filters.** `LargeBookcase`'s own are `GeneralFurnishing`, `PawnShop`, `LoanShark`,
  and `GeneralFurnishing` covers only Bedroom/LivingRoom/Study/Hallway/DiningRoom/Slum —
  it cannot reach an office at all. Overriding to `OfficeSpace` was required.
- **Company.** No `CompanyStructurePreset` — `copyFrom` carries `MediumOffice`'s structure
  and hours over, and the roles didn't change.

Deliberately **not** done: the `banInFollowing` patches to `OfficeCubicle` /
`ModernOfficeCubicle` from `plan.md`. Desk clusters still generate normally, so work
positions are untouched and no headcount compensation is needed.

---

## Worked example: the shop patch

Seven in-place patches in `ShopPatch/`, existing to make `ShopAssistant` — dead content in a
stock city — observable:

| File | Type | Change |
|---|---|---|
| `Shop.sodso_patch.json` | `CompanyStructurePreset` | ShopAssistant node `[0-1]` → `[2-3]`, i.e. exactly 2 per shop |
| `ShopAssistant.sodso_patch.json` | `OccupationPreset` | `jobFillPriority` 1 → 4 |
| `Supermarket.sodso_patch.json` | `AddressPreset` | `important` false → true |
| `Chemist` / `Launderette` / `PawnShop` / `SyncClinic` `.sodso_patch.json` | `AddressPreset` | `important` true → false |
| `murdermanifest.sodso.json` | — | load order |

Three independent gates had to be opened, which is the general shape of "why isn't this
occupation appearing":

- **Position creation.** `[0-1]` never yields 1 — see [Traps](#traps). This is the hard
  blocker; nothing downstream matters until it's fixed.
- **Position filling.** `CitizenCreator` sorts the free-job pool by
  `OccupationPreset.jobFillPriority` (0–4, descending) and fills from the top, so low-priority
  jobs are the ones left vacant when the citizen pool runs out. ShopAssistant shipped at 1.
- **A venue.** `ShopAssistant` alternates `SweepRoom` and `StackShelves`, and `StackShelves`
  is exposed only by the ten supermarket display interactables, gated on
  `RoomTypeFilter|Supermarket` (and `|Chemist` for shelving and magazines). Without a
  supermarket in the city, half the occupation's behaviour has nothing to target.

Marking `Supermarket` `important` is what makes that venue reliable — but only once it is the
*sole* important preset on the layout. Chemist, Launderette, PawnShop and SyncClinic all
shipped `important`, and the queue picks uniformly from whichever important presets are still
unsatisfied, so five `Retail` units would be consumed before all five were covered. Demoting
the other four moves the supermarket onto the **first** retail unit the city generates.

The cost is that those four lose their own guarantee and fall back to scored competition.
They all still have `baseScore: 3` and reasonable footfall targets so they generate normally
in any city of size, but a small city can now miss one — `SyncClinic` being the one with
gameplay consequences. That trade is fine for a test mod and wrong for a shipping one; a
shipping version would clone the `Supermarket` preset rather than demote its rivals.

`important` also **bypasses the size check entirely** (`NewAddress.cs:449`), so the
supermarket can land in a unit well under its `fitsUnitSizeMin: 60` and furnish thinly.
Setting `hardSizeLimits: true` would force a properly sized unit, at the cost of the
guarantee failing outright in a city with no large retail unit.

The whole `companyStructure` tree is written out even though one leaf changed — nested lists
replace wholesale, so a partial `subordinates` would delete RetailOwner → Shopkeeper.

`ShopAssistant` is `jobAIPosition: random` with no work position, so nothing in stage 5 needs
touching: they wander the shop floor running `SweepRoom` / `StackShelves` and never claim an
interactable. That also makes them the only vanilla occupation exercising the
`stackingObjects` idle.

---

## Traps

**Co-located cluster elements.** Shipped clusters often pair two classes on the *same
tile* with zero offset. The office cubicle clusters interleave
`1x1OfficeCubicle` with `1x1FilingCabinetUnderDesk`:

```
[0] 1x1OfficeCubicle          at (0,0)  onlyIfPrev=false
[1] 1x1FilingCabinetUnderDesk at (0,0)  onlyIfPrev=true   ← same tile
```

Because element 2's condition only records that the previous *class* placed, a stage-4
reskin of element 1 leaves element 2 firing regardless. Swap a desk for anything without a
knee-hole and the drawers spawn inside the model. **Any pure reskin of a slot needs its
cluster checked for a co-located partner.** Authoring your own cluster sidesteps it — the
bookcase island simply omits those elements.

**Cloned presets bring the donor's context.** Design styles, room filters, and class
membership all travel with `copyFrom` and are usually wrong for the destination. These
three are the standard override set for any cross-context reskin.

**Every integer min/max range in the game is max-exclusive.** `Toolbox.Rand(int, int)` is
`UnityEngine.Random.Range`, and the seeded `GetPsuedoRandomNumberContained(int, int)` clamps
to `Mathf.Max(upperRange - 1, lowerRange)` (`Toolbox.cs:3045`). The float overloads are
inclusive, so the two behave differently and only the int one bites.

`min == max` is safe (`Max(3,4)` is still `4`), but **`max == min + 1` collapses to `min`** —
which is exactly the shape you'd reach for to express "optional". Eighteen shipped
`CompanyStructurePreset` nodes are written that way, and four occupations are dead content as
a result: no company structure in the game can ever instantiate them.

| Occupation | Structure | Written | Actually |
|---|---|---|---|
| `ShopAssistant` | `Shop` | `[0-1]` | never |
| `DataAnalyst` | `MediumOffice` | `[0-1]` | never |
| `CustomerServiceTechnician` | `MediumOffice` | `[0-1]` | never |
| `BuildingJanitor` | `BuildingManagement` | `[0-1]` | never |

The quieter version of the same bug is a node that never reaches its stated maximum —
`Bar` `BarStaff [3-4]` is always 3, `EnforcerSquad` `Enforcer [2-3]` always 2. Write
`[n, n+2]` when you want "n or n+1", and `[n, n]` when you want exactly n. `ShopPatch/`
is a worked example of unsticking one of these.

**`integratedInteractables` and `clusterElements` both replace wholesale.** The failure is
subtractive and silent: the object still spawns, it just quietly lost its hiding place, its
work position or its lamp.

**`specialCaseFlag` holds one value.** `workDesk`, `forceStanding`, `hidingPlace` and the
other ~50 cases are mutually exclusive per interactable. Anything that needs two of them
needs two interactables on two controller ids.

**A missing `pairToController` is a log line, not an error.** `Unable for find corresponding
controller for integrated interactable on <name>` (`FurnitureLocation.cs:559`), and then the
interactable is created at the furniture origin anyway. An invisible work position inside a
desk behaves exactly like a working one to every check upstream.

**Cloned classes bring self-references.** `nodeRules` pointing at the original, not the
clone. Silent, and it manifests as placement failures rather than errors.

**`FurnitureClass` has its own `copyFrom` field**, colliding by name with ABL's directive.
Harmless — it's read only by editor-only `[Button]` helpers (`FurnitureClass.cs:12-29`),
never at runtime.

**`ChapterIntro.cs` string-matches `employer.preset.name != "MediumOffice"`** for intro
chapter poster/perp selection. New office types simply don't participate. It's the only
string-matched reference to these types.

---

## Verification

1. Fresh city; grep the log for `Unable to pick furniture of class`,
   `Unable to find room for`, `No address preset shortlist`,
   `CityGen: Cannot find … random word list`, and work-position errors from
   `InteriorCreator`.
2. `"debug": true` on an `AddressPreset` logs `Testing <preset> for <id>` scoring lines —
   confirms it's competing rather than being filtered on size or floor range.
3. `enableDebug` on a `FurnitureCluster` logs per-room suitability, placement-chance and
   per-element validity, including `Element: Unable to find valid furniture for this
   cluster/room combination` — the transitive gate firing.
4. Work positions: `CityGen: … doesn't have a desk at …` names the employer and prints
   `<n> employees, <m> valid work positions` — the direct headcount-versus-furniture
   readout. `Could not find interactable owner for index N` means `assignBelongsToOwners`
   is lower than the highest `personN` in `integratedInteractables`.
5. Animation: `Set idle state <x>` is logged per citizen through `SelectedDebug`, visible
   with a citizen selected in dev mode. A citizen standing inside a chair means the clip and
   `useSetting.useSittingOffset` disagree; a citizen never sitting at all means the action
   preset's `changeIdleOnArrival` is false.
6. Loader problems surface before city gen, in the BepInEx console: `Loading manifest: <mod>`
   confirms the folder was found at all, `Failed to load file: <name> (File not found)` means
   a `fileOrder` entry has no matching `.sodso.json` or `.sodso_patch.json` beside it, and
   `<name> failed to load, <ref> doesn't exist!` means a `REF:` target hasn't loaded yet —
   ordering, not spelling, most of the time.
7. Floor edit mode makes all address presets selectable (`NewAddress.cs:408`), so a type
   can be forced onto a unit. The address gate still applies there: in `GetValidFurniture`,
   `isFloorEdit` bypasses only the *building* and *district* checks, while
   `onlyAllowInFollowing` is guarded by `ignoreLimitations`, a separate argument.

---

## Corrections to `plan.md`

- **`Laboratory` does not compete on the `Office` layout** — it's on `OfficeHighrise`.
  `MediumOffice` is the only shipped preset on `Office`.
- **`PickFurniture` caches the candidate list, not the pick.** The draw is fresh per slot,
  which is why a shared-class skin swap gives a per-node mix rather than whole-room
  uniformity.
- **The cubicle clusters' "optional second element" is neither optional nor harmless** —
  it's the drawer trap above.
- **Island-only targeting doesn't need a new `RoomClassPreset` / `RoomConfiguration` /
  `RoomTypeFilter`.** A new class plus a new cluster gets there, because clusters
  self-register globally and gate transitively.
