using NUnit.Framework;
using UnityEngine;
using OceanGuardians.Items;

namespace OceanGuardians.EditorTools.Tests
{
    /// <summary>Edit Mode tests for the bag model (Window > General > Test Runner > EditMode).</summary>
    public class InventoryTests
    {
        static LitterItemData Litter(int maxStack)
        {
            var d = ScriptableObject.CreateInstance<LitterItemData>();
            d.maxStack = maxStack;
            return d;
        }

        [Test]
        public void StacksBeforeTakingNewSlots()
        {
            var bottle = Litter(3);
            var inv = new Inventory(2);
            for (int i = 0; i < 4; i++) Assert.IsTrue(inv.TryAdd(bottle));
            Assert.AreEqual(2, inv.UsedSlots);
            Assert.AreEqual(3, inv.Slots[0].quantity);
            Assert.AreEqual(1, inv.Slots[1].quantity);
        }

        [Test]
        public void FullBagRejectsAndKeepsContents()
        {
            var tire = Litter(1);
            var inv = new Inventory(1);
            Assert.IsTrue(inv.TryAdd(tire));
            Assert.IsFalse(inv.CanAdd(tire));
            Assert.IsFalse(inv.TryAdd(tire));
            Assert.AreEqual(1, inv.TotalQuantity);
        }

        [Test]
        public void ResizeGrowsAndNeverDropsItems()
        {
            var can = Litter(1);
            var inv = new Inventory(3);
            inv.TryAdd(can);
            inv.TryAdd(can);
            Assert.IsTrue(inv.Resize(5));
            Assert.AreEqual(5, inv.Capacity);
            Assert.IsFalse(inv.Resize(1), "shrinking below used slots must be refused");
            Assert.AreEqual(2, inv.TotalQuantity);
        }

        [Test]
        public void RemoveAllEmptiesAndRaisesChange()
        {
            var jar = Litter(2);
            var inv = new Inventory(2);
            int changes = 0;
            inv.Slots.AnyValueChanged += _ => changes++;
            inv.TryAdd(jar, 3);
            var removed = inv.RemoveAll();
            Assert.AreEqual(2, removed.Count);
            Assert.AreEqual(0, inv.UsedSlots);
            Assert.Greater(changes, 0);
        }
    }
}
