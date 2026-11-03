using System.Collections;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Player;
using OceanGuardians.Progression;

namespace OceanGuardians.Environment
{
    /// <summary>
    /// "Tender Care" rescue: a seal pup tangled in ghost netting.
    /// Near it, a glowing aura appears and the HUD shows the Care button. Clicking the pup (or the
    /// button) walks Coral over and plays: crouch &amp; calm -> snip -> net gone + heart burst ->
    /// +25 Eco Tokens and Journal entry #1.
    /// </summary>
    public class WildlifeRescue : MonoBehaviour, IInteractable
    {
        [SerializeField] string animalName = "Seal Pup";
        [Tooltip("Unique per rescue in the scene; SaveManager remembers it was completed.")]
        [SerializeField] string saveId = "seal_pup";
        [SerializeField] float interactionRange = 1.6f;
        [Tooltip("Aura + Care prompt appear inside this radius.")]
        [SerializeField] float proximityRadius = 5f;

        [Header("Scene References")]
        [SerializeField] GameObject netObject;
        [SerializeField] GameObject auraObject;
        [SerializeField] ParticleSystem heartBurst;
        [Tooltip("Optional. Triggers: Freed, Celebrate.")]
        [SerializeField] Animator animalAnimator;
        [Tooltip("The model that does a happy hop. Defaults to this transform.")]
        [SerializeField] Transform animalVisual;

        [Header("Rewards")]
        [SerializeField] int ecoTokenReward = 25;
        [SerializeField] string journalEntryId = "harbor_seal";
        [SerializeField] string secondaryJournalEntryId = "ghost_gear";

        [Header("Timing (seconds)")]
        [SerializeField] float calmDuration = 1.4f;
        [SerializeField] float snipDuration = 0.6f;
        [SerializeField] float celebrateDuration = 1.2f;

        Transform player;
        bool playerNear;

        public string SaveId => saveId;
        public bool IsRescued { get; private set; }
        public bool InProgress { get; private set; }
        public InteractionKind Kind => InteractionKind.Rescue;
        public float InteractionRange => interactionRange;

        void Start()
        {
            if (animalVisual == null) animalVisual = transform;
            if (auraObject) auraObject.SetActive(false);
            var gm = GameManager.Instance;
            if (gm && gm.Player) player = gm.Player.transform;
        }

        void Update()
        {
            if (IsRescued || player == null) return;
            var d = player.position - transform.position;
            d.y = 0f;
            bool near = d.magnitude <= proximityRadius;
            if (near == playerNear) return;

            playerNear = near;
            if (auraObject) auraObject.SetActive(near);
            EventManager.RaiseRescueNearby(near ? this : null);
        }

        public bool CanInteract(out string reason)
        {
            reason = IsRescued ? $"The {animalName} is safe and happy!" : null;
            return !IsRescued && !InProgress;
        }

        public void Interact(PlayerInteraction p)
        {
            if (CanInteract(out _)) StartCoroutine(TenderCare(p.Controller));
        }

        IEnumerator TenderCare(PlayerController coral)
        {
            InProgress = true;
            coral.InputLocked = true;
            coral.StopMoving();
            coral.FaceTowards(transform.position);

            // 1. Crouch and calm the animal.
            coral.SetAnimBool("Crouch", true);
            EventManager.RaiseAlert($"Shh... it's okay, little {animalName}.");
            yield return new WaitForSeconds(calmDuration);

            // 2. Quick snip.
            coral.SetAnimTrigger("Snip");
            yield return new WaitForSeconds(snipDuration);

            // 3. Net disappears, celebration.
            if (netObject) netObject.SetActive(false);
            if (auraObject) auraObject.SetActive(false);
            if (heartBurst) heartBurst.Play();
            if (animalAnimator) animalAnimator.SetTrigger("Freed");
            coral.SetAnimBool("Crouch", false);
            coral.SetAnimTrigger("Celebrate");
            yield return HappyHop();

            // 4. Rewards.
            IsRescued = true;
            EconomyManager.Instance?.Add(CurrencyType.EcoTokens, ecoTokenReward);
            EventManager.RaiseAlert($"You freed the {animalName}! +{ecoTokenReward} Eco");
            EventManager.RaiseRescueNearby(null);
            OceanJournal.Instance?.Discover(journalEntryId);
            OceanJournal.Instance?.Discover(secondaryJournalEntryId);

            InProgress = false;
            coral.InputLocked = false;
            EventManager.RaiseCheckpoint(); // auto-save
        }

        /// <summary>Save-load: show the animal already freed, with no rewards or popups.</summary>
        public void RestoreRescued()
        {
            IsRescued = true;
            if (netObject) netObject.SetActive(false);
            if (auraObject) auraObject.SetActive(false);
        }

        /// <summary>Two little bounces so the moment reads even without an animal rig.</summary>
        IEnumerator HappyHop()
        {
            var basePos = animalVisual.localPosition;
            for (float t = 0f; t < celebrateDuration; t += Time.deltaTime)
            {
                float k = t / celebrateDuration;
                animalVisual.localPosition = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * 0.35f;
                yield return null;
            }
            animalVisual.localPosition = basePos;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.6f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, proximityRadius);
        }
    }
}
