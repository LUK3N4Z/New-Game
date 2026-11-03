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

    /// <summary>A type of trash: Plastic Bottle, Soda Can, Snack Wrapper, Glass Jar, Car Tire, Driftwood.</summary>
    [CreateAssetMenu(menuName = "Ocean Guardians/Litter Data", fileName = "Litter_")]
    public class LitterData : ItemData
    {
        public RecycleBin bin = RecycleBin.Blue;
        [Tooltip("Recycling Credits earned when deposited at a kiosk.")]
        public int creditValue = 5;
        [Tooltip("Bag slots this takes up (a tire is bulky).")]
        [Min(1)] public int bagSlots = 1;
        [Tooltip("Equipment stage needed to pick this up. 0 = always. Car Tires use 9 (Shoreline Collection Kit).")]
        public int requiredStage;
    }
}
