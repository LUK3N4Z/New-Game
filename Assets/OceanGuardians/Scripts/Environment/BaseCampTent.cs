using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Player;

namespace OceanGuardians.Environment
{
    /// <summary>Coral's camp tent: click it and Coral walks over, then the Day Wrap-Up opens.</summary>
    public class BaseCampTent : MonoBehaviour, IInteractable
    {
        [SerializeField] float interactionRange = 2.5f;

        public static BaseCampTent Instance { get; private set; }
        public InteractionKind Kind => InteractionKind.Camp;
        public float InteractionRange => interactionRange;

        void Awake() => Instance = this;

        public bool CanInteract(out string reason) { reason = null; return true; }

        public void Interact(PlayerInteraction p) => DayNightCycle.Instance?.RequestDayEnd();
    }
}
