using System.Collections.Generic;
using UnityEngine;
using OceanGuardians.Items;
using OceanGuardians.Progression;

namespace OceanGuardians.Core
{
    /// <summary>
    /// Today's haul, tallied purely from EventManager traffic (earnings = positive currency
    /// deltas, so spending never shrinks the wrap-up numbers). DayNightCycle.StartNewDay
    /// resets it; SaveManager persists it so a mid-day F5/F9 keeps the tally.
    /// </summary>
    [DefaultExecutionOrder(-95)]
    public class DayStats : MonoBehaviour
    {
        public static DayStats Instance { get; private set; }

        public int LitterCollected { get; private set; }
        public int BeachFinds { get; private set; }
        public int WildlifeRescued { get; private set; }
        public int CraftedToday { get; private set; }
        public int SorterWinsToday { get; private set; }
        public int CreditsEarned { get; private set; }
        public int CoinsEarned { get; private set; }
        public int EcoEarned { get; private set; }
        public List<string> JournalTitlesToday { get; } = new();

        readonly int[] lastBalances = new int[3];
        bool balancesKnown;

        void Awake() => Instance = this;

        /// <summary>Baseline from the live wallet NOW, so the next gain's delta is exact (never back-computed from an event).</summary>
        void SeedBaseline()
        {
            var econ = EconomyManager.Instance;
            if (econ == null) return;
            for (int i = 0; i < 3; i++) lastBalances[i] = econ.Get((CurrencyType)i);
            balancesKnown = true;
        }

        void OnEnable()
        {
            SeedBaseline();
            EventManager.ItemCollected += OnItemCollected;
            EventManager.WildlifeRescued += OnRescued;
            EventManager.CurrencyChanged += OnCurrencyChanged;
            EventManager.JournalUnlocked += OnJournalUnlocked;
            EventManager.ItemCrafted += OnCrafted;
            EventManager.Match3Completed += OnSorter;
        }

        void OnDisable()
        {
            EventManager.ItemCollected -= OnItemCollected;
            EventManager.WildlifeRescued -= OnRescued;
            EventManager.CurrencyChanged -= OnCurrencyChanged;
            EventManager.JournalUnlocked -= OnJournalUnlocked;
            EventManager.ItemCrafted -= OnCrafted;
            EventManager.Match3Completed -= OnSorter;
        }

        void OnItemCollected(ItemData item, Vector3 _)
        {
            if (item is LitterItemData) LitterCollected++;
            else if (item is CollectibleItemData) BeachFinds++;
        }

        void OnRescued(string _) => WildlifeRescued++;
        void OnCrafted(string _) => CraftedToday++;
        void OnSorter(bool won, int _) { if (won) SorterWinsToday++; }

        void OnJournalUnlocked(JournalEntry entry)
        {
            if (entry != null) JournalTitlesToday.Add(entry.title);
        }

        void OnCurrencyChanged(CurrencyType type, int total)
        {
            if (!balancesKnown) SeedBaseline(); // wallet appeared late: this total is the baseline, not earnings
            int delta = total - lastBalances[(int)type];
            lastBalances[(int)type] = total;
            if (delta <= 0) return;
            switch (type)
            {
                case CurrencyType.RecyclingCredits: CreditsEarned += delta; break;
                case CurrencyType.CollectorCoins: CoinsEarned += delta; break;
                case CurrencyType.EcoTokens: EcoEarned += delta; break;
            }
        }

        public void ResetDay()
        {
            LitterCollected = BeachFinds = WildlifeRescued = CraftedToday = SorterWinsToday = 0;
            CreditsEarned = CoinsEarned = EcoEarned = 0;
            JournalTitlesToday.Clear();
        }

        /// <summary>Save-load restore (balances re-baseline from the live wallet).</summary>
        public void Restore(int litter, int finds, int rescues, int credits, int coins, int eco, IEnumerable<string> journalTitles, int crafted = 0)
        {
            LitterCollected = litter; BeachFinds = finds; WildlifeRescued = rescues; CraftedToday = crafted;
            CreditsEarned = credits; CoinsEarned = coins; EcoEarned = eco;
            JournalTitlesToday.Clear();
            if (journalTitles != null) JournalTitlesToday.AddRange(journalTitles);
            SeedBaseline(); // SaveManager applies wallet balances before calling Restore, so this re-baselines exactly
        }
    }
}
