using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Environment;
using OceanGuardians.Items;
using OceanGuardians.Progression;

namespace OceanGuardians.Quests
{
    /// <summary>Live state of one running quest. Steps are clones; the asset stays pristine.</summary>
    public class QuestInstance
    {
        public QuestData Data { get; }
        public List<QuestStepData> Steps { get; }
        public int CurrentStepIndex { get; internal set; }
        public QuestStepData CurrentStep => CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;
        public bool IsComplete => CurrentStepIndex >= Steps.Count;

        internal QuestInstance(QuestData data)
        {
            Data = data;
            Steps = data.steps.Select(s => s.Clone()).ToList();
        }
    }

    /// <summary>
    /// Guided story progression (pattern after ahmedAwedni/Unity-Quests-System: ScriptableObject
    /// quest data + decoupled C# events). Listens to EventManager, advances the current step of
    /// each active quest in order, pays rewards through EconomyManager, and starts nextQuestId.
    /// With an empty catalog the 3 built-in tutorial quests are generated, so Play works even
    /// in a scene the builder has not been re-run on.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Tooltip("All quest assets. The scene builder fills this; empty = built-in tutorial chain.")]
        [SerializeField] List<QuestData> questCatalog = new();

        readonly Dictionary<string, QuestData> byId = new();
        readonly List<QuestInstance> active = new();
        readonly List<string> completed = new();

        public IReadOnlyList<QuestInstance> ActiveQuests => active;
        public IReadOnlyList<string> CompletedQuestIds => completed;
        public QuestInstance CurrentQuest => active.Count > 0 ? active[0] : null;

        // SaveManager reads these (single linear chain = the first active quest).
        public string CurrentActiveQuestId => CurrentQuest?.Data.questId ?? "";
        public int CurrentQuestStepIndex => CurrentQuest?.CurrentStepIndex ?? 0;
        public int CurrentQuestStepProgress => CurrentQuest?.CurrentStep?.currentProgress ?? 0;

        public event Action QuestChanged;
        public event Action<QuestInstance> StepCompleted;
        public event Action<QuestInstance> QuestCompleted;

        void Awake()
        {
            Instance = this;
            if (byId.Count == 0) Initialize(questCatalog);
        }

        void OnEnable()
        {
            EventManager.ItemCollected += OnItemCollected;
            EventManager.LitterDeposited += OnLitterDeposited;
            EventManager.WildlifeRescued += OnWildlifeRescued;
            EventManager.StagePurchased += OnStagePurchased;
            EventManager.AreaReached += OnAreaReached;
            EventManager.KioskToggled += OnKioskToggled;
        }

        void OnDisable()
        {
            EventManager.ItemCollected -= OnItemCollected;
            EventManager.LitterDeposited -= OnLitterDeposited;
            EventManager.WildlifeRescued -= OnWildlifeRescued;
            EventManager.StagePurchased -= OnStagePurchased;
            EventManager.AreaReached -= OnAreaReached;
            EventManager.KioskToggled -= OnKioskToggled;
        }

        /// <summary>(Re)builds the catalog and starts the first uncompleted quest. Builder/tests call this directly.</summary>
        public void Initialize(IEnumerable<QuestData> catalog)
        {
            byId.Clear(); active.Clear(); completed.Clear();
            var list = catalog?.Where(q => q != null && !string.IsNullOrEmpty(q.questId)).ToList();
            if (list == null || list.Count == 0) list = CreateTutorialQuests();
            foreach (var q in list) byId[q.questId] = q;
            // Chain head: the quest nobody points to (falls back to catalog order).
            var pointedTo = new HashSet<string>(list.Select(q => q.nextQuestId));
            StartQuest((list.FirstOrDefault(q => !pointedTo.Contains(q.questId)) ?? list[0]).questId);
        }

