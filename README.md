# Ocean Guardians

A cozy 3D ocean-cleanup adventure for ages 8–14. It's built in Unity 6 (6000.6.4f1) with URP. Players click or tap to move. The camera is an isometric Cinemachine camera tilted 55° down.

## Quick start (greybox in about 1 minute)

1. Open this folder in **Unity Hub → Add → Add project from disk**, using editor **6000.6.4f1**.
2. Menu **Ocean Guardians → Build Greybox Beach Scene**.
3. Press **Play**.

| Input | Action |
|---|---|
| Left-click / tap ground | Green ripple; Coral walks there |
| Hold + drag (left or right mouse) | Keeps walking under the cursor (League-style) |
| Click litter / shell | Walks to grabber range, grabs it, `+1` popup + chirp |
| Click the kiosk (blue/green/yellow bins) | Opens the Recycling Kiosk (Quick Deposit / Upgrade Shop) |
| Walk near the seal pup → **Care** button (or click the pup) | Tender Care rescue: +25 Eco, Journal #1 |

Clearing all the litter triggers the **Restoration Bloom**. The sand lightens, the water clears, and crabs and sanderlings spawn.

The builder can be re-run any time. It reuses the data assets in `Assets/OceanGuardians/Data` (so your Inspector tweaks survive) and rebuilds `Assets/OceanGuardians/Scenes/Beach_Greybox.unity`.

## Code layout (`Assets/OceanGuardians/Scripts`)

| Namespace | Files | Role |
|---|---|---|
| `OceanGuardians.Core` | `GameManager`, `EconomyManager`, `EventManager` | Bootstrap; the three currencies; static event hub and the `IInteractable` interface |
| `OceanGuardians.Player` | `PlayerController`, `PlayerInteraction`, `ClickMarker` | Click/tap movement, smart context clicks, ripple marker |
| `OceanGuardians.Items` | `ItemData`, `LitterData`, `CollectibleData`, `LitterItem`, `BagSystem` | Item ScriptableObjects, world pickups, bag capacity |
| `OceanGuardians.Environment` | `RecyclingKiosk`, `WildlifeRescue`, `RestorationZone` | Kiosk deposit, seal rescue, zone bloom |
| `OceanGuardians.Progression` | `ProgressionManager`, `EquipmentStageData`, `OceanJournal` | 50-stage equipment ladder, journal (10 MVP entries) |
| `OceanGuardians.UI` | `HUDController`, `FloatingText`, `PolaroidPopup` | HUD, pickup popups, journal snapshot card |
| `Editor` | `OceanGuardiansSceneBuilder` | One-click greybox scene |

Systems never talk to UI directly. They raise events on `EventManager`, and the HUD listens.

**Rewards at a glance**

| Source | Reward |
|---|---|
| Deposit at the kiosk | Each litter item's `creditValue` in Recycling Credits, plus 1 Eco Token per 5 items |
| Natural finds | The item's `coinValue` in Collector Coins (finds take no bag space) |
| New journal entry | +5 Eco |
| Rescue | +25 Eco |
| Zone bloom | +15 Eco |

---

## Manual setup (for real art, or to understand what the builder does)

### 1. Scene hierarchy

```
Beach (scene)
├── _Game                 GameManager, EconomyManager, BagSystem, ProgressionManager, OceanJournal
├── Main Camera           Camera + CinemachineBrain
├── CM_IsoCamera          CinemachineCamera + CinemachineFollow
├── Directional Light
├── EventSystem           EventSystem + InputSystemUIInputModule (NOT StandaloneInputModule; project uses Input System only)
├── Sand                  ground mesh/terrain + NavMeshSurface
├── Water
├── Coral                 NavMeshAgent, PlayerController, PlayerInteraction, AudioSource (+ model as child)
├── RecyclingKiosk        RecyclingKiosk + collider(s)
├── SealPup_Rescue        WildlifeRescue + trigger collider, children: Visual, GhostNet, CareAura, HeartBurst
├── Litter/…              one object per piece: LitterItem + SphereCollider (trigger, r≈0.7) + visual + Sparkle particles
├── RestorationZone_X     RestorationZone (radius covers its litter)
└── HUD                   Canvas (Overlay, Scale With Screen Size 1920×1080, match 0.5), HUDController, PolaroidPopup
```

### 2. Cinemachine 55° camera (Cinemachine 3.x)

1. Select **Main Camera** and add **CinemachineBrain**.
2. Create an empty GameObject `CM_IsoCamera` and add **CinemachineCamera**.
   - Set **Tracking Target** to `Coral`.
   - Set **Lens → Field of View** to 35. A narrow FOV keeps it looking isometric.
