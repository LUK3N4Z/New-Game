using System.Collections.Generic;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Progression;

namespace OceanGuardians.Items
{
    /// <summary>
    /// Controller between the pure inventory model and the rest of the game:
    ///  - Bag: slot-based Inventory of litter, sized by equipment (10 slots at Stage 1, grows at bag stages).
    ///  - Collection: natural finds, kept separately, unlimited, shown in the Ocean Journal.
    /// Model changes are forwarded as EventManager events so UI never touches the model.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BagSystem : MonoBehaviour
    {
        public static BagSystem Instance { get; private set; }

        [Tooltip("Used only if there is no ProgressionManager in the scene.")]
        [SerializeField] int fallbackCapacity = 10;

        readonly Dictionary<CollectibleItemData, int> collection = new();

        public Inventory Bag { get; private set; }
        public IReadOnlyDictionary<CollectibleItemData, int> Collection => collection;

        public int MaxCapacity => ProgressionManager.Instance ? ProgressionManager.Instance.BagCapacity : fallbackCapacity;
        public int UsedSlots => Bag.UsedSlots;
        public bool IsEmpty => Bag.UsedSlots == 0;

        void Awake()
        {
            Instance = this;
            Bag = new Inventory(MaxCapacity);
            Bag.Slots.AnyValueChanged += _ => Notify();
        }

        void OnEnable() => EventManager.StagePurchased += OnStagePurchased;
        void OnDisable() => EventManager.StagePurchased -= OnStagePurchased;

        void Start() => Notify();

        public bool HasRoomFor(LitterItemData litter) => Bag.CanAdd(litter);
        public bool TryAdd(LitterItemData litter) => Bag.TryAdd(litter);

        public void AddToCollection(CollectibleItemData find, int quantity = 1)
        {
            collection.TryGetValue(find, out int n);
            collection[find] = n + quantity;
            EventManager.RaiseCollectionChanged();
        }

        public int CollectionCount(CollectibleItemData find) => collection.TryGetValue(find, out int n) ? n : 0;

        /// <summary>All-or-nothing collection removal (crafting ingredients).</summary>
        public bool TryRemoveFromCollection(CollectibleItemData find, int quantity)
        {
            if (CollectionCount(find) < quantity) return false;
            int left = collection[find] - quantity;
            if (left <= 0) collection.Remove(find); else collection[find] = left;
            EventManager.RaiseCollectionChanged();
            return true;
        }

        /// <summary>Empties the bag and returns its stacks (for the kiosk to convert).</summary>
        public List<ItemStack> EmptyAll() => Bag.RemoveAll();

        /// <summary>Save-load: replaces bag and collection contents without rewards or popups.</summary>
        public void Restore(IEnumerable<ItemStack> bagStacks, IEnumerable<ItemStack> collectionStacks)
        {
            Bag.RemoveAll();
            Bag.Resize(MaxCapacity);
            foreach (var s in bagStacks) Bag.TryAdd(s.item, s.quantity);
            collection.Clear();
            foreach (var s in collectionStacks)
                if (s.item is CollectibleItemData c) collection[c] = s.quantity;
            EventManager.RaiseCollectionChanged();
        }

        void OnStagePurchased(EquipmentStage _) => Bag.Resize(MaxCapacity); // bag upgrades add slots

        void Notify() => EventManager.RaiseBagChanged(Bag.UsedSlots, Bag.Capacity);
    }
}
