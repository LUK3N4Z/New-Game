using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Player;
using OceanGuardians.Progression;

namespace OceanGuardians.Items
{
    /// <summary>
    /// A pick-up-able object in the world: litter (LitterData) or a natural find (CollectibleData).
    /// Needs a Collider (a generous trigger sphere makes tapping easy on mobile).
    /// Optional child ParticleSystem = sparkle so kids can spot it.
    /// </summary>
    public class LitterItem : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemData data;
        [Tooltip("Gentle idle bob so items read as interactive. 0 disables.")]
        [SerializeField] float bobHeight = 0.05f;

        Vector3 basePos;
        float bobOffset;

        public ItemData Data => data;
        public bool IsLitter => data is LitterData;

        public InteractionKind Kind => IsLitter ? InteractionKind.Litter : InteractionKind.Collectible;
        public float InteractionRange => ProgressionManager.Instance ? ProgressionManager.Instance.GrabRange : 1.5f;

        void Start()
        {
            basePos = transform.position;
            bobOffset = Random.value * 10f;
        }

        void Update()
        {
            if (bobHeight > 0f)
                transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f + bobOffset) * bobHeight);
        }

        public bool CanInteract(out string reason)
        {
            reason = null;
            if (data == null) { reason = "This item has no data assigned."; return false; }
            if (data is not LitterData litter) return true; // natural finds never need bag space

            var progression = ProgressionManager.Instance;
            if (litter.requiredStage > 0 && progression && progression.CurrentStage < litter.requiredStage)
            {
                reason = $"{litter.displayName}s are too heavy! You need the {progression.StageName(litter.requiredStage)} (Stage {litter.requiredStage}).";
                return false;
            }
            var bag = BagSystem.Instance;
            if (bag && !bag.HasRoomFor(litter))
            {
                reason = "Bag is full! Visit a Recycling Kiosk to empty.";
                return false;
            }
            return true;
        }

        public void Interact(PlayerInteraction player)
        {
            if (!CanInteract(out var reason)) { EventManager.RaiseAlert(reason); return; }

            if (data is LitterData litter) BagSystem.Instance.TryAdd(litter);
            else if (data is CollectibleData find) EconomyManager.Instance?.Add(CurrencyType.CollectorCoins, find.coinValue);

            if (!string.IsNullOrEmpty(data.journalEntryId)) OceanJournal.Instance?.Discover(data.journalEntryId);
            player.PlayPickupSound(data.pickupSound);

            gameObject.SetActive(false); // deactivate first: RestorationZone counts active litter when the event fires
            EventManager.RaiseItemCollected(data, transform.position);
            Destroy(gameObject);
        }
    }
}