3. Add **CinemachineFollow** to the same object.
   - **Binding Mode: World Space.** This locks the rotation so the camera never orbits.
   - **Follow Offset: (0, 19.7, -13.8).** That's a distance of 24 at 55°: `y = d·sin55`, `z = -d·cos55`.
   - **Position Damping: 0.8 / 0.8 / 0.8.** This gives a smooth glide.
4. Set the `CM_IsoCamera` **Transform rotation to (55, 0, 0)**. Leave **Rotation Control** empty so that rotation stays fixed.
5. On tablets, a larger distance (28–30) shows more of the beach.

### 3. NavMesh baking

1. On the ground (mesh or Terrain), add **NavMeshSurface**. It's in the AI Navigation package, which is already installed.
2. Agent type **Humanoid** is fine. On the `Coral` NavMeshAgent, use radius 0.35, height 1.7, speed 5, angular speed 720, acceleration 24 and base offset 0.
3. Set **Collect Objects** to *All* (or *Volume*). Static scenery such as rocks and the kiosk cuts holes automatically.
4. Click **Bake**. Re-bake whenever the beach layout changes.
5. Litter must **not** cut holes in the NavMesh. Place it after baking, or add a **NavMeshModifier → Ignore From Build** to the Litter parent.

### 4. Components on prefabs

**Coral (player)**
- `NavMeshAgent` (settings above).
- `PlayerController`:
  - `cam` = Main Camera.
  - `clickMask` = ground + interactables. Exclude Coral's own layer, or remove Coral's colliders.
  - `markerMaterial` = `FX_Soft`.
  - `animator` is optional. The parameters are: float `Speed`, triggers `Grab`, `Snip`, `Celebrate`, and bool `Crouch`. Any parameter that's missing is skipped safely.
- `PlayerInteraction`:
  - `audioSource` = the AudioSource.
  - `defaultPickupSound` is optional. A chirp is generated if it's empty.

**Litter / natural finds**
1. Create the data with *Create → Ocean Guardians → Litter Data* or *Collectible Data*.
2. Set `journalEntryId` to one of these ids: `plastic_bottle`, `aluminum_can`, `sea_glass`, `mollusk_shells`, etc. The full list of ids is in `OceanJournal.cs`.
3. Car Tire uses `requiredStage = 9` (Shoreline Collection Kit) and `bagSlots = 3`.
4. Prefab = `LitterItem` (with `data` assigned) + a SphereCollider (trigger, radius about 0.7, for an easy tap target) + a child model + an optional child ParticleSystem for the sparkle.

**Recycling Kiosk**
- `RecyclingKiosk` + colliders on the bins.
- Bins:
  - **Blue:** plastics (bottles, wrappers).
  - **Green:** metals & glass (cans, jars).
  - **Yellow:** paper & natural/bulky (driftwood, tires).
- Each `LitterData.bin` sets which bin an item counts toward.

**Seal Pup rescue**
- `WildlifeRescue` + a trigger SphereCollider, so the pup is clickable.
- Wire these references:
  - `netObject` (hidden when the pup is freed).
  - `auraObject` (shown when Coral is near).
  - `heartBurst` (a ParticleSystem with Play On Awake turned off).
  - `animalVisual` (does a happy hop).
  - `animalAnimator` is optional (triggers `Freed`).

**Restoration Zone**
- `RestorationZone` at the centre of an area. Set `radius` to cover its litter.
- Assign `sandRenderer` and `waterRenderer` (URP Lit materials; the bloom lerps `_BaseColor`).
- Assign `wildlifePrefabs` (crab, sanderling).

**HUD**
- Every field on `HUDController` and `PolaroidPopup` must be assigned. The builder's `BuildHud()` is the reference layout.
- UI text must have **Raycast Target off**. Otherwise it blocks world clicks.

**Equipment database**
- *Create → Ocean Guardians → Equipment Stage Database* fills all 50 canonical stages automatically.
- Bonuses come from each stage's name:
  - Bags and storage add bag slots.
  - Pickers and kits add grabber reach.
  - Vessels add large capacity.
- Costs follow a quadratic curve from 45 to about 8,000 Credits.
- Edit any value in the Inspector. *Right-click → Reset To Canonical 50 Stages* restores the defaults.

## Not built yet (deliberately)
- **Save/load:** progress resets each play session. Add it once the economy numbers settle.
- **Sparkle shader:** particles stand in for it for now. Replace them with a Shader Graph sparkle when Coral's real art lands.
- **Wildlife behaviour:** spawned crabs and birds stand still. Add a wander script when they get models.
