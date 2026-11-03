using NUnit.Framework;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Crafting;
using OceanGuardians.Items;

namespace OceanGuardians.EditorTools.Tests
{
    /// <summary>Edit Mode tests for inventory removal and the upcycling workbench.</summary>
    public class CraftingTests
    {
        static LitterItemData Bottle()
        {
            var d = ScriptableObject.CreateInstance<LitterItemData>();
            d.SetId("plastic_bottle"); d.maxStack = 3;
            return d;
        }

        [Test]
        public void InventoryCountsAndRemovesAcrossStacks()
        {
            var bottle = Bottle();
            var inv = new Inventory(3);
            inv.TryAdd(bottle, 5); // 3 + 2 across two slots
            Assert.AreEqual(5, inv.CountOf(bottle));
            Assert.IsFalse(inv.TryRemove(bottle, 6), "not enough: no partial removal");
            Assert.AreEqual(5, inv.CountOf(bottle));
            Assert.IsTrue(inv.TryRemove(bottle, 4));
            Assert.AreEqual(1, inv.CountOf(bottle));
            Assert.IsTrue(inv.TryRemove(bottle, 1));
            Assert.AreEqual(0, inv.UsedSlots, "emptied slots are freed");
        }

        GameObject go;
        BagSystem bag;
        EconomyManager economy;
        CraftingWorkbench bench;
        LitterItemData bottle;
        CollectibleItemData glass;

        static void Send(MonoBehaviour mb, string method) =>
            mb.GetType().GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(mb, null);

        CraftingRecipeData Recipe()
        {
            var r = ScriptableObject.CreateInstance<CraftingRecipeData>();
            r.recipeId = "test_sunglasses"; r.recipeName = "Ocean Sunglasses";
            r.requiredLitter.Add(new InventoryCost { item = bottle, quantity = 3 });
            r.requiredFinds.Add(new InventoryCost { item = glass, quantity = 1 });
            r.rewardCollectorCoins = 35; r.rewardEcoTokens = 15;
            return r;
        }

        [SetUp]
        public void SetUp()
        {
            bottle = Bottle();
            glass = ScriptableObject.CreateInstance<CollectibleItemData>();
            glass.SetId("sea_glass");
            go = new GameObject("CraftingTest");
            economy = go.AddComponent<EconomyManager>();
            bag = go.AddComponent<BagSystem>();
            bench = go.AddComponent<CraftingWorkbench>();
            Send(economy, "Awake"); Send(bag, "Awake"); Send(bench, "Awake");
            bench.SetRecipes(new[] { Recipe() });
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void CraftingConsumesIngredientsAndPaysRewards()
        {
            string crafted = null;
            System.Action<string> handler = id => crafted = id;
            EventManager.ItemCrafted += handler;
            try
            {
                var recipe = bench.Recipes[0];
                Assert.IsFalse(bench.CanCraft(recipe));
                Assert.IsFalse(bench.TryCraft(recipe, out _), "empty bag cannot craft");

                bag.TryAdd(bottle); bag.TryAdd(bottle); bag.TryAdd(bottle); bag.TryAdd(bottle);
                bag.AddToCollection(glass);
                Assert.IsTrue(bench.CanCraft(recipe));
                Assert.IsTrue(bench.TryCraft(recipe, out var message), message);

                Assert.AreEqual(1, bag.Bag.CountOf(bottle), "3 of 4 bottles consumed");
                Assert.AreEqual(0, bag.CollectionCount(glass));
                Assert.AreEqual(35, economy.Get(CurrencyType.CollectorCoins));
                Assert.AreEqual(15, economy.Get(CurrencyType.EcoTokens));
                Assert.AreEqual("test_sunglasses", crafted);
                Assert.AreEqual(1, bench.LifetimeCount("test_sunglasses"));
            }
            finally { EventManager.ItemCrafted -= handler; }
        }
    }
}
