using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Player;
using OceanGuardians.Progression;
using OceanGuardians.UI;

namespace OceanGuardians.Environment
{
    /// <summary>
    /// "The Rocky Passage" at the beach's north end: a driftwood-barred arch between bluffs.
    /// Below Stage 5 Coral just inspects it (speech bubble); at Stage 5+ a confirm modal opens
    /// and journeying on marks Region 1 Mastered (persisted by SaveManager).
    /// </summary>
    public class RegionGate : MonoBehaviour, IInteractable
    {
        public static RegionGate Instance { get; private set; }

        [SerializeField] int requiredStage = 5;
        [SerializeField] float interactionRange = 3f;
        [Tooltip("The driftwood barricade visuals, cleared away once mastered.")]
        [SerializeField] GameObject blockage;

        public bool Region1Mastered { get; private set; }
        public int RequiredStage => requiredStage;
        public InteractionKind Kind => InteractionKind.Gate;
        public float InteractionRange => interactionRange;

        void Awake() => Instance = this;

        public bool CanInteract(out string reason) { reason = null; return true; }

        public void Interact(PlayerInteraction p)
        {
            if (Region1Mastered)
            {
                SpeechBubbleManager.Instance?.SayCoral("The Rocky Shore awaits whenever I'm ready!");
                return;
            }
            int stage = ProgressionManager.Instance ? ProgressionManager.Instance.CurrentStage : 1;
            if (stage < requiredStage)
                SpeechBubbleManager.Instance?.SayCoral(
                    $"The surf is too rough through the rocky passage! I need Stage {requiredStage} Reef-Trekker Boots first.", 4.5f);
            else
                EventManager.RaiseRegionGatePrompt(this);
        }

        /// <summary>Confirmed in the modal: fanfare, barricade cleared, Region 1 mastered, auto-save.</summary>
        public void ConfirmJourney()
        {
            if (Region1Mastered) return;
            Region1Mastered = true;
            if (blockage) blockage.SetActive(false);
            Audio.AudioManager.Instance?.PlayBrassFanfare();
            EventManager.RaiseCheckpoint(); // auto-save
        }

        /// <summary>Save-load: restore mastered state without fanfare.</summary>
        public void RestoreMastered(bool mastered)
        {
            Region1Mastered = mastered;
            if (blockage) blockage.SetActive(!mastered);
        }
    }
}
