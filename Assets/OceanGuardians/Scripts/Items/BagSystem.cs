using System.Collections.Generic;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Progression;

namespace OceanGuardians.Items
{
    /// <summary>Coral's collection bag. Capacity comes from equipment (10 slots at Stage 1).</summary>
    [DefaultExecutionOrder(-100)]
    public class BagSystem : MonoBehaviour
    {
        public static BagSystem Instance { get; private set; }

        [Tooltip("Used only if there is no ProgressionManager in the scene.")]
        [SerializeField] int fallbackCapacity = 10;

        readonly List<LitterData> contents = new();

        public int Current { get; private set; }
        public int MaxCapacity => ProgressionManager.Instance ? ProgressionManager.Instance.BagCapacity : fallbackCapacity;
        public bool IsFull => Current >= MaxCapacity;
        public IReadOnlyList<LitterData> Contents => contents;

        void Awake() => Instance = this;

        void OnEnable() => EventManager.StagePurchased += OnStagePurchased;
        void OnDisable() => EventManager.StagePurchased -= OnStagePurchased;

        void Start() => Notify();

        public bool HasRoomFor(LitterData litter) => Current + litter.bagSlots <= MaxCapacity;

        public bool TryAdd(LitterData litter)
        {
            if (!HasRoomFor(litter)) return false;
            contents.Add(litter);
            Current += litter.bagSlots;
            Notify();
            return true;
        }

        /// <summary>Empties the bag and returns what was in it (for the kiosk to convert).</summary>
        public List<LitterData> EmptyAll()
        {
            var emptied = new List<LitterData>(contents);
            contents.Clear();
            Current = 0;
            Notify();
            return emptied;
        }

        void OnStagePurchased(EquipmentStage _) => Notify(); // bag upgrades change MaxCapacity

        void Notify() => EventManager.RaiseBagChanged(Current, MaxCapacity);
    }
}
