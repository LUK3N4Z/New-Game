using System;
using System.Collections.Generic;
using UnityEngine;
using OceanGuardians.Core;

namespace OceanGuardians.Progression
{
    public enum JournalCategory { MarineWildlife, OceanPollution, BeachDiscoveries, OceanScience, Recycling }

    [Serializable]
    public class JournalEntry
    {
        [Tooltip("Stable key used by items/events, e.g. \"plastic_bottle\".")]
        public string id;
        public int number;
        public string title;
        public JournalCategory category;
        [TextArea(2, 5)] public string fact;
        [Tooltip("Polaroid photo. Optional for now.")]
        public Sprite photo;

        public string CategoryLabel => category switch
        {
            JournalCategory.MarineWildlife => "Marine Wildlife",
            JournalCategory.OceanPollution => "Ocean Pollution",
            JournalCategory.BeachDiscoveries => "Beach Discoveries",
            JournalCategory.OceanScience => "Ocean Science",
            _ => "Recycling",
        };
    }

    /// <summary>
    /// The Ocean Journal: unlocks an entry (and pays Eco Tokens) the first time something is
    /// encountered. PolaroidPopup listens for EventManager.JournalUnlocked to show the snapshot.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class OceanJournal : MonoBehaviour
    {
        public static OceanJournal Instance { get; private set; }

        [SerializeField] int ecoTokensPerEntry = 5;
        [Tooltip("Pre-filled with the 10 MVP entries when the component is added.")]
        [SerializeField] List<JournalEntry> entries = DefaultEntries();

        readonly HashSet<string> unlocked = new();

        public IReadOnlyList<JournalEntry> Entries => entries;
        public int UnlockedCount => unlocked.Count;
        public IEnumerable<string> UnlockedIds => unlocked;

        void Awake() => Instance = this;

        public bool IsUnlocked(string id) => unlocked.Contains(id);

        /// <summary>Unlocks an entry by id. Returns true only the first time.</summary>
        public bool Discover(string id)
        {
            if (string.IsNullOrEmpty(id) || unlocked.Contains(id)) return false;
            var entry = entries.Find(e => e.id == id);
            if (entry == null)
            {
                Debug.LogWarning($"[OceanJournal] No entry with id '{id}'.", this);
                return false;
            }
            unlocked.Add(id);
            EconomyManager.Instance?.Add(CurrencyType.EcoTokens, ecoTokensPerEntry);
            EventManager.RaiseJournalUnlocked(entry);
            return true;
        }

        /// <summary>Save-load: marks entries unlocked silently (no popup, no Eco Tokens).</summary>
        public void Restore(IEnumerable<string> ids)
        {
            unlocked.Clear();
            foreach (var id in ids) unlocked.Add(id);
        }

        static List<JournalEntry> DefaultEntries() => new()
        {
            new() { id = "harbor_seal", number = 1, title = "Harbor Seal Pup", category = JournalCategory.MarineWildlife,
                fact = "Harbor seal pups can swim just hours after they're born! Moms leave them on the beach while they hunt, so if you see one alone, give it space and tell a lifeguard." },
            new() { id = "ghost_crab", number = 2, title = "Ghost Crab", category = JournalCategory.MarineWildlife,
                fact = "Ghost crabs are sand-coloured, so they're almost invisible on the beach. They can scuttle sideways faster than you can jog!" },
            new() { id = "sanderling", number = 3, title = "Sanderling Shorebird", category = JournalCategory.MarineWildlife,
                fact = "Sanderlings chase the waves in and out to snatch tiny animals from the wet sand. Some fly all the way from the Arctic to South America every year." },
            new() { id = "plastic_bottle", number = 4, title = "Single-Use Plastic Bottle", category = JournalCategory.OceanPollution,
                fact = "A plastic bottle can take about 450 years to break down. Even then it doesn't vanish - it just crumbles into tiny microplastics." },
            new() { id = "ghost_gear", number = 5, title = "Ghost Fishing Gear", category = JournalCategory.OceanPollution,
                fact = "Lost fishing nets keep 'fishing' all by themselves for years, trapping seals, turtles and fish. That's why they're called ghost gear." },
            new() { id = "aluminum_can", number = 6, title = "Aluminum Cans", category = JournalCategory.OceanPollution,
                fact = "Aluminum cans are 100% recyclable - over and over, forever! A recycled can can be back on the shelf as a new can in about 60 days." },
            new() { id = "sea_glass", number = 7, title = "Sea Glass", category = JournalCategory.BeachDiscoveries,
                fact = "Sea glass starts as broken bottles. Waves and sand tumble it for 20 to 40 years until it's smooth, rounded and frosty." },
            new() { id = "mollusk_shells", number = 8, title = "Mollusk Shells", category = JournalCategory.BeachDiscoveries,
                fact = "Every shell was built by a living mollusk, like a scallop or a conch, layer by layer from minerals in seawater. The shell grows as the animal grows!" },
            new() { id = "tidepools", number = 9, title = "Intertidal Tidepools", category = JournalCategory.OceanScience,
                fact = "Tidepools are mini oceans left behind when the tide goes out. Sea stars, anemones and tiny crabs wait there until the water comes back." },
            new() { id = "circular_economy", number = 10, title = "The Circular Economy", category = JournalCategory.Recycling,
                fact = "In a circular economy, nothing goes to waste. The bottle you recycle can become a new bottle, a cozy fleece jacket, or even a park bench!" },
        };
    }
}
