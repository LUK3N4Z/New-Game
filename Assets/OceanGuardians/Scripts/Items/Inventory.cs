using System;
using System.Collections.Generic;

namespace OceanGuardians.Items
{
    /// <summary>A quantity of one item type, occupying one bag slot.</summary>
    [Serializable]
    public class ItemStack
    {
        public ItemData item;
        public int quantity;

        public ItemStack(ItemData item, int quantity)
        {
            this.item = item;
            this.quantity = quantity;
        }

        public int SpaceLeft => item.maxStack - quantity;
    }

    /// <summary>
    /// Fixed-size array that tells listeners whenever any element changes
    /// (the model half of the model / controller / view inventory pattern).
    /// </summary>
    public class ObservableArray<T>
    {
        T[] items;

        public event Action<T[]> AnyValueChanged;

        public ObservableArray(int size) => items = new T[size];

        public int Length => items.Length;
        public T this[int index] => items[index];

        public void Set(int index, T value)
        {
            items[index] = value;
            Invoke();
        }

        public void Resize(int size)
        {
            Array.Resize(ref items, size);
            Invoke();
        }

        public void Clear()
        {
            Array.Clear(items, 0, items.Length);
            Invoke();
        }

        /// <summary>Call after mutating an element in place (e.g. stack quantity).</summary>
        public void Invoke() => AnyValueChanged?.Invoke(items);
    }

    /// <summary>
    /// Pure C# slot inventory: no MonoBehaviour, no UI. Items stack into existing slots first,
    /// then take an empty slot. BagSystem owns one and forwards its changes to the HUD.
    /// </summary>
    public class Inventory
    {
        public readonly ObservableArray<ItemStack> Slots;

        public Inventory(int capacity) => Slots = new ObservableArray<ItemStack>(capacity);

        public int Capacity => Slots.Length;

        public int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < Slots.Length; i++)
                    if (Slots[i] != null) used++;
                return used;
            }
        }

        public int TotalQuantity
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Slots.Length; i++)
                    if (Slots[i] != null) total += Slots[i].quantity;
                return total;
            }
        }

        public bool CanAdd(ItemData item, int quantity = 1)
        {
            int room = 0;
            for (int i = 0; i < Slots.Length && room < quantity; i++)
            {
                var s = Slots[i];
                room += s == null ? item.maxStack : s.item == item ? s.SpaceLeft : 0;
            }
            return room >= quantity;
        }

        /// <summary>All-or-nothing add: fills matching stacks, then empty slots.</summary>
        public bool TryAdd(ItemData item, int quantity = 1)
        {
            if (item == null || quantity <= 0 || !CanAdd(item, quantity)) return false;

            for (int i = 0; i < Slots.Length && quantity > 0; i++)
            {
                var s = Slots[i];
                if (s == null || s.item != item || s.SpaceLeft <= 0) continue;
                int moved = Math.Min(s.SpaceLeft, quantity);
                s.quantity += moved;
                quantity -= moved;
            }
            for (int i = 0; i < Slots.Length && quantity > 0; i++)
            {
                if (Slots[i] != null) continue;
                int moved = Math.Min(item.maxStack, quantity);
                Slots.Set(i, new ItemStack(item, moved));
                quantity -= moved;
            }
            Slots.Invoke(); // covers quantities changed in place above
            return true;
        }

        public int CountOf(ItemData item)
        {
            int total = 0;
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] != null && Slots[i].item == item) total += Slots[i].quantity;
            return total;
        }

        /// <summary>All-or-nothing removal across stacks (crafting ingredients). Emptied slots free up.</summary>
        public bool TryRemove(ItemData item, int quantity)
        {
            if (item == null || quantity <= 0 || CountOf(item) < quantity) return false;
            for (int i = 0; i < Slots.Length && quantity > 0; i++)
            {
                var s = Slots[i];
                if (s == null || s.item != item) continue;
                int moved = Math.Min(s.quantity, quantity);
                s.quantity -= moved;
                quantity -= moved;
                if (s.quantity <= 0) Slots.Set(i, null);
            }
            Slots.Invoke();
            return true;
        }

        /// <summary>Empties every slot and returns the stacks that were in them.</summary>
        public List<ItemStack> RemoveAll()
        {
            var removed = new List<ItemStack>();
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] != null) removed.Add(Slots[i]);
            Slots.Clear();
            return removed;
        }

        /// <summary>Grows (or shrinks) the bag. Never drops items: shrinking below the used slots is refused.</summary>
        public bool Resize(int capacity)
        {
            if (capacity == Slots.Length) return true;
            if (capacity < Slots.Length)
            {
                Compact();
                if (capacity < UsedSlots) return false;
            }
            Slots.Resize(capacity);
            return true;
        }

        /// <summary>Moves stacks to the front so trailing empty slots can be trimmed.</summary>
        void Compact()
        {
            int write = 0;
            var tmp = new List<ItemStack>();
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] != null) tmp.Add(Slots[i]);
            Slots.Clear();
            foreach (var s in tmp) Slots.Set(write++, s);
        }
    }
}
