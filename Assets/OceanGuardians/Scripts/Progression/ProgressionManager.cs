using UnityEngine;
using OceanGuardians.Core;

namespace OceanGuardians.Progression
{
    /// <summary>
    /// Tracks which equipment stage Coral owns and derives her stats from it.
    /// Stages are bought strictly in order with Recycling Credits.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ProgressionManager : MonoBehaviour
    {
        public static ProgressionManager Instance { get; private set; }

        [Tooltip("If empty, the canonical 50 stages are generated at runtime.")]
        [SerializeField] EquipmentStageData database;
        [SerializeField] int baseBagCapacity = 10;
        [SerializeField] float baseGrabRange = 1.5f;
        [Tooltip("Stage owned at game start (1 = Basic Collection Bag).")]
        [SerializeField, Min(1)] int startingStage = 1;

        public EquipmentStageData Database => database;
        public int CurrentStage { get; private set; }
        public int BagCapacity { get; private set; }
        public float GrabRange { get; private set; }
        public EquipmentStage NextStage => database.Get(CurrentStage + 1);
        public bool IsMaxed => NextStage == null;

        void Awake()
        {
            Instance = this;
            if (database == null || database.Count == 0)
            {
                database = ScriptableObject.CreateInstance<EquipmentStageData>();
                database.PopulateDefaults();
            }
            CurrentStage = Mathf.Clamp(startingStage, 1, database.Count);
            Recalculate();
        }

        public string StageName(int stage) => database.Get(stage)?.name ?? $"Stage {stage}";

        /// <summary>Save-load: jump straight to an owned stage (no cost, no events).</summary>
        public void RestoreStage(int stage)
        {
            CurrentStage = Mathf.Clamp(stage, 1, database.Count);
            Recalculate();
        }

        public bool TryPurchaseNext(out string message)
        {
            var next = NextStage;
            if (next == null)
            {
                message = "You own every piece of equipment. True Ocean Guardian!";
                return false;
            }

            var economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(CurrencyType.RecyclingCredits, next.creditCost))
            {
                int have = economy ? economy.Get(CurrencyType.RecyclingCredits) : 0;
                message = $"Need {next.creditCost - have} more Recycling Credits for the {next.name}.";
                return false;
            }

            CurrentStage++;
            Recalculate();
            message = $"Unlocked {next.name}!";
            EventManager.RaiseStagePurchased(next);
            return true;
        }

        void Recalculate()
        {
            BagCapacity = baseBagCapacity;
            GrabRange = baseGrabRange;
            for (int s = 1; s <= CurrentStage; s++)
            {
                var stage = database.Get(s);
                BagCapacity += stage.bagCapacityBonus;
                GrabRange += stage.grabRangeBonus;
            }
        }
    }
}
