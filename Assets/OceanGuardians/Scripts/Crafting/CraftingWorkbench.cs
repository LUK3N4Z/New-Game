using System.Collections.Generic;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Items;
using OceanGuardians.Player;
using OceanGuardians.Progression;

namespace OceanGuardians.Crafting
{
    /// <summary>
    /// The Base Camp upcycling bench: click it, Coral walks over, the CraftingUI modal opens.
    /// TryCraft is all-or-nothing: ingredients are checked first, then consumed, rewards paid
    /// through EconomyManager, and EventManager.ItemCrafted raised (DayStats tallies it; future
    /// quests can hook the same event). Lifetime craft counts persist via SaveManager.
    /// </summary>
    public class CraftingWorkbench : MonoBehaviour, IInteractable
    {
        public static CraftingWorkbench Instance { get; private set; }

        [SerializeField] List<CraftingRecipeData> recipes = new();
        [SerializeField] float interactionRange = 2.5f;
        [Tooltip("Modal auto-closes when Coral is this much farther than interactionRange.")]
        [SerializeField] float closeDistancePadding = 2f;

        readonly Dictionary<string, int> lifetimeCounts = new();
        Transform player;
        bool isOpen;

        public IReadOnlyList<CraftingRecipeData> Recipes => recipes;
        public IReadOnlyDictionary<string, int> LifetimeCounts => lifetimeCounts;
        public InteractionKind Kind => InteractionKind.Workbench;
        public float InteractionRange => interactionRange;

        void Awake() => Instance = this;

        /// <summary>Builder/tests: replace the recipe list.</summary>
        public void SetRecipes(IEnumerable<CraftingRecipeData> newRecipes)
        {
            recipes.Clear();
            recipes.AddRange(newRecipes);
        }

        public int LifetimeCount(string recipeId) => lifetimeCounts.TryGetValue(recipeId, out int n) ? n : 0;

        /// <summary>Save-load: restore lifetime crafting tallies.</summary>
        public void RestoreCounts(IEnumerable<KeyValuePair<string, int>> counts)
        {
            lifetimeCounts.Clear();
            if (counts != null) foreach (var kv in counts) lifetimeCounts[kv.Key] = kv.Value;
        }

        public bool CanInteract(out string reason) { reason = null; return true; }

        public void Interact(PlayerInteraction p)
        {
            player = p.transform;
            isOpen = true;
            EventManager.RaiseCraftingToggled(this);
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            EventManager.RaiseCraftingToggled(null);
        }

        void Update()
        {
            if (!isOpen || player == null) return;
            var d = player.position - transform.position;
            d.y = 0f;
            if (d.magnitude > interactionRange + closeDistancePadding) Close();
        }

        public bool IsUnlocked(CraftingRecipeData recipe)
        {
            var progression = ProgressionManager.Instance;
            return recipe.unlockRequiredStage <= 0 || progression == null || progression.CurrentStage >= recipe.unlockRequiredStage;
        }

        public bool CanCraft(CraftingRecipeData recipe)
        {
            var bag = BagSystem.Instance;
            if (bag == null || recipe == null || !IsUnlocked(recipe)) return false;
            foreach (var cost in recipe.requiredLitter)
                if (bag.Bag.CountOf(cost.item) < cost.quantity) return false;
            foreach (var cost in recipe.requiredFinds)
                if (cost.item is CollectibleItemData find && bag.CollectionCount(find) < cost.quantity) return false;
            return true;
        }

        /// <summary>Owned/required for the UI line of one ingredient.</summary>
        public int OwnedCount(InventoryCost cost)
        {
            var bag = BagSystem.Instance;
            if (bag == null) return 0;
            return cost.item is CollectibleItemData find ? bag.CollectionCount(find) : bag.Bag.CountOf(cost.item);
        }

        public bool TryCraft(CraftingRecipeData recipe, out string message)
        {
            if (!CanCraft(recipe))
            {
                message = recipe != null && !IsUnlocked(recipe)
                    ? $"Unlocks at equipment Stage {recipe.unlockRequiredStage}."
                    : "You are missing some materials - check the recipe!";
                return false;
            }

            var bag = BagSystem.Instance;
            foreach (var cost in recipe.requiredLitter) bag.Bag.TryRemove(cost.item, cost.quantity);
            foreach (var cost in recipe.requiredFinds)
                if (cost.item is CollectibleItemData find) bag.TryRemoveFromCollection(find, cost.quantity);

            var economy = EconomyManager.Instance;
            economy?.Add(CurrencyType.CollectorCoins, recipe.rewardCollectorCoins);
            economy?.Add(CurrencyType.EcoTokens, recipe.rewardEcoTokens);
            lifetimeCounts[recipe.recipeId] = LifetimeCount(recipe.recipeId) + 1;

            Audio.AudioManager.Instance?.PlayCraftingChime();
            EventManager.RaiseItemCrafted(recipe.recipeId);
            EventManager.RaiseCheckpoint(); // auto-save
            message = $"Upcycled: {recipe.recipeName}! +{recipe.rewardCollectorCoins} Collector Coins, +{recipe.rewardEcoTokens} Eco";
            return true;
        }
    }
}
