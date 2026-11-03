using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using OceanGuardians.Environment;
using OceanGuardians.Items;
using OceanGuardians.Progression;

namespace OceanGuardians.Core
{
    /// <summary>
    /// Lightweight JSON save at Application.persistentDataPath/savegame.json.
    /// Saves: currencies, equipment stage, journal, bag + collection, cleared litter, rescued animals.
    /// Auto-saves on EventManager.Checkpoint (kiosk deposit, rescue), on app pause and on quit.
    /// F5 = quick-save, F9 = reload (re-opens the scene, which loads the file on Start).
    /// Runs before every other Start so restored litter/rescues are in place when zones look at them.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SaveManager : MonoBehaviour
    {
        const int Version = 1;

        [Serializable]
        class StackSave
        {
            public string id;
            public int quantity;
        }

        [Serializable]
        class SaveData
        {
            public int version = Version;
            public int recyclingCredits, collectorCoins, ecoTokens;
            public int equipmentStage = 1;
            public List<string> journal = new();
            public List<StackSave> bag = new();
            public List<StackSave> collection = new();
            public List<string> clearedLitter = new();
            public List<string> rescued = new();
            public string currentActiveQuestId = "";
            public int currentQuestStepIndex;
            public int currentQuestStepProgress;
            public List<string> completedQuestIds = new();
            public int currentDayNumber = 1;
            public float dayTimeProgress;
            public int dayLitter, dayFinds, dayRescues, dayCredits, dayCoins, dayEco;
            public List<string> dayJournalTitles = new();
            public int dayCrafted;
            public List<StackSave> craftedItemCounts = new(); // StackSave reused: id = recipeId, quantity = lifetime crafts
            public bool region1Mastered;
            public int sorterLifetimeRounds;
            public int sorterLifetimeWins;
        }

        public static SaveManager Instance { get; private set; }

        [Tooltip("Every ItemData that can end up in a save (bag or collection). The scene builder fills this.")]
        [SerializeField] ItemData[] itemCatalog;
        [SerializeField] string fileName = "savegame.json";
        [SerializeField] bool loadOnStart = true;
        [Tooltip("F5/F9 quick-save and reload. Handy for testing; turn off for release builds if you like.")]
        [SerializeField] bool debugHotkeys = true;

        // Captured before anything is picked up: a destroyed item can't be asked for its id later.
        readonly List<(string id, LitterItem item)> sceneLitter = new();

        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        void Awake() => Instance = this;
        void OnEnable() => EventManager.Checkpoint += Save;
        void OnDisable() => EventManager.Checkpoint -= Save;

        void Start()
        {
            foreach (var item in FindObjectsByType<LitterItem>(FindObjectsInactive.Include))
                sceneLitter.Add((item.SaveId, item));
            if (loadOnStart) Load();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (!debugHotkeys || kb == null) return;
            if (kb.f5Key.wasPressedThisFrame)
            {
                Save();
                EventManager.RaiseAlert("Game saved!");
            }
            else if (kb.f9Key.wasPressedThisFrame)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // Start() reloads the file
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save(); // mobile: the OS may kill the app while it's in the background
        }

        void OnApplicationQuit() => Save();

        public void Save()
        {
            var data = Capture();
            string tmp = SavePath + ".tmp";
            try
            {
                // Write-then-swap so a crash mid-write never leaves a half-written save.
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                File.Copy(tmp, SavePath, true);
                File.Delete(tmp);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Could not save to {SavePath}: {e.Message}");
            }
        }

        public bool Load()
        {
            if (!File.Exists(SavePath)) return false;
            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null) throw new FormatException("empty file");
            }
            catch (Exception e)
            {
                // Keep the bad file for inspection instead of overwriting it on the next auto-save.
                string backup = Path.ChangeExtension(SavePath, ".corrupt.json");
                File.Copy(SavePath, backup, true);
                Debug.LogWarning($"[SaveManager] Save unreadable ({e.Message}); starting fresh. Backup: {backup}");
                return false;
            }
            Apply(data);
            return true;
        }

        SaveData Capture()
        {
            var d = new SaveData();
            var economy = EconomyManager.Instance;
            if (economy)
            {
                d.recyclingCredits = economy.Get(CurrencyType.RecyclingCredits);
                d.collectorCoins = economy.Get(CurrencyType.CollectorCoins);
                d.ecoTokens = economy.Get(CurrencyType.EcoTokens);
            }
            if (ProgressionManager.Instance) d.equipmentStage = ProgressionManager.Instance.CurrentStage;
            if (OceanJournal.Instance) d.journal.AddRange(OceanJournal.Instance.UnlockedIds);
            var quests = Quests.QuestManager.Instance;
            if (quests)
            {
                d.currentActiveQuestId = quests.CurrentActiveQuestId;
                d.currentQuestStepIndex = quests.CurrentQuestStepIndex;
                d.currentQuestStepProgress = quests.CurrentQuestStepProgress;
                d.completedQuestIds.AddRange(quests.CompletedQuestIds);
            }
            var cycle = Environment.DayNightCycle.Instance;
            if (cycle) { d.currentDayNumber = cycle.DayNumber; d.dayTimeProgress = cycle.TimeOfDay; }
            var stats = DayStats.Instance;
            if (stats)
            {
                d.dayLitter = stats.LitterCollected; d.dayFinds = stats.BeachFinds; d.dayRescues = stats.WildlifeRescued;
                d.dayCredits = stats.CreditsEarned; d.dayCoins = stats.CoinsEarned; d.dayEco = stats.EcoEarned;
                d.dayJournalTitles.AddRange(stats.JournalTitlesToday);
                d.dayCrafted = stats.CraftedToday;
            }
            var bench = Crafting.CraftingWorkbench.Instance;
            if (bench) foreach (var kv in bench.LifetimeCounts) d.craftedItemCounts.Add(new StackSave { id = kv.Key, quantity = kv.Value });
            if (Environment.RegionGate.Instance) d.region1Mastered = Environment.RegionGate.Instance.Region1Mastered;
            var sorter = MiniGames.Match3.RecycleSorterGame.Instance;
            if (sorter) { d.sorterLifetimeRounds = sorter.LifetimeRounds; d.sorterLifetimeWins = sorter.LifetimeWins; }

            var bag = BagSystem.Instance;
            if (bag)
            {
                for (int i = 0; i < bag.Bag.Capacity; i++)
                {
                    var s = bag.Bag.Slots[i];
                    if (s != null) d.bag.Add(new StackSave { id = s.item.Id, quantity = s.quantity });
                }
                foreach (var kv in bag.Collection)
                    d.collection.Add(new StackSave { id = kv.Key.Id, quantity = kv.Value });
            }

            foreach (var (id, item) in sceneLitter)
                if (item == null || !item.gameObject.activeSelf) d.clearedLitter.Add(id);

            foreach (var r in FindObjectsByType<WildlifeRescue>())
                if (r.IsRescued) d.rescued.Add(r.SaveId);
            return d;
        }

        void Apply(SaveData d)
        {
            var economy = EconomyManager.Instance;
            if (economy)
            {
                economy.SetBalance(CurrencyType.RecyclingCredits, d.recyclingCredits);
                economy.SetBalance(CurrencyType.CollectorCoins, d.collectorCoins);
                economy.SetBalance(CurrencyType.EcoTokens, d.ecoTokens);
            }
            ProgressionManager.Instance?.RestoreStage(d.equipmentStage); // before the bag: stage sets capacity
            OceanJournal.Instance?.Restore(d.journal);
            Quests.QuestManager.Instance?.RestoreState(d.currentActiveQuestId, d.currentQuestStepIndex, d.currentQuestStepProgress, d.completedQuestIds);
            Environment.DayNightCycle.Instance?.Restore(d.currentDayNumber, d.dayTimeProgress);
            DayStats.Instance?.Restore(d.dayLitter, d.dayFinds, d.dayRescues, d.dayCredits, d.dayCoins, d.dayEco, d.dayJournalTitles, d.dayCrafted);
            Crafting.CraftingWorkbench.Instance?.RestoreCounts(d.craftedItemCounts.Select(s => new System.Collections.Generic.KeyValuePair<string, int>(s.id, s.quantity)));
            Environment.RegionGate.Instance?.RestoreMastered(d.region1Mastered);
            MiniGames.Match3.RecycleSorterGame.Instance?.RestoreStats(d.sorterLifetimeRounds, d.sorterLifetimeWins);

            var byId = itemCatalog.Where(i => i != null).ToDictionary(i => i.Id);
            IEnumerable<ItemStack> Stacks(List<StackSave> list) =>
                list.Where(s => byId.ContainsKey(s.id)).Select(s => new ItemStack(byId[s.id], s.quantity));
            BagSystem.Instance?.Restore(Stacks(d.bag), Stacks(d.collection));

            var cleared = new HashSet<string>(d.clearedLitter);
            foreach (var (id, item) in sceneLitter)
                if (item != null && cleared.Contains(id))
                    item.gameObject.SetActive(false); // inactive, not destroyed: zones still count it as cleared

            var rescued = new HashSet<string>(d.rescued);
            foreach (var r in FindObjectsByType<WildlifeRescue>())
                if (rescued.Contains(r.SaveId)) r.RestoreRescued();
        }
    }
}