        public void StartQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId) || completed.Contains(questId) || active.Any(q => q.Data.questId == questId)) return;
            if (!byId.TryGetValue(questId, out var data)) return;
            var instance = new QuestInstance(data);
            active.Add(instance);
            SatisfyAlreadyMetSteps(instance);
            QuestChanged?.Invoke();
        }

        /// <summary>Save-load: rebuild exact progress (completed ids + active quest/step/progress).</summary>
        public void RestoreState(string activeQuestId, int stepIndex, int stepProgress, IEnumerable<string> completedIds)
        {
            active.Clear(); completed.Clear();
            if (completedIds != null) completed.AddRange(completedIds);

            string toStart = activeQuestId;
            if (string.IsNullOrEmpty(toStart) || !byId.ContainsKey(toStart) || completed.Contains(toStart))
            {
                // Fresh/legacy save: resume the chain at the first quest not yet completed.
                toStart = byId.Values.FirstOrDefault(q => !completed.Contains(q.questId)
                    && byId.Values.All(o => o.nextQuestId != q.questId || completed.Contains(o.questId)))?.questId;
                stepIndex = 0; stepProgress = 0;
            }
            if (string.IsNullOrEmpty(toStart)) { QuestChanged?.Invoke(); return; }

            var instance = new QuestInstance(byId[toStart]);
            for (int i = 0; i < Mathf.Min(stepIndex, instance.Steps.Count); i++)
            {
                instance.Steps[i].currentProgress = instance.Steps[i].targetAmount;
                instance.Steps[i].isComplete = true;
            }
            instance.CurrentStepIndex = Mathf.Clamp(stepIndex, 0, instance.Steps.Count);
            if (instance.CurrentStep != null)
                instance.CurrentStep.currentProgress = Mathf.Clamp(stepProgress, 0, instance.CurrentStep.targetAmount - 1);
            active.Add(instance);
            if (instance.IsComplete) CompleteQuest(instance, silent: true);
            QuestChanged?.Invoke();
        }

        // ---------- Event handlers ----------

        void OnItemCollected(ItemData item, Vector3 _) => Progress(StepType.CollectLitter, item ? item.Id : null, 1);
        void OnLitterDeposited(int itemCount) { if (itemCount > 0) Progress(StepType.DepositLitter, "", 1); }
        void OnWildlifeRescued(string saveId) => Progress(StepType.RescueWildlife, saveId, 1);
        void OnAreaReached(string areaId) => Progress(StepType.ReachArea, areaId, 1);
        void OnKioskToggled(RecyclingKiosk kiosk) { if (kiosk != null) Progress(StepType.ReachArea, "eco_kiosk", 1); }
        void OnStagePurchased(EquipmentStage stage) => Progress(StepType.PurchaseUpgrade, $"stage_{stage.stage:00}", 1, stage.stage);

        void Progress(StepType type, string targetId, int amount, int purchasedStage = 0)
        {
            bool changed = false;
            foreach (var quest in active.ToArray()) // completion mutates the list
            {
                var step = quest.CurrentStep;
                if (step == null || step.stepType != type || !Matches(step, targetId, purchasedStage)) continue;
                changed = true;
                if (step.AddProgress(amount))
                {
                    quest.CurrentStepIndex++;
                    StepCompleted?.Invoke(quest);
                    if (quest.IsComplete) CompleteQuest(quest);
                    else SatisfyAlreadyMetSteps(quest);
                }
            }
            if (changed) QuestChanged?.Invoke();
        }

        static bool Matches(QuestStepData step, string targetId, int purchasedStage)
        {
            if (string.IsNullOrEmpty(step.targetId)) return true;
            // Buying a later stage also satisfies an earlier-stage step (player may buy ahead of the quest).
            if (step.stepType == StepType.PurchaseUpgrade && purchasedStage > 0)
                return purchasedStage >= StageNumber(step.targetId);
            return step.targetId == targetId;
        }

        /// <summary>A purchase step whose stage is already owned completes as soon as it becomes current.</summary>
        void SatisfyAlreadyMetSteps(QuestInstance quest)
        {
            var step = quest.CurrentStep;
            var progression = ProgressionManager.Instance;
            if (step == null || progression == null || step.stepType != StepType.PurchaseUpgrade) return;
            if (progression.CurrentStage < StageNumber(step.targetId)) return;
            step.AddProgress(step.targetAmount);
            quest.CurrentStepIndex++;
            if (quest.IsComplete) CompleteQuest(quest);
        }

        static int StageNumber(string targetId) =>
            int.TryParse(targetId?.Replace("stage_", ""), out int n) ? n : 1;

        void CompleteQuest(QuestInstance quest, bool silent = false)
        {
            active.Remove(quest);
            if (!completed.Contains(quest.Data.questId)) completed.Add(quest.Data.questId);

            if (!silent)
            {
                var econ = EconomyManager.Instance;
                econ?.Add(CurrencyType.RecyclingCredits, quest.Data.rewardCredits);
                econ?.Add(CurrencyType.CollectorCoins, quest.Data.rewardCollectorCoins);
                econ?.Add(CurrencyType.EcoTokens, quest.Data.rewardEcoTokens);
                if (!string.IsNullOrEmpty(quest.Data.rewardJournalEntryId))
                    OceanJournal.Instance?.Discover(quest.Data.rewardJournalEntryId);
                EventManager.RaiseAlert($"Quest complete: {quest.Data.questTitle}!");
                EventManager.RaiseCheckpoint(); // auto-save
            }
            QuestCompleted?.Invoke(quest);
            StartQuest(quest.Data.nextQuestId);
            QuestChanged?.Invoke();
        }

        /// <summary>The 3 tutorial quests, also used by the scene builder to author .asset files.</summary>
        public static List<QuestData> CreateTutorialQuests()
        {
            QuestData Make(string id, string title, string desc, int credits, int coins, int eco, string journal, string next, params QuestStepData[] steps)
            {
                var q = ScriptableObject.CreateInstance<QuestData>();
                q.name = "Quest_" + id; q.questId = id; q.questTitle = title; q.questDescription = desc;
                q.rewardCredits = credits; q.rewardCollectorCoins = coins; q.rewardEcoTokens = eco;
                q.rewardJournalEntryId = journal; q.nextQuestId = next;
                q.steps.AddRange(steps);
                return q;
            }
            QuestStepData Step(StepType t, string desc, string target, int n = 1) =>
                new() { stepType = t, stepDescription = desc, targetId = target, targetAmount = n };

            return new List<QuestData>
            {
                Make("quest_01_first_sweep", "First Sweep",
                    "Clean up the shoreline and sort your haul at the kiosk.", 50, 0, 10, "", "quest_02_cove_distress",
                    Step(StepType.CollectLitter, "Collect 5 Plastic Bottles along the shoreline", "plastic_bottle", 5),
                    Step(StepType.DepositLitter, "Deposit collected litter at the 3-Bin Kiosk", "")),
                Make("quest_02_cove_distress", "Cove Distress Call",
                    "A little seal pup needs your help in the cove!", 100, 0, 35, "harbor_seal", "quest_03_equipped",
                    Step(StepType.ReachArea, "Walk to the Stranded Wildlife Cove", "wildlife_cove"),
                    Step(StepType.RescueWildlife, "Rescue the tangled Harbor Seal Pup using Tender Care", "seal_pup")),
                Make("quest_03_equipped", "Equipped for Good",
                    "Better gear means a cleaner beach.", 0, 25, 20, "", "",
                    Step(StepType.ReachArea, "Visit the Eco-Kiosk upgrade board", "eco_kiosk"),
                    Step(StepType.PurchaseUpgrade, "Purchase Stage 2 at the Eco-Kiosk", "stage_02")),
            };
        }
    }
}
