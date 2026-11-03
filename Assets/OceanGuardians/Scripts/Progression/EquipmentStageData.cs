using System;
using System.Collections.Generic;
using UnityEngine;

namespace OceanGuardians.Progression
{
    /// <summary>One rung of the equipment ladder.</summary>
    [Serializable]
    public class EquipmentStage
    {
        public int stage;
        public string name;
        [TextArea(1, 3)] public string description;
        [Tooltip("Recycling Credits to buy. Stage 1 is owned from the start.")]
        public int creditCost;
        [Tooltip("Extra bag slots granted by this stage.")]
        public int bagCapacityBonus;
        [Tooltip("Extra grabber reach (metres) granted by this stage.")]
        public float grabRangeBonus;
        public Sprite icon;
    }

    /// <summary>
    /// The canonical 50-stage equipment database. Creating the asset (Create > Ocean Guardians >
    /// Equipment Stage Database) auto-fills all 50 stages; tweak costs/bonuses in the Inspector after.
    /// </summary>
    [CreateAssetMenu(menuName = "Ocean Guardians/Equipment Stage Database", fileName = "EquipmentStageDatabase")]
    public class EquipmentStageData : ScriptableObject
    {
        public static readonly string[] CanonicalNames =
        {
            "Basic Collection Bag", "Protective Gloves", "Basic Litter Picker", "Heavy-Duty Collection Bag", "Beach Cleaning Kit",
            "Extended Litter Picker", "Waterproof Boots", "Explorer Backpack", "Shoreline Collection Kit", "Professional Beach Kit",
            "Lightweight Wetsuit", "Water-Resistant Gloves", "Snorkel", "Diving Mask and Fins", "Shallow-Water Collection Bag",
            "Advanced Snorkel Kit", "Underwater Storage Bag", "Buoyancy Equipment", "Supervised Diving Equipment", "Underwater Cleanup Kit",
            "Basic Paddleboard", "Improved Paddleboard", "Waterproof Storage Container", "Paddleboard Collection Attachment", "Expedition Paddleboard",
            "Basic Kayak", "Improved Kayak Paddle", "Kayak Storage Compartment", "Kayak Collection Equipment", "Expedition Kayak",
            "Rowing Boat", "Improved Rowing Equipment", "Electric Boat Motor", "Marine Storage System", "Small Cleanup Vessel",
            "Professional Cleanup Boat", "Floating Debris Collection System", "Marine Monitoring Equipment", "Advanced Storage System", "Multi-Person Cleanup Vessel",
            "Offshore Support Vessel", "Advanced Collection Equipment", "Automated Sorting System", "Support Ship", "Offshore Cleanup Fleet",
            "Ocean Cleanup Platform", "Advanced Floating Collection System", "Industrial Recycling Facility", "Global Cleanup Fleet", "Ocean Guardians Mega System",
        };

        static readonly string[] StorageWords = { "Bag", "Backpack", "Storage", "Container", "Compartment" };
        static readonly string[] VesselWords = { "Vessel", "Boat", "Ship", "Fleet", "Platform", "Mega" };
        static readonly string[] ReachWords = { "Picker", "Kit", "Collection Equipment", "Collection Attachment", "Collection System" };

        public List<EquipmentStage> stages = new();

        public int Count => stages.Count;

        /// <summary>1-based lookup. Returns null past the last stage.</summary>
        public EquipmentStage Get(int stage) => stage >= 1 && stage <= stages.Count ? stages[stage - 1] : null;

        void Reset() => PopulateDefaults(); // runs when the asset is first created in the editor

        /// <summary>Fills all 50 stages with a smooth cost curve and name-based bonuses (storage = bag slots, pickers/kits = reach).</summary>
        [ContextMenu("Reset To Canonical 50 Stages")]
        public void PopulateDefaults()
        {
            stages.Clear();
            for (int i = 0; i < CanonicalNames.Length; i++)
            {
                int s = i + 1;
                string n = CanonicalNames[i];
                var stage = new EquipmentStage
                {
                    stage = s,
                    name = n,
                    // ponytail: quadratic curve (45 at stage 2 -> ~8000 at 50); tune once real income rates are playtested
                    creditCost = s == 1 ? 0 : Mathf.RoundToInt((15 + 10 * s + 3 * s * s) / 5f) * 5,
                    bagCapacityBonus = s == 1 ? 0
                        : ContainsAny(n, VesselWords) ? 10 + s / 5
                        : ContainsAny(n, StorageWords) ? 5 + s / 5
                        : 0,
                    grabRangeBonus = ContainsAny(n, ReachWords) ? 0.25f : 0f,
                };
                stage.description = Describe(stage);
                stages.Add(stage);
            }
        }

        static bool ContainsAny(string text, string[] words)
        {
            foreach (var w in words)
                if (text.Contains(w)) return true;
            return false;
        }

        static string Describe(EquipmentStage s)
        {
            if (s.stage == 1) return "Your trusty first bag. Holds 10 pieces of litter.";
            var parts = new List<string>();
            if (s.bagCapacityBonus > 0) parts.Add($"+{s.bagCapacityBonus} bag slots");
            if (s.grabRangeBonus > 0f) parts.Add($"+{s.grabRangeBonus:0.##}m grabber reach");
            if (s.stage == 9) parts.Add("lets you haul heavy car tires");
            return parts.Count > 0 ? string.Join(", ", parts) + "." : "Another step toward protecting the open ocean!";
        }
    }
}
