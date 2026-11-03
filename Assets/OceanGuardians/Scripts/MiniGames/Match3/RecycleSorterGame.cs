using System.Collections.Generic;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Items;

namespace OceanGuardians.MiniGames.Match3
{
    public enum SorterState { Playing, Won, OutOfMoves }

    /// <summary>Pure round rules: move limit, quotas, win/out-of-moves. No scene dependencies.</summary>
    public class SorterRound
    {
        public Dictionary<Match3PieceType, int> Quotas { get; }
        public Dictionary<Match3PieceType, int> Progress { get; } = new();
        public int MovesLeft { get; private set; }
        public int Score { get; private set; }
        public int TotalMatched { get; private set; }
        public SorterState State { get; private set; } = SorterState.Playing;

        public SorterRound(Dictionary<Match3PieceType, int> quotas, int moves = 18)
        {
            Quotas = quotas;
            MovesLeft = moves;
            foreach (var k in quotas.Keys) Progress[k] = 0;
        }

        public bool QuotaMet(Match3PieceType type) =>
            !Quotas.TryGetValue(type, out int need) || (Progress.TryGetValue(type, out int have) && have >= need);

        public bool AllQuotasMet
        {
            get { foreach (var k in Quotas.Keys) if (!QuotaMet(k)) return false; return true; }
        }

        /// <summary>Applies one successful swap's cleared counts. Winning on the last move counts.</summary>
        public void ApplyMove(IReadOnlyDictionary<Match3PieceType, int> clearedByType, int cascades, int score = 0)
        {
            if (State != SorterState.Playing) return;
            MovesLeft = Mathf.Max(0, MovesLeft - 1);
            Score += score;
            foreach (var kv in clearedByType)
            {
                TotalMatched += kv.Value;
                if (Progress.ContainsKey(kv.Key)) Progress[kv.Key] += kv.Value;
            }
            if (AllQuotasMet) State = SorterState.Won;
            else if (MovesLeft <= 0) State = SorterState.OutOfMoves;
        }
    }

    /// <summary>
    /// Scene-side controller: owns lifetime sorter stats for SaveManager, builds quotas from
    /// Coral's bag, and holds the shared +25% bonus math. Match3UI drives the board visuals.
    /// </summary>
    public class RecycleSorterGame : MonoBehaviour
    {
        public static RecycleSorterGame Instance { get; private set; }

        public int LifetimeRounds { get; private set; }
        public int LifetimeWins { get; private set; }

        void Awake() => Instance = this;

        /// <summary>The victory bonus: +25% of base deposit credits, rounded up so it never vanishes.</summary>
        public static int BonusCredits(int baseCredits) => baseCredits <= 0 ? 0 : Mathf.CeilToInt(baseCredits * 0.25f);

        public const int VictoryEcoTokens = 25;

        /// <summary>Quotas from what Coral is actually carrying (mapped by item), else a friendly baseline.</summary>
        public static Dictionary<Match3PieceType, int> BuildQuotas(BagSystem bag)
        {
            var counts = new Dictionary<Match3PieceType, int>();
            if (bag != null)
            {
                for (int i = 0; i < bag.Bag.Capacity; i++)
                {
                    var stack = bag.Bag.Slots[i];
                    if (stack?.item is not LitterItemData litter || stack.quantity <= 0) continue;
                    var type = PieceTypeFor(litter);
                    counts[type] = (counts.TryGetValue(type, out int n) ? n : 0) + stack.quantity;
                }
            }
            if (counts.Count == 0)
                return new Dictionary<Match3PieceType, int>
                {
                    { Match3PieceType.Plastic, 8 }, { Match3PieceType.Metal, 6 }, { Match3PieceType.Glass, 4 },
                };
            var quotas = new Dictionary<Match3PieceType, int>();
            foreach (var kv in counts) quotas[kv.Key] = Mathf.Clamp(kv.Value + 4, 4, 12);
            return quotas;
        }

        public static Match3PieceType PieceTypeFor(LitterItemData litter)
        {
            if (litter.Id == "glass_jar") return Match3PieceType.Glass;
            return litter.bin switch
            {
                RecycleBin.Blue => Match3PieceType.Plastic,
                RecycleBin.Green => Match3PieceType.Metal,
                _ => Match3PieceType.Paper,
            };
        }

        /// <summary>Called by Match3UI when a round finishes; stats feed DayStats + SaveManager.</summary>
        public void RecordResult(bool won)
        {
            LifetimeRounds++;
            if (won) LifetimeWins++;
        }

        public void RestoreStats(int rounds, int wins)
        {
            LifetimeRounds = rounds;
            LifetimeWins = wins;
        }
    }
}
