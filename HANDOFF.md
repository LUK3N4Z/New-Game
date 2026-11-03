# Handoff: Ocean Guardians Phase 6 (Path 1 — 3D Art & Production Visuals)

**Branch:** `feat/phase-6` (based on `feat/phase-5`), pushed to [LUK3N4Z/New-Game](https://github.com/LUK3N4Z/New-Game/tree/feat/phase-6).
**Unity:** 6000.6.4f1, URP.

## Status
Zero compiler errors/warnings, all 15 EditMode tests pass, and the full headless Play Mode journey (real NavMesh walks + real UI clicks: gate blocked bubble → workbench craft → kiosk Stage 2/3/4/5 purchases with gear swaps → gate confirm/mastered) passes with the new art — zero regressions. Captured frames were eyeballed: Coral portrait, kiosk, camp, gate, and shoreline water all render as intended (one sign-mirroring bug was caught in captures and fixed; see below).

## What's done

| Requirement | Where | Notes |
|---|---|---|
| A. Coral | Builder `BuildPlayer` | Composite stylized model on the existing `Visual` root: curl-cluster auburn hair, white/pupil/catchlight eyes, torus round glasses + bridge/temples (procedural mesh `Art/Characters/Coral/GlassesRing.asset`), cap with white panel + teal wave disc, shirt + yellow vest, rolled sleeves, yellow gloves, shorts, notebook on the stage-1 backpack. All Phase 5 sockets (`Backpack`, boots, tong) unchanged, so gear swaps and `ProceduralAnimator` bob/squash/celebrate work as before. |
| B. Kiosk & camp | Builder `BuildKiosk`/`BuildCamp`/`BuildRegionGate` | Timber shelter, sloped green shake roof, 3 rounded bins with lids/knobs and white pictogram discs (bottle/can/paper), "RECYCLE!" signboard + "Upgrades sold here!" notice — all facing the player (south) side. Tent got a door flap + emissive lantern (second DayNightCycle lantern light); workbench got a hammer + tool pots; campfire got crossed logs + emissive embers. Gate got a driftwood post arch + boulders on the approach side. |
| C. Pickups | Builder `BuildItemVisual` + `Art/Items/*.asset` meshes | Lathe-turned PET bottle (translucent + white cap) and green/amber glass bottle, metallic soda can with silver top + pop-tab, torus car tire with caked sand core, ghost-net torus tangle + orange buoys, faceted sea-glass gems in 3 rotating colors, ribbed scallop fan, spiral conch, foil wrapper, driftwood logs. Roots, tap colliders, save ids and glints untouched. |
| D. Water | `Art/Shaders/StylizedWater.shader` (+ material) | Depth-ramp shallow turquoise → deep azure, depth-fade foam rim, procedural animated caustics in the shallows. A sloped Seabed plane gives the depth texture real shallows to read; `ShoreWaves` wash/foam segments still ride on top. |
| E. Wildlife | Builder `BuildSeal`/`CritterPrefab` | Seal pup: mottled spots, belly, muzzle, big glinting eyes, flippers; net is now torus rings + buoy on the same `GhostNet` object the rescue disables. Crab: red-orange, eye stalks, claws, 6 legs (dig/scuttle unchanged). Sanderling: white body, grey wing/cap, beak, legs, tail (peck/hop unchanged). |
| F. Post & lighting | Builder `BuildPostProcessing`, `Art/BeachVolumeProfile.asset`, `Assets/Settings/*` | ACES tonemapping, +12 saturation, warm white balance, Bloom threshold 0.9 / intensity 0.35, soft sun shadows, shadow distance 55, depth texture enabled on both PC and Mobile pipeline assets, SSAO feature ensured on both renderer data assets (PC already had one). |

## Bug caught by looking at captures
World-space TMP text reads correctly only from its local −Z side. First art pass put kiosk signs/bin fronts on the sea side (+Z), so the real game camera saw them mirrored. Fixed by moving bin fronts, pictograms, signboards and the gate signpost/arch to the player side (−Z); re-captured frames confirm "RECYCLE!" and the notice board read correctly.

## Still to do / watch
1. **Human play pass for taste:** color grade warmth, bloom on glints at sunset/dusk (DayNightCycle tints may fight the volume), water foam width against the moving wash, caustics strength.
2. **Mobile check:** depth texture + SSAO now on for the Mobile pipeline — measure GPU cost on a real device; consider dropping SSAO on Mobile if it hurts.
3. Gate signpost sits partly behind the left bluff from the default angle; nudge it east if it bothers.
4. Coral's vest is a plain slab; a future art pass could sculpt it (and add real FBX via the existing Animator bridge without touching builder logic).
5. The seabed plane's far edge can read as a faint sandbar line in the water; widen/soften if desired.
