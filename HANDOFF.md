# Handoff: Ocean Guardians Phase 2

**Branch:** `feat/phase-2` (based on `feat/core-foundation`), pushed to [LUK3N4Z/New-Game](https://github.com/LUK3N4Z/New-Game/tree/feat/phase-2).
**Unity:** 6000.6.4f1, URP.

## Status
The code compiles (no errors or warnings in the last build log). **Ocean Guardians → Build Greybox Beach Scene** builds the scene, and the result is committed. Nothing in this phase has been checked in play mode yet.

## What's done

| Requirement | Where | Notes |
|---|---|---|
| A. Inventory (model/controller/view pattern) | `Items/Inventory.cs`, `BagSystem.cs`, `ItemData.cs`, `LitterItemData.cs`, `CollectibleItemData.cs` | Slots hold stacks (`maxStack` per item). Collectibles go to a separate unlimited collection. Bag slots grow when an equipment stage adds capacity. The UI only listens to events. |
| B. RTS camera | `Camera/IsoCameraRig.cs` + builder | Mouse wheel zoom (12–36 m) slides along the 55° line, so the angle never changes. The camera leads ahead of Coral as she walks. A Cinemachine deoccluder handles rocks. A tall test "RockBluff" is in the scene. |
| C. "Collector Coins" label | `HUDController.cs`, builder | Top-bar pill is renamed and widened. |
| D. Wildlife AI | `Environment/AmbientCreature.cs`, `ShoreWaves.cs`, `RestorationZone.cs` | Crabs scuttle sideways between rocks and tidepools, dig in when Coral comes near, and come back up when she leaves. Sanderlings follow the wave front and run from waves and from Coral. One of each appears at start, three more of each when the zone blooms. |
| E. JSON save/load | `Core/SaveManager.cs` | Writes `persistentDataPath/savegame.json`. Auto-saves after a deposit, after a rescue, on pause and on quit. **F5** saves, **F9** reloads, and there's a **Delete Save File** menu item. Saves currencies, equipment stage, journal, bag, collection, picked-up litter and rescued animals. |
| F. Sparkle shader | `Shaders/SparkleGlint.shader` | A camera-facing twinkling star drawn on a Quad on each pickup. Replaces the particle sparkles. |
| Extra | HUD | **Ocean Journal panel** (J key / button): entries plus the beach collection. The kiosk modal now blocks clicks from reaching the beach, and the upgrade list scrolls. |
| Extra | `Editor/Tests/InventoryTests.cs` | 4 edit-mode tests for the bag model. |

## Still to do (in order)
1. **Run the tests.** Use **Window → General → Test Runner → EditMode**. It hasn't been checked whether tests in the `Editor` folder are picked up without an assembly definition (`.asmdef`) file. If they don't appear, give the scripts and tests their own `.asmdef` files.
2. **Playtest the scene by hand.** In particular:
   - Wheel zoom feels right, and zoom is ignored while the cursor is over the HUD.
   - Walking behind the RockBluff pulls the camera in. Check that the deoccluder doesn't jitter at the sand surface; if it does, raise `MinimumDistanceFromTarget` or put the sand on its own layer.
   - The glint shader shows up and twinkles in Game view. It's built for the URP Forward renderer, so check the Mobile renderer as well.
   - Stacking: a bottle stack fills to 3, and the bag gauge counts slots.
   - The journal panel shows text and scrolls.
   - Crabs dig in and come back up. Birds run up the beach from the waves (the waves come in about 4.5 m every 7 s).
3. **Test save/load end to end:**
   1. Pick up items, deposit, rescue the seal.
   2. Quit play mode, which saves.
   3. Play again: currencies, stage, journal, bag and collection should come back, picked-up litter should stay gone, and the seal should stay freed.
   4. Test F9 reload.
   5. Check that a fully cleared zone loads already bloomed.
4. **Check the restoration change:** the bloom no longer waits for heavy litter (the Stage 9 car tire). In Phase 1 the bloom could never happen at Stage 1.
5. **Update `README.md`** for Phase 2 (controls: wheel zoom, J, F5/F9; save location; new components). It still describes Phase 1.
6. **Optional:** open a PR from `feat/phase-2` into `feat/core-foundation` (or into a `main` branch, once one is created).

## Things to know
- **Balance changed.** Bag slots now hold stacks (Bottle 3, Can 4, Wrapper 5, Jar 2, Driftwood 2, Tire 1), so 10 slots hold far more than 10 items. The `bagSlots` field is gone.
- **Item data was regenerated.** The assets in `Assets/OceanGuardians/Data` were recreated with the new fields (`id`, `maxStack`). Save ids such as `plastic_bottle` must never change once players have save files.
- **Save ids on hand-placed litter.** Litter placed by hand gets a GUID `saveId` automatically. Duplicating an object with Ctrl+D copies the id, so clear the field on the copy to get a new one.
- **No pinch zoom yet** (needed for the mobile target). Tapping still moves Coral.
- **Creatures aren't saved.** They respawn on every load (one each at start, three more each if the zone is restored).
- **The reference repos weren't copied.** Adam Myhre's inventory and the RTS camera controller were followed as patterns only, with no code taken from them. Their GitHub pages weren't opened during this session.
