using System.Text;
using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Items;
using OceanGuardians.Player;
using OceanGuardians.Progression;

namespace OceanGuardians.Environment
{
    /// <summary>
    /// 3-bin beach kiosk (Blue plastics / Green metals &amp; glass / Yellow paper &amp; natural).
    /// Click it -> Coral walks over -> the HUD opens the kiosk modal (Quick Deposit + Upgrade Shop).
    /// The modal closes itself when Coral walks away.
    /// </summary>
    public class RecyclingKiosk : MonoBehaviour, IInteractable
    {
        [SerializeField] float interactionRange = 2.5f;
        [Tooltip("Modal auto-closes when Coral is this much farther than interactionRange.")]
        [SerializeField] float closeDistancePadding = 2f;
        [Tooltip("Bonus Eco Tokens per this many items deposited (rounded up).")]
        [SerializeField] int itemsPerEcoToken = 5;
        [SerializeField] string journalEntryOnFirstDeposit = "circular_economy";

        Transform player;
        bool isOpen;

        public InteractionKind Kind => InteractionKind.Kiosk;
        public float InteractionRange => interactionRange;

        public bool CanInteract(out string reason) { reason = null; return true; }

        public void Interact(PlayerInteraction p)
        {
            player = p.transform;
            isOpen = true;
            EventManager.RaiseKioskToggled(this);
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            EventManager.RaiseKioskToggled(null);
        }

        void Update()
        {
            if (!isOpen || player == null) return;
            var d = player.position - transform.position;
            d.y = 0f;
            if (d.magnitude > interactionRange + closeDistancePadding) Close();
        }

        /// <summary>
        /// Empties the bag into the right bins, pays Recycling Credits plus bonus Eco Tokens,
        /// and returns a short summary for the UI.
        /// </summary>
        public string QuickDepositAll()
        {
            var bag = BagSystem.Instance;
            if (bag == null || bag.IsEmpty) return "Your bag is empty. Go find some litter!";

            int credits = 0, count = 0;
            var perBin = new int[3];
            foreach (var stack in bag.EmptyAll())
            {
                if (stack.item is not LitterItemData litter) continue;
                credits += litter.creditValue * stack.quantity;
                perBin[(int)litter.bin] += stack.quantity;
                count += stack.quantity;
            }
            int eco = Mathf.CeilToInt(count / (float)Mathf.Max(1, itemsPerEcoToken));

            var economy = EconomyManager.Instance;
            economy?.Add(CurrencyType.RecyclingCredits, credits);
            economy?.Add(CurrencyType.EcoTokens, eco);
            OceanJournal.Instance?.Discover(journalEntryOnFirstDeposit);
            EventManager.RaiseCheckpoint(); // auto-save

            var sb = new StringBuilder();
            sb.Append($"Sorted {count} items  ");
            sb.Append($"<color=#4AA3FF>Blue {perBin[0]}</color>  ");
            sb.Append($"<color=#5CD65C>Green {perBin[1]}</color>  ");
            sb.Append($"<color=#FFD24A>Yellow {perBin[2]}</color>\n");
            sb.Append($"+{credits} Credits   +{eco} Eco");
            return sb.ToString();
        }
    }
}
