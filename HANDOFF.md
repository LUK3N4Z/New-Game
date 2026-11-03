# Handoff: Ocean Guardians Phase 5

**Branch:** `feat/phase-5` (based on `feat/phase-4`), pushed to [LUK3N4Z/New-Game](https://github.com/LUK3N4Z/New-Game/tree/feat/phase-5).
**Unity:** 6000.6.4f1, URP.

## Status
Compiles with zero errors/warnings. Greybox scene rebuilt headless and committed (workbench, gate, pause/crafting/map modals, gear accessories all present). All 15 EditMode tests pass (4 inventory + 5 quest + 4 day-cycle + 2 crafting). Play Mode not yet hand-tested.

## What's done

| Requirement | Where | Notes |
|---|---|---|
| A. Upcycling workshop | `Scripts/Crafting/{CraftingRecipeData,CraftingWorkbench}.cs`, `UI/CraftingUI.cs`, assets `Data/Recipe_*.asset` | New pure-model ops `Inventory.CountOf/TryRemove` + `BagSystem` collection remove (all-or-nothing). Bench beside the tent, modal auto-closes on walk-away like the kiosk. Craft = chime + `EventManager.ItemCrafted` + speech bubble + DayStats tally + checkpoint save. Lifetime counts persist. |
| B. Visual gear | `Player/PlayerVisualEquipment.cs` + builder | Stage 2 teal 1.3x backpack w/ pouches, Stage 3 silver tong material (+40% grab in `ProgressionManager.Recalculate`, feeds the existing `GrabRange` → `LitterItem.InteractionRange`), Stage 5 aqua boots. Purchase = celebrate jump + spin (via `ProceduralAnimator.ExtraYaw`) + starburst particles. Applies on load too. |
| C. Region 2 gate | `Environment/RegionGate.cs`, `UI/RegionGateUI.cs` | NE corner (20, 21): bluff arch, driftwood barricade, TMP signpost. < Stage 5: exact speech-bubble line. ≥ 5: confirm modal → brass fanfare, barricade clears, world-map teaser stamps Region 1 MASTERED, `region1Mastered` persists. |
| D. Pause menu | `UI/PauseMenuUI.cs` | Esc or top-bar Gear button; `Time.timeScale = 0`; master/music/SFX sliders write `AudioManager` → PlayerPrefs (`vol_master/music/sfx`, loaded on startup); controls overview; Quick Save; Save & Quit. |

## Deviations (deliberate)
- **Ghost Net was not a collectable item**, so the Ocean Sunglasses recipe could never be crafted. Added it as litter (`ghost_net`, Blue bin, 8 credits, stack 2, journal `ghost_gear`); 3 spawn on the beach like other litter.
- Stage names in the brief (Stage 2 "Heavy-Duty Bag" etc.) don't match the canonical 50-stage ladder; visual thresholds use the brief's stage *numbers* (2 bag, 3 grabber, 5 boots) without renaming ladder stages.
- "Full water walking speed" at Stage 5: there is no wading slowdown anywhere in the game, so boots are visual-only for now (adding a slowdown just to remove it later would be inventing a mechanic).
- QuestManager has no Craft step type yet; `ItemCrafted` is raised so a future quest step can hook it without touching the workbench.

## Still to do
1. **Playtest crafting:** collect 3 bottles + 1 ghost net → craft sunglasses, check counts, rewards, bubble, wrap-up "Upcycled Creations" line, save/reload keeps lifetime craft counts and remaining bag.
2. **Gear check:** buy stages 2/3/5 at the kiosk (credits needed: 45/75/165 by the cost curve) — backpack swap, silver tong + longer grab, aqua boots, spin + sparkle; reload and confirm visuals restore.
3. **Gate check:** bubble below Stage 5; modal + fanfare + map at Stage 5; barricade stays cleared after F9.
4. **Pause check:** Esc during daylight freezes the sun; sliders change volume live and survive restart; Resume/Quick Save/Quit paths. Watch for any system misbehaving under `timeScale = 0` (speech bubbles and fades use scaled/unscaled time inconsistently — bubbles freeze mid-pause, which is fine).
