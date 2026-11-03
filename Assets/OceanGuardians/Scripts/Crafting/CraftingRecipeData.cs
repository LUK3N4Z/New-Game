using System;
using System.Collections.Generic;
using UnityEngine;
using OceanGuardians.Items;

namespace OceanGuardians.Crafting
{
    /// <summary>One ingredient line of a recipe.</summary>
    [Serializable]
    public class InventoryCost
    {
        public ItemData item;
        [Min(1)] public int quantity = 1;
    }

    /// <summary>Upcycling: litter + beach finds in, Collector Coins + Eco Tokens out.</summary>
    [CreateAssetMenu(menuName = "Ocean Guardians/Crafting Recipe", fileName = "Recipe_New")]
    public class CraftingRecipeData : ScriptableObject
    {
        public string recipeId;
        public string recipeName;
        [TextArea(1, 3)] public string recipeDescription;
        public Sprite recipeIcon;
        public List<InventoryCost> requiredLitter = new();
        public List<InventoryCost> requiredFinds = new();
        public int rewardCollectorCoins;
        public int rewardEcoTokens;
        [Tooltip("Equipment stage that must be owned before this recipe appears (0 = always).")]
        public int unlockRequiredStage;
    }
}
