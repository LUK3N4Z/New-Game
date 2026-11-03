using UnityEngine;
using OceanGuardians.Core;
using OceanGuardians.Environment;
using OceanGuardians.Items;
using OceanGuardians.Quests;

namespace OceanGuardians.UI
{
    /// <summary>
    /// Decides who says what: listens to EventManager and pops SpeechBubbleUIs over Coral,
    /// the seal pup, and the kiosk. One live bubble per speaker (new line replaces old).
    /// </summary>
    public class SpeechBubbleManager : MonoBehaviour
    {
        [SerializeField] RectTransform layer;
        [SerializeField] Camera worldCamera;

        SpeechBubbleUI coralBubble, sealBubble, kioskBubble;
        bool bagWasFull;
        int lastCreditGain, lastCreditsTotal = -1;

        public static SpeechBubbleManager Instance { get; private set; }

        Transform Coral => GameManager.Instance && GameManager.Instance.Player ? GameManager.Instance.Player.transform : null;

        void Awake() => Instance = this;

        /// <summary>Public line API for other systems (crafting, region gate, gear upgrades).</summary>
        public void SayCoral(string line, float seconds = 4f) => coralBubble = Say(coralBubble, Coral, line, seconds);

        void OnEnable()
        {
            EventManager.ItemCollected += OnItemCollected;
            EventManager.BagChanged += OnBagChanged;
            EventManager.CurrencyChanged += OnCurrencyChanged;
            EventManager.LitterDeposited += OnDeposited;
            EventManager.WildlifeRescued += OnRescued;
            EventManager.DuskFalls += OnDusk;
        }

        void OnDisable()
        {
            EventManager.ItemCollected -= OnItemCollected;
            EventManager.BagChanged -= OnBagChanged;
            EventManager.CurrencyChanged -= OnCurrencyChanged;
            EventManager.LitterDeposited -= OnDeposited;
            EventManager.WildlifeRescued -= OnRescued;
            EventManager.DuskFalls -= OnDusk;
        }

        void Start()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (QuestManager.Instance)
                QuestManager.Instance.QuestCompleted += _ =>
                    coralBubble = Say(coralBubble, Coral, "Yes! Another step toward a clean ocean!");
        }

        SpeechBubbleUI Say(SpeechBubbleUI current, Transform who, string line, float seconds = 3.5f)
        {
            if (current) Destroy(current.gameObject);
            return SpeechBubbleUI.Show(layer, worldCamera, who, line, seconds);
        }

        void OnItemCollected(ItemData item, Vector3 _)
        {
            if (item != null && item.Id == "sea_glass")
                coralBubble = Say(coralBubble, Coral, "Ooh, sea glass! Into the collection pouch.");
        }

        void OnBagChanged(int current, int max)
        {
            bool full = max > 0 && current >= max;
            if (full && !bagWasFull)
                coralBubble = Say(coralBubble, Coral, "My pack is bursting! Better head back to the recycling bins.", 4f);
            bagWasFull = full;
        }

        void OnCurrencyChanged(CurrencyType type, int total)
        {
            // The kiosk pays credits just before LitterDeposited fires; remember the gain for its bubble.
            if (type != CurrencyType.RecyclingCredits) return;
            if (lastCreditsTotal >= 0 && total > lastCreditsTotal) lastCreditGain = total - lastCreditsTotal;
            lastCreditsTotal = total;
        }

        void OnDeposited(int count)
        {
            if (count <= 0) return;
            var kiosk = FindAnyObjectByType<RecyclingKiosk>();
            if (kiosk) kioskBubble = Say(kioskBubble, kiosk.transform, $"(thumbs up) Sorted! +{lastCreditGain} Credits", 3f);
        }

        void OnRescued(string saveId)
        {
            foreach (var rescue in FindObjectsByType<WildlifeRescue>())
            {
                if (rescue.SaveId != saveId) continue;
                sealBubble = Say(sealBubble, rescue.transform, "Arf! Arf!", 3f);
                // Floating heart emotes over the pup.
                FloatingText.Spawn(layer, worldCamera, rescue.transform.position, "<3 <3 <3", new Color(1f, 0.45f, 0.65f), 40f);
                break;
            }
        }

        void OnDusk() => coralBubble = Say(coralBubble, Coral, "The sun is setting... time to head back to camp.", 4f);
    }
}
