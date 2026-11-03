# Handoff: Ocean Guardians Phase 3

**Branch:** `feat/phase-3` (based on `feat/phase-2`), pushed to [LUK3N4Z/New-Game](https://github.com/LUK3N4Z/New-Game/tree/feat/phase-3).
**Unity:** 6000.6.4f1, URP.

## Status
Compiles with zero errors/warnings. **Ocean Guardians → Build Greybox Beach Scene** was re-run headless and the rebuilt scene + 3 quest assets are committed. All 9 EditMode tests pass (4 inventory + 5 quest). Play Mode has **not** been hand-tested yet — audio feel, pinch zoom on a real device, and the full quest chain in Play still need a pass.

## What's done

| Requirement | Where | Notes |
|---|---|---|
| A. Quest system | `Scripts/Quests/{QuestData,QuestStepData,QuestManager,QuestAreaTrigger}.cs`, assets `Data/Quest_quest_0*.asset` | Steps run in order; runtime state clones steps so assets never mutate. New hub events: `LitterDeposited(count)`, `WildlifeRescued(saveId)`, `AreaReached(areaId)`. Empty catalog = built-in tutorial chain, so any scene works. Buying a stage ahead of Quest 3 still satisfies it. |
| B. Audio engine | `Scripts/Audio/AudioManager.cs`, `Environment/WaterZone.cs` | Waves+gulls ambience (spatial from the shoreline), 16s marimba loop, 8-source SFX pool. All clips procedural until real ones are assigned. Footsteps splash in tidepools/wave wash, crunch on sand. Persistent across F9 reloads. |
| C. Pinch-zoom | `Camera/IsoCameraRig.cs`, `Player/PlayerController.cs` | The Phase-2 rig is `IsoCameraRig` (no `RTSCameraController` ever existed). 2-finger pinch zooms along the fixed 55° line, 12–36 m; two fingers cancel tap-to-move; pinches starting on HUD UI are ignored. |
| D. Quest tracker | `Scripts/UI/QuestTrackerUI.cs` + builder | Card under the minimap, checkbox steps with counters, gold-star pulse + green flash on step completion, tap header to fold. |
| E. Save + builder | `Core/SaveManager.cs`, `Editor/OceanGuardiansSceneBuilder.cs` | Save fields: `currentActiveQuestId`, `currentQuestStepIndex`, `currentQuestStepProgress`, `completedQuestIds`. Old saves resume the chain at the first uncompleted quest. |

## Deviations from the brief (deliberate)
- Quest 3 says "Stage 2: Heavy-Duty Collection Bag", but the canonical ladder has Heavy-Duty at Stage 4 (Stage 2 = Protective Gloves). Step targets `stage_02` and reads "Purchase Stage 2 at the Eco-Kiosk".
- `EventManager.OnItemCollected` etc. from the brief don't exist; quests subscribe to the real hub (`ItemCollected`, `StagePurchased`, `KioskToggled`) plus the 3 new events above.

## Still to do
1. **Playtest in Play Mode:** full chain Quest 1 → 2 → 3 with tracker updates, reward alerts, save mid-quest (F5) and reload (F9) restoring exact step progress.
2. **Listen:** procedural waves/music/SFX levels; replace `wavesClip`/`musicClip` on AudioManager with real recordings when available.
3. **Device test pinch-zoom** (or Device Simulator): pinch feel (`pinchZoomPerPixel`), two-finger tap doesn't move Coral, HUD taps don't leak through.
4. Quest 2's "Ocean Journal Entry #1" reward maps to journal id `harbor_seal` (also granted by the rescue itself; `Discover` is idempotent).
