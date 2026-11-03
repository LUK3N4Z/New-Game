# Handoff: Ocean Guardians Phase 4

**Branch:** `feat/phase-4` (based on `feat/phase-3`), pushed to [LUK3N4Z/New-Game](https://github.com/LUK3N4Z/New-Game/tree/feat/phase-4).
**Unity:** 6000.6.4f1, URP.

## Status
Compiles with zero errors/warnings. Greybox scene rebuilt headless and committed (tent, campfire, dial, wrap-up modal all present). All 13 EditMode tests pass (4 inventory + 5 quest + 4 day-cycle). Play Mode has **not** been hand-tested: sun/shadow feel over a full day, wrap-up flow, bubble legibility, and bob/squash feel all need a play pass. Set `dayLengthSeconds` low (e.g. 60) on DayNightCycle in the Inspector to test a full day quickly.

## What's done

| Requirement | Where | Notes |
|---|---|---|
| A. Daylight cycle | `Environment/DayNightCycle.cs`, `UI/SunDialUI.cs` | 9 min default (`dayLengthSeconds`), manual `AdvanceTime(fraction)`. Sun color/intensity/rotation + ambient + sky interpolate across Morning/Midday/Sunset/Dusk stops; campfire light fades on at dusk. Walking is free; car tires (stage ≥ 9 litter) and rescues advance time by `heavyTaskTime` (2%). |
| B. Day Wrap-Up | `UI/DaySummaryUI.cs`, `Core/DayStats.cs`, `Environment/BaseCampTent.cs` | Opens at end of dusk or by clicking the tent (camp at -18,-16). Fades out, warps Coral to the campfire, fades in. DayStats tallies pickups/rescues and *positive* currency deltas (spending doesn't shrink "earned today"); journal highlight lists today's new entry titles. Sleep = next day, stats reset, `PlayMorningBirdsong()`, checkpoint save. |
| C. Speech bubbles | `UI/SpeechBubbleUI.cs`, `UI/SpeechBubbleManager.cs` | Screen-space like FloatingText, so no terrain clipping at 55°. Bounce pop-in, follows anchor, tap to skip, one bubble per speaker. Lines: sea glass, bag full, quest complete, dusk, seal "Arf! Arf!" + floating `<3` hearts, kiosk "Sorted! +X Credits". |
| D. Procedural animation | `Player/ProceduralAnimator.cs` | Builder now groups Coral's parts under a `Visual` child. Speed bob/tilt/lean, spring squash (halt), grab stretch, celebrate jump (quests + rescues), grabber tong cylinder stretched hand→litter while a litter target is active. Animator bridge feeds `Speed`/`IsInteracting`/`Celebrate` when a rigged model arrives. `PlayerController.SetAnimTrigger` forwards Grab/Celebrate. |
| E. Shoreline | `Environment/ShorelineWave.cs` over existing `ShoreWaves.cs` | Sanderling flee/forage + wash already existed since Phase 2. Added: 14 undulating foam segments riding the front, and a transparent wet-sand band that soaks on surge and dries over ~9 s (runtime-instanced material). Splash footsteps already sync via `WaterZone.IsWet`. |

## Persistence
SaveData gained `currentDayNumber`, `dayTimeProgress`, and the six daily tallies + today's journal titles. Old (Phase 3) saves load fine: day defaults to 1 at morning with an empty tally. Note: DayStats re-baselines currency deltas on restore, so loaded balances never count as "earned".

## Deviations (deliberate)
- No emoji in the wrap-up/bubbles: the TMP default font lacks those glyphs, so lines use text (`<3`, "(thumbs up)") until an emoji-capable font is imported.
- "Camera pan to camp" is implemented as fade → warp → fade (the iso camera follows Coral, so warping her *is* the pan, minus motion sickness).

## Still to do
1. **Playtest a full day** (shorten `dayLengthSeconds`): lighting transitions, lantern at dusk, dusk bubble, wrap-up numbers vs actual earnings, sleep → morning + save reload.
2. **Tent mid-day rest**: confirm wrap-up + new day feels right when ending early.
3. **Bubble check at 55°** on a small (mobile) resolution; resize bubble if it crowds the minimap/quest card.
4. **Squash tuning**: bob amplitude/frequency and tong alignment while grabbing near rocks.
