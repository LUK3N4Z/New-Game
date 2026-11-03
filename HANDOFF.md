# Handoff: Ocean Guardians Phase 7 (Path 2 — Match-3 Recycle Sorter)

**Branch:** `feat/phase-7` (based on `feat/phase-6`), pushed to [LUK3N4Z/New-Game](https://github.com/LUK3N4Z/New-Game/tree/feat/phase-7).
**Unity:** 6000.6.4f1, URP.

## Status
Zero compiler errors/warnings. All 24 EditMode tests pass (15 previous + 9 match-3). Play Mode verified headlessly end-to-end on the real scene: walk to kiosk → "Manual Sort (+25% Bonus)" button → board opens → real tile swipes → victory payout landed exactly (+25 credits on a 20-credit bag: 20 base + 5 bonus, bag emptied, Eco paid), stats recorded, quit-to-beach closed cleanly. The board UI was captured and eyeballed (6×6 PET/CAN/BOX/GLS tiles, NET obstacles, moves badge, objectives).

## What's done

| Requirement | Where | Notes |
|---|---|---|
| A. Match-3 core | `Scripts/MiniGames/Match3/Match3Board.cs` | Pure C# (no MonoBehaviour), seeded RNG, 6×6, no pre-existing matches, snap-back invalid swaps, per-cascade step results for animation. Match-4 → Eco-Skimmer (row/column sweep by orientation), match-5 or T/L → Eco-Compactor (3×3 blast), both chain-detonate. GhostNets are immovable gravity dams, freed by adjacent matches. |
| A. Rules | `Scripts/MiniGames/Match3/RecycleSorterGame.cs` | `SorterRound` (pure): 18 moves, quotas, winning on the last move counts. Quotas derive from the bag (item→piece-type map, count+4 clamped 4–12) or baseline 8/6/4 when empty. `BonusCredits` = ceil(25%). |
| B. UI | `Scripts/UI/Match3UI.cs` (+ `Match3Tile`) | Moves badge, quota checklist with colored dots + "(Done!)", score + "Combo xN!" message, tiles built in code with swap tweens, snap-back, pop shrink, bouncy falls, skimmer stripe marks. Input: drag/swipe or tap-select-then-tap-adjacent. Quit confirms on second tap mid-round. |
| C. Kiosk | `Environment/RecyclingKiosk.cs`, `UI/HUDController.cs`, builder | Deposit page now has side-by-side **Quick Deposit** / **Manual Sort (+25% Bonus)**. Both share `DepositAllCore` (one source of deposit truth). Victory = base deposit + exact 25% bonus + 25 Eco + fanfare + speech bubble + checkpoint. Out of moves: matched pieces convert 1:1 to credits (+1 Eco per 10), bag untouched. `Match3Completed(won, credits)` feeds DayStats (`SorterWinsToday`, shown via tally) and SaveManager lifetime rounds/wins. |
| 2. Audio | `Audio/AudioManager.cs` | `PlayTileSelect` (pop), `PlayMatchChime(cascade)` rising pitch per chain, `PlaySplash` on skimmer spawn, `PlaySorterVictory` (brass fanfare). |

## Decisions worth knowing
- Tile icons are short text labels (PET/CAN/BOX/GLS/NET) on bin-colored tiles — swap for sprite icons when the art pass provides them (`Match3Tile.Label` is the seam).
- Out-of-moves "base credits" = 1 credit per matched piece; tune in `Match3UI.FinishOutOfMoves` if economy balancing says otherwise.
- The sorter victory deposit also fires the normal `LitterDeposited` event, so quest deposit steps progress from sorting too.

## Still to do
1. **Human play pass:** swipe feel on a real touchscreen (36px threshold), cascade pacing, combo readability, and whether 18 moves vs bag-based quotas feels fair at bigger bag sizes.
2. **Audio pass:** match-chime pitch ladder and splash volume against the ambient mix.
3. Possible balance: ghost-net density (2 per board) and quota clamps (4–12) after a few real rounds.
