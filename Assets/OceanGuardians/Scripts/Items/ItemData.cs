using UnityEngine;

namespace OceanGuardians.Items
{
    /// <summary>Shared data for anything Coral can pick up: litter (LitterData) or natural finds (CollectibleData).</summary>
    public abstract class ItemData : ScriptableObject
    {
        public string displayName = "Item";
        public Sprite icon;
        [Tooltip("OceanJournal entry id unlocked the first time this is picked up. Leave empty for none.")]
        public string journalEntryId;
        [Tooltip("Optional. Falls back to the player's default pickup chirp.")]
        public AudioClip pickupSound;
        [Tooltip("Floating popup colour.")]
        public Color popupColor = Color.white;
    }
}
