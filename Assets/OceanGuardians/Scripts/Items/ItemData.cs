using UnityEngine;

namespace OceanGuardians.Items
{
    /// <summary>
    /// Shared definition for anything Coral can pick up: litter (LitterItemData) or natural finds
    /// (CollectibleItemData). Pure data - runtime quantities live in ItemStack / Inventory.
    /// </summary>
    public abstract class ItemData : ScriptableObject
    {
        [Tooltip("Stable save-file key, e.g. \"plastic_bottle\". Never change it once players have saves.")]
        [SerializeField] string id;
        public string displayName = "Item";
        [TextArea(1, 3)] public string description;
        public Sprite icon;
        [Tooltip("How many fit in one bag slot.")]
        [Min(1)] public int maxStack = 1;
        [Tooltip("OceanJournal entry id unlocked the first time this is picked up. Leave empty for none.")]
        public string journalEntryId;
        [Tooltip("Optional. Falls back to the player's default pickup chirp.")]
        public AudioClip pickupSound;
        [Tooltip("Floating popup colour.")]
        public Color popupColor = Color.white;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        /// <summary>Editor/builder only: assigns the save id.</summary>
        public void SetId(string newId) => id = newId;
    }
}
