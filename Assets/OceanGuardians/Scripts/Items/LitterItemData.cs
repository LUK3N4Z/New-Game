using UnityEngine;

namespace OceanGuardians.Items
{
    /// <summary>The kiosk's three colour-coded bins.</summary>
    public enum RecycleBin
    {
        Blue,   // Plastics
        Green,  // Metals, cans & glass
        Yellow  // Cardboard, paper & natural debris
    }

    /// <summary>
    /// Trash (Plastic Bottle, Soda Can, Snack Wrapper, Glass Jar, Car Tire, Driftwood).
    /// Goes in the bag, stacks up to maxStack per slot, and is deposited at a kiosk for Recycling Credits.
    /// </summary>
    [CreateAssetMenu(menuName = "Ocean Guardians/Litter Item", fileName = "Litter_")]
    public class LitterItemData : ItemData
    {
        public RecycleBin bin = RecycleBin.Blue;
        [Tooltip("Recycling Credits earned per item when deposited at a kiosk.")]
        public int creditValue = 5;
        [Tooltip("Equipment stage needed to pick this up. 0 = always. Car Tires use 9 (Shoreline Collection Kit).")]
        public int requiredStage;
    }
}
